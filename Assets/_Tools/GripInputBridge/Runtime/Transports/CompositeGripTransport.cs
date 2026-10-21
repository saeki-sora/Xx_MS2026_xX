using System;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機(device)とキーボードシミュレータ(fallback)を合成する入力元。プレイヤーごとに、
    /// 実機が繋がっていれば「実機とキーボードの大きい方」、繋がっていなければキーボードの値を返す。
    /// 実機を挿したままでもキーボードでデバッグでき、実機が無い・抜けたときも自動でキーボードで動く。
    /// 実機側の破棄(Dispose)はこのクラスでは行わない(作った側が責任を持つ)。
    /// 実機の値を使うのは先頭の <see cref="DevicePlayerCount"/> 人(P1から)だけで、それより後のプレイヤーはキーボードだけで動く
    /// (1人プレイなのにマイコンが複数の値を送ってきて、P2以降のレーザーまで出るのを防ぐため)。
    /// </summary>
    public sealed class CompositeGripTransport : IGripTransport
    {
        public IGripTransport Device { get; }
        public IGripTransport Fallback { get; }

        /// <summary>実機の値を使うプレイヤー数(P1から数える、0-4)。これより後のプレイヤーは実機を見ない。</summary>
        public int DevicePlayerCount { get; }

        public event Action<int> OnConnected;
        public event Action<int> OnDisconnected;

        public CompositeGripTransport(IGripTransport device, IGripTransport fallback,
            int devicePlayerCount = GripInputBridgeConstants.PlayerCount)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            DevicePlayerCount = devicePlayerCount < 0 ? 0
                : devicePlayerCount > GripInputBridgeConstants.PlayerCount ? GripInputBridgeConstants.PlayerCount
                : devicePlayerCount;

            // 接続/切断の通知は実機側だけを転送する(キーボードは常に繋がっている扱いのため)。実機を使わないプレイヤーの分は送らない。
            Device.OnConnected += playerIndex =>
            {
                if (UsesDevice(playerIndex))
                {
                    OnConnected?.Invoke(playerIndex);
                }
            };
            Device.OnDisconnected += playerIndex =>
            {
                if (UsesDevice(playerIndex))
                {
                    OnDisconnected?.Invoke(playerIndex);
                }
            };
        }

        /// <summary>このプレイヤーに実機の値を使うか(<see cref="DevicePlayerCount"/> の範囲内か)。</summary>
        public bool UsesDevice(int playerIndex) => playerIndex >= 0 && playerIndex < DevicePlayerCount;

        /// <summary>このプレイヤーの実機が繋がっているか(診断表示用)。実機を使わないプレイヤーは常に false。</summary>
        public bool IsDeviceConnected(int playerIndex) => UsesDevice(playerIndex) && Device.IsConnected(playerIndex);

        public bool IsConnected(int playerIndex)
        {
            return IsDeviceConnected(playerIndex) || Fallback.IsConnected(playerIndex);
        }

        public float GetRawValue(int playerIndex)
        {
            // キーボード側は毎フレームの更新(押下中の上昇など)を進めるため、実機が繋がっていても必ず読む。
            var fallbackValue = Fallback.IsConnected(playerIndex) ? Fallback.GetRawValue(playerIndex) : 0f;
            if (!IsDeviceConnected(playerIndex))
            {
                return fallbackValue;
            }

            var deviceValue = Device.GetRawValue(playerIndex);
            return deviceValue > fallbackValue ? deviceValue : fallbackValue;
        }
    }
}
