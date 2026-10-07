using HsmsTester.API;
using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Tcp;
using HsmsTester.Util;
using System;
using System.Collections.Concurrent;
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
        // 처음 접근할 때 생성 (생성자에서 쓰는 MsgDefinePath 등 정적 필드가 먼저 초기화되도록)
        private static readonly Lazy<HsmsManager> _instance = new Lazy<HsmsManager>(() => new HsmsManager());
        public static HsmsManager Instance => _instance.Value;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            private set
            {
                bool wasSelected = _isSelected;
                _isSelected = value;
                if (wasSelected == false && value == true)
                {
                    SendSelectedMessages();
                }
            }
        }
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

        // 메시지가 들어올 때까지 스레드가 대기(Take)하도록 BlockingCollection 사용
        private readonly BlockingCollection<(HsmsHeaderMessage, byte[])> _sendMsgQueue = new BlockingCollection<(HsmsHeaderMessage, byte[])>();     // Send Message Queue
        private readonly BlockingCollection<byte[]> _receiveMsgQueue = new BlockingCollection<byte[]>();                                            // Receive Message Queue

        // TCP로 받은 데이터를 HSMS 메시지 단위로 자르기 위한 버퍼 (TCP는 메시지가 붙거나 나뉘어 올 수 있음)
        private readonly List<byte> _recvBuffer = new List<byte>();
        private const int MAX_MESSAGE_LENGTH = 16 * 1024 * 1024;     // ponytail: 이보다 큰 Length는 깨진 데이터로 보고 버퍼를 버림, 큰 메시지 쓰면 늘릴 것

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

            Config = new HsmsConfiguration(Path.Combine( Environment.CurrentDirectory,"config.ini"));

            var random = new Random();
            _systemByte = (uint)random.Next();

            HsmsMsgDefines = ReadJsonFile<List<HsmsMsgDefine>>(MsgDefinePath) ?? [];
            HsmsConditionalDefines = ReadJsonFile<List<HsmsConditionalDefine>>(ConditionalDefinePath) ?? [];
        }

        // 정의 파일 (실행폴더/msgDefine.json, conditionalDefine.json)
        private static readonly string MsgDefinePath = Path.Combine(AppContext.BaseDirectory, "msgDefine.json");
        private static readonly string ConditionalDefinePath = Path.Combine(AppContext.BaseDirectory, "conditionalDefine.json");
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        // 메시지 정의를 파일로 저장하고 현재 정의로 교체
        public bool SaveMsgDefines(List<HsmsMsgDefine> defines)
        {
            if (WriteJsonFile(MsgDefinePath, defines) == false) return false;
            HsmsMsgDefines = defines;
            return true;
        }

        // 조건 응답 정의를 파일로 저장하고 현재 정의로 교체
        public bool SaveConditionalDefines(List<HsmsConditionalDefine> defines)
        {
            if (WriteJsonFile(ConditionalDefinePath, defines) == false) return false;
            HsmsConditionalDefines = defines;
            return true;
        }

        private static bool WriteJsonFile<T>(string path, T data)
        {
            try
            {
                // 저장 중 실패해도 기존 파일이 깨지지 않도록 임시 파일에 쓴 후 교체
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(data, _jsonOptions));
                File.Move(tempPath, path, true);
                return true;
            }
            catch (Exception ex)
            {
                LogBroadcaster.Instance.Write($"{Path.GetFileName(path)} 저장 실패 : {ex.Message}");
                return false;
            }
        }

        // 프로그램 시작 시 정의 파일 불러오기. 파일이 없거나 읽기 실패하면 null (빈 정의로 시작)
        private static T? ReadJsonFile<T>(string path) where T : class
        {
            if (File.Exists(path) == false) return null;

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _jsonOptions);
            }
            catch (Exception ex)
            {
                LogBroadcaster.Instance.Write($"{Path.GetFileName(path)} 불러오기 실패 : {ex.Message}");
                return null;
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
            IsSelected = false;

            if (Config.Active == true)
            {
                // 이벤트를 먼저 구독한 후 연결 시작 (연결이 빨리 끝나도 Connected 이벤트를 놓치지 않도록)
                _client = new TCPClient(Config.IPAddress, Config.Port);
                _client.Connected += OnConnectedClient;
                _client.Disconnected += OnDisconnectedClient;
                _client.DataReceived += OnRecvMessage;
                _client.ConnectRequest();
            }
            else
            {
                _server = new TCPServer(Config.IPAddress, Config.Port);
                _server.Start();
                _server.ClientConnected += OnConnectedClient;
                _server.ClientDisconnected += OnDisconnectedClient;
                _server.DataReceived += OnRecvMessage;

            }
            // 이전 스레드 종료 후 새 토큰으로 시작. 각 스레드는 시작할 때 받은 토큰만 사용 (재시작 시 이전 스레드가 남지 않도록)
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _readMsgThread = new Thread(() => _readThreadDowork(token)) { IsBackground = true };
            _readMsgThread.Start();

            _writeMsgThread = new Thread(() => _writeThreadDowork(token)) { IsBackground = true };
            _writeMsgThread.Start();

            _timeoutCheckThread = new Thread(() => _timeoutCheckThreadDowork(token)) { IsBackground = true };
            _timeoutCheckThread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _client?.Dispose();
            _client = null;
            _server?.Dispose();
            _server = null;     // Start()에서 다시 Dispose하지 않도록
            while (_receiveMsgQueue.TryTake(out _) == true) { }
            while (_sendMsgQueue.TryTake(out _) == true) { }
            ClearRecvBuffer();
        }

        public bool IsConnected()
        {
            if (Config.Active == true)
            {
                if (_client == null) return false;
                return _client.IsConnected;
            }
            else
            {
                if (_server == null) return false;
                return _server.ConnectedClient?.Connected ?? false;
            }
        }


        private void OnConnectedClient()
        {
            LogBroadcaster.Instance.Write("TCP 연결됨");
            ClearRecvBuffer();
            if (Config.Active == true)
            {
                SendControlMessage(eSessionSType.SelectRequest);
            }
        }

        // 연결이 이미 끊긴 상태라 Deselect.req를 보낼 수 없음. 세션 상태와 보내지 못한 메시지를 정리 (다음 연결에 이전 메시지가 나가지 않도록)
        private void OnDisconnectedClient()
        {
            ClearRecvBuffer();
            IsSelected = false;
            while (_sendMsgQueue.TryTake(out _) == true) { }
        }

        // 받은 데이터를 버퍼에 쌓고, 앞 4byte Length 기준으로 완성된 메시지만 잘라서 큐에 넣는다.
        private void OnRecvMessage(byte[] msg, int length)
        {
            lock (_recvBuffer)
            {
                _recvBuffer.AddRange(msg.AsSpan(0, length));

                while (_recvBuffer.Count >= 4)
                {
                    long bodyLength = ((long)_recvBuffer[0] << 24) | ((long)_recvBuffer[1] << 16) | ((long)_recvBuffer[2] << 8) | _recvBuffer[3];
                    if (bodyLength < 10 || bodyLength > MAX_MESSAGE_LENGTH)
                    {
                        // Header(10byte)보다 짧거나 비정상적으로 큰 Length → 깨진 데이터, 버퍼 버림
                        _recvBuffer.Clear();
                        break;
                    }

                    int totalLength = 4 + (int)bodyLength;
                    if (_recvBuffer.Count < totalLength) break;     // 아직 다 안 옴

                    _receiveMsgQueue.Add(_recvBuffer.GetRange(0, totalLength).ToArray());
                    _recvBuffer.RemoveRange(0, totalLength);
                }
            }
        }

        private void ClearRecvBuffer()
        {
            lock (_recvBuffer)
            {
                _recvBuffer.Clear();
            }
        }

        private void _readThreadDowork(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                try
                {
                    byte[] recvMsg = _receiveMsgQueue.Take(token);     // 메시지가 올 때까지 대기
                    {
                        var header = new HsmsHeaderMessage();
                        header.ParsingRecvToHeader(recvMsg);

                        if (header.Stype != 0)
                        {
                            var sessionType = (eSessionSType)header.Stype;
                            LogBroadcaster.Instance.Write($"Control Message 수신 : [{sessionType}] \n\t\t\t데이터: {byteArrToStr(recvMsg)}");


                            // Control Msg 처리
                            switch (header.Stype)
                            {
                                // Select.Req
                                case 1:
                                    {
                                        SendControlMessage(eSessionSType.SelectResponse, header.SystemByte);
                                        IsSelected = true;      // Passive : Select.rsp 전송 후 Selected
                                    }
                                    break;
                                // Select.rsp
                                case 2:
                                    {
                                        // Active : Select Status(Byte3)가 0이면 성공
                                        if (header.HeaderByte3 == 0)
                                        {
                                            IsSelected = true;
                                        }
                                    }
                                    break;
                                // Deselect.Req
                                case 3:
                                    {
                                        SendControlMessage(eSessionSType.DeselectResponse, header.SystemByte);
                                        IsSelected = false;
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
                                LogBroadcaster.Instance.Write($"Data Message 수신 : [{json.Header.StreamFunction}] {json.FullName}\n{msgToLog(json)}");
                                SendAutoResponses(json);
                            }
                            else
                            {
                                LogBroadcaster.Instance.Write($"Data Message 수신 (파싱 실패) \n\t\t\t데이터 : {byteArrToStr(recvMsg)}");
                            }

                        }

                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void _writeThreadDowork(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                try
                {
                    (HsmsHeaderMessage, byte[]) recvMsg = _sendMsgQueue.Take(token);     // 보낼 메시지가 올 때까지 대기
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
        private void _timeoutCheckThreadDowork(CancellationToken token)
        {
            // 1초마다 확인 (종료 요청 시 즉시 빠져나옴)
            while (token.WaitHandle.WaitOne(1000) == false)
            {
                try
                {
                    if (IsConnected() == true)
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


        internal void EnequeueSendMsg(HsmsHeaderMessage header, byte[] msg)
        {
            _sendMsgQueue.Add((header, msg));
        }
    }
}
