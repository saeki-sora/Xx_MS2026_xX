using System;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>届いた放送を受け入れたか。</summary>
    public enum GripRelayAcceptResult
    {
        Accepted,

        /// <summary>自分のPCの放送（自分で送った物が戻ってきた）。</summary>
        OwnPacket,

        /// <summary>同じ放送が別の宛先から重なって届いた・前の物が遅れて届いた。</summary>
        OldOrDuplicate,

        /// <summary>別のPCの放送を使っているので、こちらは使わない（放送が2か所から来ている）。</summary>
        OtherSender
    }

    /// <summary>受け取りの様子（診断表示用。<see cref="GripRelayReceiverState.GetSnapshot"/>）。</summary>
    public struct GripRelayReceiverSnapshot
    {
        /// <summary>今使っている放送があるか（一度でも受け取ったか）。</summary>
        public bool HasSender;

        public string SenderName;
        public bool SenderIsTestSender;

        /// <summary>最後に放送を受け取ってからの秒数（受け取っていなければ +∞）。</summary>
        public double SecondsSinceLastPacket;

        /// <summary>放送が今も届いているか（<see cref="SecondsSinceLastPacket"/> が時間切れより短い）。</summary>
        public bool IsFresh;

        public long PacketsAccepted;

        /// <summary>使っていない別の放送の名前（届いていなければ null）と、最後に届いてからの秒数。</summary>
        public string OtherSenderName;
        public double SecondsSinceOtherSender;

        /// <summary>版が違う放送（ビルドが違うPC）を受け取った回数。</summary>
        public long WrongVersionPackets;

        /// <summary>最後に受け取った4人分の値（<see cref="IsFresh"/> が false なら古い）。</summary>
        public GripRelaySensorSample[] Sensors;
    }

    /// <summary>
    /// 握力の放送を受け取った結果を覚えておく（受信スレッドが書き、メインスレッドが読むので lock で守る。純粋・テストあり）。
    /// ・自分のPCの放送は使わない（<see cref="OwnSenderId"/>）。
    /// ・放送が2か所から来ていたら、先に使っていた方を使い続ける。ただし、使っている方がしばらく届かなくなった、
    ///   または使っている方の Arduino の値が1つも届いていないのに、もう一方は届いているなら、もう一方に切り替える
    ///   （Arduino を予備のPCに挿し替えたとき、自動で新しいPCの放送に移るため）。
    /// ・同じ放送は複数の宛先から重なって届くので、連番が前より大きい物だけを使う。
    /// </summary>
    public sealed class GripRelayReceiverState
    {
        private readonly object _gate = new object();
        private readonly Func<double> _clock;
        private readonly GripRelayPacket _latest = new GripRelayPacket();

        private bool _hasSender;
        private double _lastAcceptedAt = double.NegativeInfinity;
        private long _packetsAccepted;

        private uint _otherSenderId;
        private string _otherSenderName;
        private double _otherSenderAt = double.NegativeInfinity;
        private long _wrongVersionPackets;

        /// <param name="ownSenderId">このPCが放送するときの番号（自分の放送を使わないため。放送しないなら 0）。</param>
        /// <param name="timeoutSeconds">この秒数放送が届かなければ、値は「届いていない」とみなす。</param>
        /// <param name="clock">今の時刻（秒）。どのスレッドから呼んでもよい物（テストでは差し替える）。</param>
        public GripRelayReceiverState(uint ownSenderId, double timeoutSeconds, Func<double> clock)
        {
            if (timeoutSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "時間切れの秒数は 0 より大きくしてください。");
            }

            OwnSenderId = ownSenderId;
            TimeoutSeconds = timeoutSeconds;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public uint OwnSenderId { get; }

        public double TimeoutSeconds { get; }

        /// <summary>受け取った放送を覚える（受信スレッドから呼ぶ）。</summary>
        public GripRelayAcceptResult Accept(GripRelayPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (OwnSenderId != 0 && packet.SenderId == OwnSenderId)
            {
                return GripRelayAcceptResult.OwnPacket;
            }

            lock (_gate)
            {
                var now = _clock();
                if (_hasSender && packet.SenderId == _latest.SenderId)
                {
                    // 連番は1周しても比べられるよう、差を符号付きで見る。
                    if ((int)(packet.Sequence - _latest.Sequence) <= 0)
                    {
                        return GripRelayAcceptResult.OldOrDuplicate;
                    }
                }
                else if (_hasSender && !ShouldSwitchTo(packet, now))
                {
                    _otherSenderId = packet.SenderId;
                    _otherSenderName = packet.SenderName;
                    _otherSenderAt = now;
                    return GripRelayAcceptResult.OtherSender;
                }
                else if (_hasSender)
                {
                    // 切り替える。前に使っていた方を「別の放送」として覚えておく（まだ届いていれば、2か所から来ていると分かる）。
                    _otherSenderId = _latest.SenderId;
                    _otherSenderName = _latest.SenderName;
                    _otherSenderAt = _lastAcceptedAt;
                }

                if (_otherSenderId == packet.SenderId)
                {
                    _otherSenderId = 0;
                    _otherSenderName = null;
                    _otherSenderAt = double.NegativeInfinity;
                }

                _latest.CopyFrom(packet);
                _hasSender = true;
                _lastAcceptedAt = now;
                _packetsAccepted++;
                return GripRelayAcceptResult.Accepted;
            }
        }

        /// <summary>版が違う放送が届いたことを覚える（受信スレッドから呼ぶ）。</summary>
        public void NoteWrongVersion()
        {
            lock (_gate)
            {
                _wrongVersionPackets++;
            }
        }

        /// <summary>このセンサー(0-3)の値が今届いているか（放送が時間内に届いていて、そのセンサーの値もある）。</summary>
        public bool IsSensorConnected(int sensorIndex)
        {
            return TryGetSensor(sensorIndex, out _, out _);
        }

        /// <summary>このセンサー(0-3)の今の値。届いていなければ false（値は 0）。</summary>
        public bool TryGetSensor(int sensorIndex, out float raw, out float received)
        {
            raw = 0f;
            received = 0f;
            if (sensorIndex < 0 || sensorIndex >= GripRelayPacket.SensorCount)
            {
                return false;
            }

            lock (_gate)
            {
                if (!IsFreshLocked(_clock()) || !_latest.Sensors[sensorIndex].Connected)
                {
                    return false;
                }

                raw = _latest.Sensors[sensorIndex].Raw;
                received = _latest.Sensors[sensorIndex].Received;
                return true;
            }
        }

        /// <summary>
        /// 知らせが要ることだけを読む（毎フレーム呼んでもゴミを作らない。<see cref="GetSnapshot"/> は配列を作る）。
        /// otherSenderName = 使っていない別の放送が今も届いていればその名前（無ければ null）。
        /// </summary>
        public void GetWarningInfo(out string senderName, out string otherSenderName, out long wrongVersionPackets)
        {
            lock (_gate)
            {
                var now = _clock();
                senderName = _hasSender ? _latest.SenderName : null;
                otherSenderName = _otherSenderId != 0 && now - _otherSenderAt <= TimeoutSeconds ? _otherSenderName : null;
                wrongVersionPackets = _wrongVersionPackets;
            }
        }

        public GripRelayReceiverSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                var now = _clock();
                var otherFresh = _otherSenderId != 0 && now - _otherSenderAt <= TimeoutSeconds;
                return new GripRelayReceiverSnapshot
                {
                    HasSender = _hasSender,
                    SenderName = _hasSender ? _latest.SenderName : null,
                    SenderIsTestSender = _hasSender && _latest.ForcedTestSender,
                    SecondsSinceLastPacket = _hasSender ? Math.Max(0d, now - _lastAcceptedAt) : double.PositiveInfinity,
                    IsFresh = IsFreshLocked(now),
                    PacketsAccepted = _packetsAccepted,
                    OtherSenderName = otherFresh ? _otherSenderName : null,
                    SecondsSinceOtherSender = otherFresh ? Math.Max(0d, now - _otherSenderAt) : double.PositiveInfinity,
                    WrongVersionPackets = _wrongVersionPackets,
                    Sensors = (GripRelaySensorSample[])_latest.Sensors.Clone()
                };
            }
        }

        private bool ShouldSwitchTo(GripRelayPacket packet, double now)
        {
            if (!IsFreshLocked(now))
            {
                return true; // 使っていた放送が届かなくなった。
            }

            // 使っている方は Arduino の値が1つも無い（抜けた・まだ届かない）が、新しい方には届いている。
            return !_latest.HasConnectedSensor && packet.HasConnectedSensor;
        }

        private bool IsFreshLocked(double now)
        {
            return _hasSender && now - _lastAcceptedAt <= TimeoutSeconds;
        }
    }
}
