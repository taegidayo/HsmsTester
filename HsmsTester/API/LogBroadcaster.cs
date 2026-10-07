using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HsmsTester.API
{
    internal class LogBroadcaster : IDisposable
    {
        public static LogBroadcaster Instance { get; } = new LogBroadcaster();
        private LogBroadcaster() 
        { 
        
            LibraryController.Instance.RegisterDisposable(this);
        }

        private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
        private readonly ConcurrentQueue<string> _history = new();
        private const int HistorySize = 100;

        // 라이브러리 어디서든 호출: LogBroadcaster.Write("작업 시작");
        public void Write(string message)
        {
            var line = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] {message}";

            _history.Enqueue(line);
            while (_history.Count > HistorySize) _history.TryDequeue(out _);

            foreach (var ch in _clients.Values)
                ch.Writer.TryWrite(line);
        }

        internal (Guid Id, ChannelReader<string> Reader, string[] History) Subscribe()
        {
            var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest   // 느린 클라이언트 때문에 서버가 막히지 않게
            });
            var id = Guid.NewGuid();
            _clients[id] = ch;
            return (id, ch.Reader, _history.ToArray());
        }

        internal void Unsubscribe(Guid id)
        {
            if (_clients.TryRemove(id, out var ch)) ch.Writer.TryComplete();
        }

        public void Dispose()
        {
            foreach (var client in _clients.ToList())
            {
                _clients.TryRemove(client.Key, out var ch);

                client.Value.Writer.TryComplete();
            }
        }
    }
}