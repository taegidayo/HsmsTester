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
        private TcpClient _client = new TcpClient();

        public bool IsConnected => _client != null && _client.Connected;

        private readonly CancellationTokenSource _cts = new();
        public IPAddress? IP { get; private set; } = null;
        public int Port { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<byte[], int>? DataReceived;

        public TCPClient(string ip, int port)
        {
            _client = new TcpClient();

            if (IPAddress.TryParse(ip, out var address) == true)
            {
                IP = address;

                ConnectRequest();
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

            RunAsync(_cts.Token);
            return true;
        }

        private async Task RunAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested == true)
            {
                _cts.TryReset();
            }

            while (token.IsCancellationRequested == false)
            {
                try
                {
                    _client = new TcpClient();
                    if (IP == null) return;

                    // 연결을 기다리는 동안 스레드는 점유되지 않음
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    await _client.ConnectAsync(IP, Port, timeout.Token);

                    Connected?.Invoke();
                    await ReceiveLoopAsync(_client, token);   // 연결 유지하며 수신
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // 연결 타임아웃 → 재시도
                }
                catch (SocketException)
                {
                    // 연결 거부/끊김 → 재시도
                }
                finally
                {
                    bool wasConnected = _client.Connected;
                    _client.Dispose();
                    _client = null;
                    if (wasConnected) Disconnected?.Invoke();
                }

                if (!token.IsCancellationRequested)
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
            if (_client is { Connected: true })
                await _client.GetStream().WriteAsync(data, _cts.Token);
        }

        public void Disconnect()
        {
            _cts.Cancel();
            _client.Close();
            _client.Dispose();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
