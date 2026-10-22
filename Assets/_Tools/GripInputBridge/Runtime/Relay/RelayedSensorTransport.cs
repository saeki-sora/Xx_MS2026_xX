using System;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>「このPCの前の人の握力」をどこから取っているか。</summary>
    public enum GripRelayInputSource
    {
        /// <summary>どこからも届いていない（キーボードだけで動く）。</summary>
        None,

        /// <summary>このPCにつないだ Arduino から直接。</summary>
        LocalDevice,

        /// <summary>LAN の放送（Arduino をつないだ別のPC）から。</summary>
        Relay
    }

    /// <summary>
    /// 4人分のセンサーのうち、決めた1つ（<see cref="SensorIndex"/>）の値を「このPCの前の人（プレイヤー0 = P1）」の握力として返す入力元。
    /// このPCに Arduino がつながっていて、そのセンサーの値が届いていればそれを、無ければ LAN の放送の値を使う。
    /// P2〜P4（プレイヤー1〜3）は常に「つながっていない」（キーボードの Q 以外では動かない）。
    /// <c>CompositeGripTransport</c>（devicePlayerCount = 1）でキーボードと合成して使う。
    /// メインスレッドから使う（<see cref="LocalDevice"/> の差し替えもメインスレッドで）。
    /// </summary>
    public sealed class RelayedSensorTransport : IGripTransport
    {
        private readonly GripRelayReceiverState _relay;

#pragma warning disable CS0067 // 接続・切断は知らせない（使っている所が無い。状態は IsConnected で見る）
        public event Action<int> OnConnected;
        public event Action<int> OnDisconnected;
#pragma warning restore CS0067

        /// <param name="sensorIndex">このPCの前の人のセンサー（0〜3 = センサー1〜4）。</param>
        /// <param name="relay">LAN の放送を受け取った結果（受け取らないなら null）。</param>
        public RelayedSensorTransport(int sensorIndex, GripRelayReceiverState relay)
        {
            if (sensorIndex < 0 || sensorIndex >= GripRelayPacket.SensorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sensorIndex), "センサーは 0〜3 で指定してください。");
            }

            SensorIndex = sensorIndex;
            _relay = relay;
        }

        /// <summary>このPCの前の人のセンサー（0〜3）。</summary>
        public int SensorIndex { get; }

        /// <summary>このPCにつないだ Arduino（無ければ null）。あれば、届いている間はこちらを優先する。</summary>
        public IGripTransport LocalDevice { get; set; }

        /// <summary>今、どこから値を取っているか（診断表示用）。</summary>
        public GripRelayInputSource CurrentSource
        {
            get
            {
                if (LocalDevice != null && LocalDevice.IsConnected(SensorIndex))
                {
                    return GripRelayInputSource.LocalDevice;
                }

                return _relay != null && _relay.IsSensorConnected(SensorIndex) ? GripRelayInputSource.Relay : GripRelayInputSource.None;
            }
        }

        public bool IsConnected(int playerIndex)
        {
            return playerIndex == 0 && CurrentSource != GripRelayInputSource.None;
        }

        public float GetRawValue(int playerIndex)
        {
            if (playerIndex != 0)
            {
                return 0f;
            }

            var local = LocalDevice;
            if (local != null && local.IsConnected(SensorIndex))
            {
                return local.GetRawValue(SensorIndex);
            }

            return _relay != null && _relay.TryGetSensor(SensorIndex, out var raw, out _) ? raw : 0f;
        }
    }
}
