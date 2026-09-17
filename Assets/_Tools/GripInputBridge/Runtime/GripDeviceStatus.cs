namespace MS2026.GripInputBridge
{
    /// <summary>
    /// プレイヤー1人分の握力入力デバイスの状態。
    /// 設計書 7.1 参照。
    /// </summary>
    public enum GripDeviceStatus
    {
        /// <summary>正常に接続され、値を受信できている。</summary>
        Connected,

        /// <summary>未接続、またはハートビートタイムアウトで切断とみなされた。</summary>
        Disconnected,

        /// <summary>接続はしているが、一定時間データが更新されていない（Phase 3 以降で使用）。</summary>
        Stale,

        /// <summary>値が異常に張り付いているなど、センサー故障が疑われる（Phase 3 以降で使用）。</summary>
        Suspicious
    }
}
