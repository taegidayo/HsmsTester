using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.Tcp
{
    public class TCPServer : IDisposable
    {
        public IPAddress? IP { get; private set; } = null;
        public int Port { get; private set; } = 5000;

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new object();
        private TcpClient? _client;     // HSMS-SS : 연결은 항상 1개

        public bool IsRunning => _listener != null;

        public TcpClient? ConnectedClient
        {
            get { lock (_lock) return _client; }
        }

        public event Action? ClientConnected;
        public event Action? ClientDisconnected;
        public event Action< byte[], int>? DataReceived;

        public TCPServer(string ip, int port)
        {
            if (IPAddress.TryParse(ip, out IPAddress? ipAddress) == true)
            {
                IP = ipAddress;
            }

            Port = port;
            Start();
        }

        public bool SetTcpProperty(string ip, int port)
        {
            if (IPAddress.TryParse(ip, out var address) == false)
            {
                return false;
            }

            IP = address;
            Port = port;

            if (IsRunning) Start(); // 실행 중이면 새 설정으로 재시작
            return true;
        }

        public void Start()
        {
            Stop();

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IP ?? IPAddress.Any, Port);
            _listener.Start();

            _ = AcceptLoopAsync(_listener, _cts.Token);
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }   // Stop()으로 리스너 종료
                catch (SocketException) { continue; }        // 개별 accept 실패 → 계속 대기

                // 새 연결이 오면 기존 연결은 끊고 교체 (Host 재접속 시 남아있는 이전 소켓 정리)
                TcpClient? old;
                lock (_lock)
                {
                    old = _client;
                    _client = client;
                }
                old?.Close();   // 이전 수신 루프가 종료되며 ClientDisconnected 발생

                ClientConnected?.Invoke();

                _ = HandleClientAsync(client, token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];
                while (token.IsCancellationRequested == false)
                {
                    int n = await stream.ReadAsync(buffer, token);
                    if (n == 0) break;                               // 클라이언트가 연결 종료
                    DataReceived?.Invoke(buffer, n);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }          // 연결 강제 종료
            catch (ObjectDisposedException) { }
            finally
            {
                lock (_lock)
                {
                    if (_client == client) _client = null;
                }
                client.Dispose();
                ClientDisconnected?.Invoke();
            }
        }

        public async Task SendAsync(byte[] data)
        {
            var client = ConnectedClient;
            if (client is null || client.Connected == false) return;
            try
            {
                await client.GetStream().WriteAsync(data, _cts?.Token ?? CancellationToken.None);
            }
            catch (IOException) { client.Close(); }   // 수신 루프가 정리하도록 소켓만 닫음
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener = null;

            ConnectedClient?.Close();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
