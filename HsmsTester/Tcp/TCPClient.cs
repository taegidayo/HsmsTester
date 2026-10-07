using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Tcp
{
    public class TCPClient : IDisposable
    {
        private TcpClient? _client = null;

        public bool IsConnected => _client != null && _client.Connected;

        private CancellationTokenSource _cts = new();
        public IPAddress? IP { get; private set; } = null;
        public int Port { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<byte[], int>? DataReceived;

        // 생성만 하고 연결은 하지 않는다. 이벤트 구독 후 ConnectRequest()로 연결 시작
        public TCPClient(string ip, int port)
        {
            Port = port;
            if (IPAddress.TryParse(ip, out var address) == true)
            {
                IP = address;
            }
        }

        public bool SetTcpProperty(string ip,int port)
        {
            if(IPAddress.TryParse(ip, out var address)== false)
            {
                return false;
            }

            IP = address;
            Port = port;

            return ConnectRequest();
        }

        public bool ConnectRequest()
        {
            if (IP == null) return false;
            Disconnect();

            // 취소된 토큰은 되돌릴 수 없으므로 연결할 때마다 새로 만든다
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
            return true;
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                if (IP == null) return;

                // 재연결 시 이전 루프가 새 연결을 건드리지 않도록 이번 루프의 소켓은 지역 변수로 관리
                var client = new TcpClient();
                _client = client;
                bool connected = false;
                try
                {
                    // 연결을 기다리는 동안 스레드는 점유되지 않음
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    await client.ConnectAsync(IP, Port, timeout.Token);

                    connected = true;
                    Connected?.Invoke();
                    await ReceiveLoopAsync(client, token);   // 연결 유지하며 수신
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested == false)
                {
                    // 연결 타임아웃 → 재시도
                }
                catch (OperationCanceledException)
                {
                    // Disconnect() 요청 → 종료
                }
                catch (SocketException)
                {
                    // 연결 거부 → 재시도
                }
                catch (IOException)
                {
                    // 수신 중 연결 끊김 → 재시도
                }
                catch (ObjectDisposedException)
                {
                    // Disconnect()로 소켓 닫힘 → 종료
                }
                finally
                {
                    client.Dispose();
                    if (_client == client) _client = null;
                    if (connected == true) Disconnected?.Invoke();
                }

                if (token.IsCancellationRequested == false)
                    await Task.Delay(1000, token).ContinueWith(_ => { }); // 재시도 간격
            }
        }

        private async Task ReceiveLoopAsync(System.Net.Sockets.TcpClient client, CancellationToken token)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            while (token.IsCancellationRequested == false)
            {
                int n = await stream.ReadAsync(buffer, token);  // 수신 대기 중에도 스레드 점유 X
                if (n == 0) break;                               // 서버가 연결 종료
                DataReceived?.Invoke(buffer, n);
            }
        }

        public async Task SendAsync(byte[] data)
        {
            var client = _client;
            if (client is { Connected: true })
                await client.GetStream().WriteAsync(data, _cts.Token);
        }

        public void Disconnect()
        {
            _cts.Cancel();
            _client?.Close();       // 수신 루프의 finally에서 Dispose
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
