using System;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機(device)とキーボードシミュレータ(fallback)を合成する入力元。プレイヤーごとに、
    /// 実機が繋がっていれば「実機とキーボードの大きい方」、繋がっていなければキーボードの値を返す。
    /// 実機を挿したままでもキーボードでデバッグでき、実機が無い・抜けたときも自動でキーボードで動く。
    /// 実機側の破棄(Dispose)はこのクラスでは行わない(作った側が責任を持つ)。
    /// </summary>
    public sealed class CompositeGripTransport : IGripTransport
    {
        public IGripTransport Device { get; }
        public IGripTransport Fallback { get; }

        public event Action<int> OnConnected;
        public event Action<int> OnDisconnected;

        public CompositeGripTransport(IGripTransport device, IGripTransport fallback)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

            // 接続/切断の通知は実機側だけを転送する(キーボードは常に繋がっている扱いのため)。
            Device.OnConnected += playerIndex => OnConnected?.Invoke(playerIndex);
            Device.OnDisconnected += playerIndex => OnDisconnected?.Invoke(playerIndex);
        }

        /// <summary>このプレイヤーの実機が繋がっているか(診断表示用)。</summary>
        public bool IsDeviceConnected(int playerIndex) => Device.IsConnected(playerIndex);

        public bool IsConnected(int playerIndex)
        {
            return Device.IsConnected(playerIndex) || Fallback.IsConnected(playerIndex);
        }

        public float GetRawValue(int playerIndex)
        {
            // キーボード側は毎フレームの更新(押下中の上昇など)を進めるため、実機が繋がっていても必ず読む。
            var fallbackValue = Fallback.IsConnected(playerIndex) ? Fallback.GetRawValue(playerIndex) : 0f;
            if (!Device.IsConnected(playerIndex))
            {
                return fallbackValue;
            }

            var deviceValue = Device.GetRawValue(playerIndex);
            return deviceValue > fallbackValue ? deviceValue : fallbackValue;
        }
    }
}
