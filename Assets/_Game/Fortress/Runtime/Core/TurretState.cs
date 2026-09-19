namespace MS2026.Fortress
{
    /// <summary>砲台の現在状態。企画書の「握るほど太く強く」「灼け落ちて沈黙する」に対応。</summary>
    public enum TurretState
    {
        /// <summary>握っていない。レーザーは出ていない。</summary>
        Idle,

        /// <summary>最大握力を保持して発射までチャージ中。まだレーザーは出ていない。</summary>
        Charging,

        /// <summary>チャージが完了し、レーザーを発射中。</summary>
        Firing,

        /// <summary>オーバーヒートして沈黙中。握っても反応しない。</summary>
        Overheated
    }
}
