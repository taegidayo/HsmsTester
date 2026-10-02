using HsmsTester.API;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Tcp;
using HsmsTester.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HsmsTester.Manager
{
    partial class HsmsManager : IDisposable
    {
        public static HsmsManager Instance { get; } = new HsmsManager();
        public bool IsSelected { get; private set; }

        private TCPServer _server = null;
        private TCPClient _client = null;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private uint _systemByte;

        // 새 SystemByte 발급. Data Message는 2, Control Message는 4씩 증가 (read/timeout 스레드에서 동시에 호출되므로 Interlocked)
        internal uint NextDataSystemByte() => Interlocked.Add(ref _systemByte, 2);
        internal uint NextControlSystemByte() => Interlocked.Add(ref _systemByte, 4);

        public HsmsConfiguration Config = null;
        public List<HsmsMsgDefine> HsmsMsgDefines = [];
        public List<HsmsConditionalDefine> HsmsConditionalDefines = [];

        public bool T3TimeoutCheckFlag = false;// T3에 대한 결과를 확인하기 위한 플래그, true라면 S6F11에 대해 응답하지 않는다.

        private Queue<(HsmsHeaderMessage, byte[])> _sendMsgQueue = new Queue<(HsmsHeaderMessage, byte[])>();                           // Send Message Queue
        private Queue<byte[]> _receiveMsgQueue = new Queue<byte[]>();                           // Receive Message Queue
        private object _readLock = new object();
        private object _writeLock = new object();

        // 읽은 메시지에 대하여 처리하는 프로세스
        private Thread _readMsgThread = null;
        // 작성해야할 메시지에 대하여 처리하는 프로세스
        private Thread _writeMsgThread = null;
        // 타임아웃 확인용 프로세스
        private Thread _timeoutCheckThread = null;

        // t3Time 체크를 위한 데이터
        private Dictionary<uint, ulong> _t3TimeoutCheck = new Dictionary<uint, ulong>();

        private HsmsManager()
        {
            LibraryController.Instance.RegisterDisposable(this);

            Config = new HsmsConfiguration(Environment.CurrentDirectory);

            var random = new Random();
            _systemByte = (uint)random.Next();

            LoadMsgDefines();
        }

        // 메시지 정의 파일 (실행폴더/msgDefine.json)
        private static readonly string MsgDefinePath = Path.Combine(AppContext.BaseDirectory, "msgDefine.json");
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        // 메시지 정의를 파일로 저장하고 현재 정의로 교체
        public bool SaveMsgDefines(List<HsmsMsgDefine> defines)
        {
            try
            {
                // 저장 중 실패해도 기존 파일이 깨지지 않도록 임시 파일에 쓴 후 교체
                var tempPath = MsgDefinePath + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(defines, _jsonOptions));
                File.Move(tempPath, MsgDefinePath, true);
            }
            catch (Exception ex)
            {
                LogBroadcaster.Instance.Write($"msgDefine.json 저장 실패 : {ex.Message}");
                return false;
            }

            HsmsMsgDefines = defines;
            return true;
        }

        // 프로그램 시작 시 메시지 정의 파일 불러오기. 파일이 없거나 읽기 실패하면 빈 정의로 시작
        private void LoadMsgDefines()
        {
            if (File.Exists(MsgDefinePath) == false) return;

            try
            {
                HsmsMsgDefines = JsonSerializer.Deserialize<List<HsmsMsgDefine>>(File.ReadAllText(MsgDefinePath), _jsonOptions) ?? [];
            }
            catch (Exception ex)
            {
                LogBroadcaster.Instance.Write($"msgDefine.json 불러오기 실패 : {ex.Message}");
            }
        }

        public void Start()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
            if (Config.Active == true)
            {
                _client = new TCPClient(Config.IPAddress, Config.Port);
                _client.ConnectRequest();
                _client.Connected += OnConnectedClient;
                _client.Disconnected += OnDisconnectedClient;
                _client.DataReceived += OnRecvMessage;
            }
            else
            {
                _server = new TCPServer(Config.IPAddress, Config.Port);
                _server.Start();
                _server.ClientConnected += OnConnectedClient;
                _server.ClientDisconnected += OnDisconnectedClient;
                _server.DataReceived += OnRecvMessage;

            }
            _readMsgThread = new Thread(_readThreadDowork);
            _readMsgThread.Start();

            _writeMsgThread = new Thread(_writeThreadDowork);
            _writeMsgThread.Start();

            _timeoutCheckThread = new Thread(_timeoutCheckThreadDowork);
            _timeoutCheckThread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _client?.Dispose();
            _server?.Dispose();
            _receiveMsgQueue.Clear();
            _sendMsgQueue.Clear();
        }

        private void OnConnectedClient()
        {
            if (Config.Active == true)
            {
                SendControlMessage(eSessionSType.SelectRequest);
            }
        }

        private void OnDisconnectedClient()
        {
            SendControlMessage(eSessionSType.DeselectRequest);
        }

        private void OnRecvMessage(byte[] msg, int length)
        {
            EnequeueReceiveMsg(msg);
        }

        private void _readThreadDowork()
        {
            while (_cts.Token.IsCancellationRequested == false)
            {
                try
                {
                    byte[] recvMsg = DequeueReceiveMsg();
                    if (recvMsg != null)
                    {
                        var header = new HsmsHeaderMessage();
                        header.ParsingRecvToHeader(recvMsg);

                        if (header.Stype != 0)
                        {
                            // Control Msg 처리
                            switch (header.Stype)
                            {
                                // Select.Req
                                case 1:
                                    {
                                        SendControlMessage(eSessionSType.SelectResponse, header.SystemByte);
                                    }
                                    break;
                                // Select.rsp
                                case 2:
                                    {
                                        IsSelected = true;
                                    }
                                    break;
                                // Deselect.Req
                                case 3:
                                    {
                                        SendControlMessage(eSessionSType.DeselectResponse, header.SystemByte);
                                    }
                                    break;
                                // Deselect.rsp
                                case 4:
                                    {
                                        IsSelected = false;
                                    }
                                    break;
                                // Linktest.req
                                case 5:
                                    {
                                        SendControlMessage(eSessionSType.LinktestResponse, header.SystemByte);
                                    }
                                    break;
                                // Linktest.rsp
                                case 6:
                                    {
                                    }
                                    break;
                                // Reject.req
                                case 7:
                                    {
                                    }
                                    break;
                                // Separate.req
                                case 8:
                                    {

                                    }
                                    break;
                            }
                        }
                        else
                        {
                            // Data Msg 처리
                            HsmsJson json = new HsmsJson();
                            if (json.FromHsmsBytes(recvMsg) == true)
                            {
                                SendAutoResponses(json);
                            }

                        }

                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void _writeThreadDowork()
        {
            while (_cts.Token.IsCancellationRequested == false)
            {
                try
                {
                    (HsmsHeaderMessage?, byte[]) recvMsg = DequeueSendMsg();
                    if (recvMsg.Item1 != null)
                    {
                        if (Config.Active == true)
                        {
                            _client.SendAsync(recvMsg.Item2);
                        }
                        else
                        {
                            _server.SendAsync(recvMsg.Item2);
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private ulong _lastSendTime = 0;
        private void _timeoutCheckThreadDowork()
        {
            while (_cts.Token.IsCancellationRequested == false)
            {
                try
                {
                    if (_client.IsConnected == true)
                    {
                        if (Win32Api.GetDuration_ms(_lastSendTime) > 30000)
                        {
                            SendControlMessage(eSessionSType.LinktestRequest);
                            _lastSendTime = Win32Api.TickCount64;
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }


        private void EnequeueReceiveMsg(byte[] msg)
        {
            lock (_readLock)
            {
                _receiveMsgQueue.Enqueue(msg);
            }
        }

        private byte[] DequeueReceiveMsg()
        {
            lock (_readLock)
            {
                if (_receiveMsgQueue.Count == 0)
                {
                    return [];
                }
                return _receiveMsgQueue.Dequeue();
            }
        }

        internal void EnequeueSendMsg(HsmsHeaderMessage header, byte[] msg)
        {
            lock (_readLock)
            {
                _sendMsgQueue.Enqueue((header, msg));
            }
        }

        private (HsmsHeaderMessage?, byte[]) DequeueSendMsg()
        {
            lock (_readLock)
            {
                if (_sendMsgQueue.Count == 0)
                {
                    return (null, []);
                }
                return _sendMsgQueue.Dequeue();
            }
        }
    }
}
