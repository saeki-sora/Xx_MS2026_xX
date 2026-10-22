using System;
using System.Diagnostics;
using System.Threading;
using MS2026.GripInputBridge.Transports;
using Debug = UnityEngine.Debug;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>LAN で握力を配る仕組みの今の様子（診断表示 F8 用）。</summary>
    public struct GripRelayStatus
    {
        /// <summary>このPCの前の人のセンサー番号（1〜4）。</summary>
        public int SensorNumber;

        public int ListenPort;

        /// <summary>受け取りを始められなかった理由（始められたら null）。</summary>
        public string ListenError;

        /// <summary>起動オプションの書き方の間違い（無ければ null）。</summary>
        public string OptionsError;

        /// <summary>このPCの前の人の握力を、今どこから取っているか。</summary>
        public GripRelayInputSource Source;

        /// <summary>このPCに Arduino がつながっていて、放送する立場か。</summary>
        public bool HasLocalDevice;

        /// <summary>テスト用にキーボードを放送している（-grip-relay-send）。</summary>
        public bool ForceSend;

        /// <summary>今、LAN へ放送しているか。</summary>
        public bool IsSending;

        public long PacketsSent;

        /// <summary>受け取りの様子。</summary>
        public GripRelayReceiverSnapshot Receiver;
    }

    /// <summary>
    /// 「1台の Arduino に4人分のセンサーをつなぎ、LAN で配る」使い方をまとめて動かす（起動オプション -grip-sensor N のときだけ作る）。
    /// <list type="bullet">
    /// <item>受け取り: どのPCも、自分のセンサー番号の UDP（<see cref="GripRelayOptions.ListenPort"/>）で放送を受け取る。</item>
    /// <item>放送: このPCに Arduino がつながっている間（<see cref="SetLocalDevice"/>）、4人分の値を LAN の全員へ配る（<c>-grip-relay-send</c> なら Arduino が無くてもキーボードの値を配る）。</item>
    /// <item>入力元: <see cref="Transport"/> を GripInputBridge に設定すると、このPCの P1 = 自分のセンサー番号の値（このPCの Arduino が先、無ければ放送）＋キーボードの Q。</item>
    /// </list>
    /// <see cref="Tick"/> を毎フレーム（メインスレッドで）呼び、使い終わったら <see cref="Dispose"/> する。
    /// </summary>
    public sealed class GripRelaySession : IDisposable, IGripRelaySampleSource
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private readonly GripRelayOptions _options;
        private readonly GripRelayReceiverState _state;
        private readonly UdpGripRelayReceiver _receiver;
        private readonly UdpGripRelaySender _sender;
        private readonly float[] _keyboard = new float[GripRelayPacket.SensorCount];
        private readonly float[] _keyboardCopy = new float[GripRelayPacket.SensorCount];
        private IGripRelayDevice _device;
        private string _warnedOtherSender;
        private bool _warnedWrongVersion;
        private bool _disposed;

        /// <param name="options">起動オプション（<see cref="GripRelayOptions.Enabled"/> であること）。</param>
        /// <param name="timeoutSeconds">この秒数放送が届かなければ「届いていない」とみなす。</param>
        /// <param name="sendRateHz">1秒に放送する回数。</param>
        public GripRelaySession(GripRelayOptions options, double timeoutSeconds, float sendRateHz)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (!options.Enabled)
            {
                throw new ArgumentException("センサー番号（-grip-sensor）が決まっていません。", nameof(options));
            }

            var senderId = NewSenderId();
            _state = new GripRelayReceiverState(senderId, timeoutSeconds, Now);
            _receiver = new UdpGripRelayReceiver(options.ListenPort, _state, Warn);
            _sender = new UdpGripRelaySender(this, senderId, Environment.MachineName, options.BasePort, sendRateHz, Warn);

            SensorTransport = new RelayedSensorTransport(options.SensorNumber - 1, _state);
            Transport = new CompositeGripTransport(SensorTransport, MS2026.GripInputBridge.GripInputBridge.Simulator, 1);

            Debug.Log($"[GripRelay] センサー{options.SensorNumber}番のPCとして動きます。LAN の握力の放送を UDP {options.ListenPort} で受け取り、" +
                      "このPCに Arduino がつながったら4人分を LAN へ放送します" + (options.ForceSend ? "（-grip-relay-send: Arduino が無くてもキーボードの値を放送します）。" : "。"));
            if (_receiver.ListenError != null)
            {
                Debug.LogError("[GripRelay] " + _receiver.ListenError + " このPCでは LAN の放送を受け取れません。");
            }
        }

        public GripRelayOptions Options => _options;

        /// <summary>このPCの前の人の握力（P1）だけを返す入力元（キーボードとは合成していない）。</summary>
        public RelayedSensorTransport SensorTransport { get; }

        /// <summary>GripInputBridge に設定する入力元（<see cref="SensorTransport"/>＋キーボード）。</summary>
        public CompositeGripTransport Transport { get; }

        /// <summary>このPCにつないだ Arduino（無くなったら null）。あれば放送し、自分の分もこちらから取る。メインスレッドから呼ぶ。</summary>
        public void SetLocalDevice(IGripRelayDevice device)
        {
            Volatile.Write(ref _device, device);
            SensorTransport.LocalDevice = device;
        }

        /// <summary>毎フレーム、メインスレッドから呼ぶ（キーボードの値を放送用に渡す・困ったことを知らせる）。</summary>
        public void Tick()
        {
            if (_disposed)
            {
                return;
            }

            var simulator = MS2026.GripInputBridge.GripInputBridge.Simulator;
            for (var i = 0; i < _keyboard.Length; i++)
            {
                Volatile.Write(ref _keyboard[i], simulator.GetRawValue(i));
            }

            _state.GetWarningInfo(out var senderName, out var otherSenderName, out var wrongVersionPackets);
            if (otherSenderName != null && otherSenderName != _warnedOtherSender)
            {
                _warnedOtherSender = otherSenderName;
                Debug.LogWarning($"[GripRelay] 握力の放送が2か所から届いています（使っている方: {senderName} ／ 使っていない方: {otherSenderName}）。" +
                                 "Arduino をつないだPCが2台ないか、-grip-relay-send を付けたPCがないか確かめてください。");
            }

            if (!_warnedWrongVersion && wrongVersionPackets > 0)
            {
                _warnedWrongVersion = true;
                Debug.LogWarning("[GripRelay] 形の違う握力の放送が届いています。全部のPCを同じビルドにしてください。");
            }
        }

        public GripRelayStatus GetStatus()
        {
            return new GripRelayStatus
            {
                SensorNumber = _options.SensorNumber,
                ListenPort = _options.ListenPort,
                ListenError = _receiver.ListenError,
                OptionsError = _options.Error,
                Source = SensorTransport.CurrentSource,
                HasLocalDevice = Volatile.Read(ref _device) != null,
                ForceSend = _options.ForceSend,
                IsSending = _sender.IsSending,
                PacketsSent = _sender.PacketsSent,
                Receiver = _state.GetSnapshot()
            };
        }

        /// <summary>放送する4人分の値を用意する（放送のスレッドから呼ばれる）。</summary>
        bool IGripRelaySampleSource.TryFill(GripRelayPacket packet)
        {
            // 放送のスレッドだけが使う控えに、メインスレッドが書いたキーボードの値を写す。
            for (var i = 0; i < _keyboardCopy.Length; i++)
            {
                _keyboardCopy[i] = Volatile.Read(ref _keyboard[i]);
            }

            return GripRelaySampleBuilder.Fill(packet, Volatile.Read(ref _device), _keyboardCopy, _options.ForceSend);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sender.Dispose();
            _receiver.Dispose();
        }

        private static double Now() => Clock.Elapsed.TotalSeconds;

        private static uint NewSenderId()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var id = BitConverter.ToUInt32(bytes, 0);
            return id == 0 ? 1u : id;
        }

        private static void Warn(string message)
        {
            Debug.LogWarning("[GripRelay] " + message);
        }
    }
}
