using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>放送する値を用意する物。別のスレッドから呼ぶので、どのスレッドから呼んでも安全にすること。</summary>
    public interface IGripRelaySampleSource
    {
        /// <summary>4人分の値と ForcedTestSender を packet に入れる。今は放送しない（Arduino が無い等）なら false。</summary>
        bool TryFill(GripRelayPacket packet);
    }

    /// <summary>
    /// 握力の放送係（Unity を使わない）。専用のスレッドで1秒に <c>rateHz</c> 回、4人分の値を LAN へ送る。
    /// 放送はゲームの画面の重さ（ホストのPCは一番重い）に左右されないよう、メインスレッドとは別に送る。
    /// 宛先は2秒ごとに作り直す（ゲームを起動した後に LAN のケーブルを挿しても届くように）。
    /// </summary>
    public sealed class UdpGripRelaySender : IDisposable
    {
        private const double TargetRefreshSeconds = 2d;

        private readonly IGripRelaySampleSource _source;
        private readonly uint _senderId;
        private readonly string _senderName;
        private readonly int _basePort;
        private readonly double _intervalSeconds;
        private readonly Action<string> _warn;
        private readonly Func<int, IReadOnlyList<IPEndPoint>> _buildTargets;
        private readonly Socket _socket;
        private readonly Thread _thread;
        private volatile bool _running;
        private volatile bool _isSending;
        private long _packetsSent;
        private bool _warnedSendError;

        /// <param name="source">放送する値（別スレッドから呼ぶ）。</param>
        /// <param name="senderId">このゲームの放送の番号（0 以外）。</param>
        /// <param name="senderName">このPCの名前（診断表示用）。</param>
        /// <param name="basePort">センサー1を受け取る UDP の番号。</param>
        /// <param name="rateHz">1秒に送る回数。</param>
        /// <param name="warn">困ったときの知らせ（別スレッドから呼ぶ）。</param>
        /// <param name="buildTargets">宛先の作り方（テスト用に差し替える。null なら <see cref="GripRelayNetwork.BuildTargets"/>）。</param>
        public UdpGripRelaySender(IGripRelaySampleSource source, uint senderId, string senderName, int basePort, float rateHz,
            Action<string> warn, Func<int, IReadOnlyList<IPEndPoint>> buildTargets = null)
        {
            if (senderId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(senderId), "放送の番号は 0 以外にしてください。");
            }

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _senderId = senderId;
            _senderName = GripRelayCodec.TruncateName(senderName);
            _basePort = basePort;
            _intervalSeconds = 1d / Math.Max(1f, rateHz);
            _warn = warn ?? (_ => { });
            _buildTargets = buildTargets ?? (port => GripRelayNetwork.BuildTargets(port));

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
            GripRelayNetwork.DisableConnectionReset(_socket);
            _running = true;
            _thread = new Thread(SendLoop) { IsBackground = true, Name = "GripInputBridge.RelaySend" };
            _thread.Start();
        }

        /// <summary>今、放送しているか（値を用意する物が「放送しない」と言っていれば false）。</summary>
        public bool IsSending => _isSending;

        /// <summary>これまでに送った回数（宛先の数ではなく、放送の回数）。</summary>
        public long PacketsSent => Interlocked.Read(ref _packetsSent);

        public void Dispose()
        {
            _running = false;
            try
            {
                _thread.Join(500);
            }
            catch (ThreadStateException)
            {
                // 始まっていない。
            }

            _socket.Close();
        }

        private void SendLoop()
        {
            var packet = new GripRelayPacket();
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var clock = Stopwatch.StartNew();
            IReadOnlyList<IPEndPoint> targets = Array.Empty<IPEndPoint>();
            var nextTargets = 0d;
            var nextSend = 0d;
            uint sequence = 0;

            while (_running)
            {
                var now = clock.Elapsed.TotalSeconds;
                if (now < nextSend)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // 予定の時刻を基準に進める（Windows の Sleep は 1〜16ms ずれるので、毎回「今」から数えると回数が半分に落ちる）。
                // 大きく遅れたとき（PC が止まっていた等）は、まとめて送らずに今から数え直す。
                nextSend += _intervalSeconds;
                if (nextSend < now)
                {
                    nextSend = now + _intervalSeconds;
                }

                try
                {
                    if (now >= nextTargets)
                    {
                        nextTargets = now + TargetRefreshSeconds;
                        targets = _buildTargets(_basePort) ?? (IReadOnlyList<IPEndPoint>)Array.Empty<IPEndPoint>();
                    }

                    if (!_source.TryFill(packet))
                    {
                        _isSending = false;
                        continue;
                    }

                    packet.SenderId = _senderId;
                    packet.SenderName = _senderName;
                    packet.Sequence = ++sequence;
                    var length = GripRelayCodec.Write(packet, buffer);
                    for (var i = 0; i < targets.Count; i++)
                    {
                        try
                        {
                            _socket.SendTo(buffer, 0, length, SocketFlags.None, targets[i]);
                        }
                        catch (SocketException e)
                        {
                            // そのネットワークには送れない（ケーブルが抜けている・無効なアダプタ等）。ほかの宛先は続ける。
                            if (!_warnedSendError && e.SocketErrorCode != SocketError.NetworkUnreachable && e.SocketErrorCode != SocketError.HostUnreachable &&
                                e.SocketErrorCode != SocketError.AddressNotAvailable)
                            {
                                _warnedSendError = true;
                                _warn($"握力の放送を {targets[i]} へ送れませんでした（{e.SocketErrorCode}）。ほかの宛先へは送り続けます。");
                            }
                        }
                    }

                    _isSending = true;
                    Interlocked.Increment(ref _packetsSent);
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

                    if (!_warnedSendError)
                    {
                        _warnedSendError = true;
                        _warn($"握力の放送でエラーが起きました（放送は続けます）: {e.Message}");
                    }

                    Thread.Sleep(50);
                }
            }

            _isSending = false;
        }
    }
}
