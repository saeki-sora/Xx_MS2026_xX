using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>
    /// 握力の放送の受け取り係（Unity を使わない）。決まった UDP の番号（センサーごとに別）で待ち、届いた放送を
    /// <see cref="GripRelayReceiverState"/> に渡す。専用のスレッドで受け取るので、ゲームの画面の重さに左右されない。
    /// 番号はほかのゲームと共有しない（同じPCで同じセンサー番号のゲームを2つ起動したら、2つ目は受け取れずに知らせる）。
    /// </summary>
    public sealed class UdpGripRelayReceiver : IDisposable
    {
        private readonly GripRelayReceiverState _state;
        private readonly Action<string> _warn;
        private readonly Socket _socket;
        private readonly Thread _thread;
        private volatile bool _running;
        private long _packetsReceived;
        private long _foreignPackets;
        private bool _warnedReceiveError;

        /// <param name="port">待つ UDP の番号。</param>
        /// <param name="state">届いた放送を覚える物。</param>
        /// <param name="warn">困ったときの知らせ（別スレッドから呼ぶことがある）。</param>
        /// <param name="bindAddress">待つアドレス（null = すべてのネットワーク。テストでは 127.0.0.1）。</param>
        public UdpGripRelayReceiver(int port, GripRelayReceiverState state, Action<string> warn, IPAddress bindAddress = null)
        {
            Port = port;
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _warn = warn ?? (_ => { });

            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { ReceiveTimeout = 250 };
                try
                {
                    // ほかのソフトが同じ番号を横取りできないようにする（使えない環境でも受け取りは続ける）。
                    socket.ExclusiveAddressUse = true;
                }
                catch (Exception)
                {
                    // 使えない環境。同じ番号の2つ目は Bind で失敗するので、困ることは無い。
                }

                GripRelayNetwork.DisableConnectionReset(socket);
                socket.Bind(new IPEndPoint(bindAddress ?? IPAddress.Any, port));
                _socket = socket;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse || e.SocketErrorCode == SocketError.AccessDenied)
            {
                ListenError = $"UDP {port} を使えません。このPCで同じセンサー番号のゲームがもう動いているか、ほかのソフトがこの番号を使っています（{e.SocketErrorCode}）。";
                return;
            }
            catch (Exception e)
            {
                ListenError = $"UDP {port} で受け取りを始められませんでした: {e.Message}";
                return;
            }

            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "GripInputBridge.RelayReceive" };
            _thread.Start();
        }

        public int Port { get; }

        /// <summary>受け取りを始められたか。</summary>
        public bool IsListening => _socket != null;

        /// <summary>受け取りを始められなかった理由（始められたら null）。</summary>
        public string ListenError { get; }

        /// <summary>この番号に届いた物の数（このゲームの放送かどうかによらない）。</summary>
        public long PacketsReceived => Interlocked.Read(ref _packetsReceived);

        /// <summary>このゲームの放送ではない物・壊れた物の数。</summary>
        public long ForeignPackets => Interlocked.Read(ref _foreignPackets);

        public void Dispose()
        {
            _running = false;
            try
            {
                _socket?.Close(); // 待っている受信を終わらせる。
            }
            catch (Exception)
            {
                // 片付け中なので無視してよい。
            }

            try
            {
                _thread?.Join(500);
            }
            catch (ThreadStateException)
            {
                // 始まっていない。
            }
        }

        private void ReceiveLoop()
        {
            var buffer = new byte[512];
            var packet = new GripRelayPacket();
            while (_running)
            {
                int length;
                try
                {
                    // 送り主のアドレスは使わない（放送の中の番号で見分ける）。ReceiveFrom は受け取るたびにアドレスの物を作るので使わない。
                    length = _socket.Receive(buffer);
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.ConnectionReset ||
                                                e.SocketErrorCode == SocketError.MessageSize)
                {
                    continue; // 届いていないだけ／相手のいない宛先の知らせ（Windows）／大きすぎる物（この放送ではない）。
                }
                catch (ObjectDisposedException)
                {
                    break; // 片付け中。
                }
                catch (Exception e)
                {
                    if (!_running)
                    {
                        break;
                    }

                    if (!_warnedReceiveError)
                    {
                        _warnedReceiveError = true;
                        _warn($"握力の放送の受け取りでエラーが起きました（受け取りは続けます）: {e.Message}");
                    }

                    Thread.Sleep(50);
                    continue;
                }

                Interlocked.Increment(ref _packetsReceived);
                switch (GripRelayCodec.TryRead(buffer, length, packet))
                {
                    case GripRelayReadResult.Ok:
                        _state.Accept(packet);
                        break;
                    case GripRelayReadResult.WrongVersion:
                        _state.NoteWrongVersion();
                        break;
                    default:
                        Interlocked.Increment(ref _foreignPackets);
                        break;
                }
            }
        }
    }
}
