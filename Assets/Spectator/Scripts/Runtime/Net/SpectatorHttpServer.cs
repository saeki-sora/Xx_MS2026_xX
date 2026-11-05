using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客ページを配る小さな Web サーバー（ゲームとは別のスレッドで動く。Unity に頼らない）。
    /// HttpListener は Windows で管理者の設定（netsh http add urlacl）が要るので、TcpListener で自分で読み書きする。
    /// 1本の線に1つのスレッド。スマホは 0.7 秒ごとに状態を聞くので、線はつないだまま使い回す（10 秒何も来なければ閉じる）。
    /// </summary>
    public sealed class SpectatorHttpServer : IDisposable
    {
        public delegate SpectatorResponse Handler(SpectatorRequest request);

        private const int IdleTimeoutMs = 10000;
        private const int SendTimeoutMs = 5000;
        private const int MaxRequestsPerConnection = 2000;

        private readonly Handler _handler;
        private readonly int _maxConnections;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private int _active;
        private readonly ConcurrentDictionary<TcpClient, byte> _clients = new ConcurrentDictionary<TcpClient, byte>();

        public SpectatorHttpServer(Handler handler, int maxConnections)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _maxConnections = Math.Max(1, maxConnections);
        }

        public bool IsRunning => _running;

        /// <summary>いまつながっている線の数。</summary>
        public int ActiveConnections => Volatile.Read(ref _active);

        /// <summary>実際に開いた番号（0 を渡したときは空いている番号が入る。テスト用）。</summary>
        public int Port { get; private set; }

        /// <summary>このPCのすべての住所で待ち受ける。開けなければ false と理由。</summary>
        public bool Start(int port, out string error)
        {
            error = null;
            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            }
            catch (SocketException e)
            {
                error = e.Message;
                _listener = null;
                return false;
            }

            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "Spectator HTTP accept" };
            _acceptThread.Start();
            return true;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        break;
                    }

                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (Interlocked.Increment(ref _active) > _maxConnections)
                {
                    Interlocked.Decrement(ref _active);
                    SafeClose(client);
                    continue;
                }

                _clients[client] = 0;
                var thread = new Thread(() => Serve(client)) { IsBackground = true, Name = "Spectator HTTP" };
                try
                {
                    thread.Start();
                }
                catch (OutOfMemoryException)
                {
                    _clients.TryRemove(client, out _);
                    Interlocked.Decrement(ref _active);
                    SafeClose(client);
                }
            }
        }

        private void Serve(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = IdleTimeoutMs;
                client.SendTimeout = SendTimeoutMs;
                using (var stream = client.GetStream())
                {
                    var reader = new SpectatorRequestReader(stream);
                    for (var i = 0; i < MaxRequestsPerConnection && _running; i++)
                    {
                        if (!reader.TryRead(out var request))
                        {
                            break;
                        }

                        SpectatorResponse response;
                        try
                        {
                            response = _handler(request);
                        }
                        catch (Exception)
                        {
                            response = SpectatorResponse.Empty(503);
                        }

                        var keepAlive = request.KeepAlive && _running;
                        var bytes = SpectatorHttp.BuildResponse(response, keepAlive);
                        stream.Write(bytes, 0, bytes.Length);
                        if (!keepAlive)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 線が切れた・時間切れ。黙って閉じる（スマホがつなぎ直す）。
            }
            finally
            {
                _clients.TryRemove(client, out _);
                SafeClose(client);
                Interlocked.Decrement(ref _active);
            }
        }

        private static void SafeClose(TcpClient client)
        {
            try
            {
                client.Close();
            }
            catch (Exception)
            {
                // 閉じるときの失敗は気にしない
            }
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch (Exception)
            {
                // 止めるときの失敗は気にしない
            }

            _listener = null;

            // 待っている線も閉じる（Play を止めたあとに線が残らないように）。
            foreach (var client in _clients.Keys)
            {
                SafeClose(client);
            }

            _clients.Clear();
        }
    }
}
