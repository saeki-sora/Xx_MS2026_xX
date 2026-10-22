namespace MS2026.GripInputBridge.Relay
{
    /// <summary>
    /// 握力を LAN へ放送できる実機（今は <c>SerialGripTransport</c>）。
    /// 放送は別のスレッドから読むので、<b>すべてのメンバーをどのスレッドから呼んでも安全</b>にすること
    /// （Unity の API を使わない・値は Volatile などで受け渡す）。
    /// </summary>
    public interface IGripRelayDevice : IGripTransport
    {
        /// <summary>マイコンから届いたままの値(0-1、センサー特性の変換前)。診断表示用。</summary>
        float GetReceivedValue(int playerIndex);
    }
}
