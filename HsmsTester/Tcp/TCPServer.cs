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
        private readonly List<TcpClient> _clients = new List<TcpClient>();

        public bool IsRunning => _listener != null;

        // 외부에서 리스트를 직접 수정하지 못하도록 스냅샷 반환
        public IReadOnlyList<TcpClient> ConnectedClients
        {
            get { lock (_clients) return _clients.ToList(); }
        }

        public event Action<TcpClient>? ClientConnected;
        public event Action<TcpClient>? ClientDisconnected;
        public event Action<TcpClient, byte[], int>? DataReceived;

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

                lock (_clients) _clients.Add(client);
                ClientConnected?.Invoke(client);

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
                    DataReceived?.Invoke(client, buffer, n);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }          // 연결 강제 종료
            catch (ObjectDisposedException) { }
            finally
            {
                bool removed;
                lock (_clients) removed = _clients.Remove(client);
                client.Dispose();
                if (removed) ClientDisconnected?.Invoke(client);
            }
        }

        public async Task SendAsync(TcpClient client, byte[] data)
        {
            if (client.Connected == false) return;
            try
            {
                await client.GetStream().WriteAsync(data, _cts?.Token ?? CancellationToken.None);
            }
            catch (IOException) { client.Close(); }   // 수신 루프가 정리하도록 소켓만 닫음
        }

        public Task BroadcastAsync(byte[] data)
        {
            return Task.WhenAll(ConnectedClients.Select(c => SendAsync(c, data)));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener = null;

            foreach (var client in ConnectedClients)
            {
                client.Close();
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
