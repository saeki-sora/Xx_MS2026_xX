namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// シミュレータ用の波形プリセット。設計書 8.3 参照。
    /// キーボード操作の代わりに、企画書の「そっと／しっかり／渾身」の3段階や
    /// リズムスキル判定のテスト用パルスを自動再生できるようにする。
    /// </summary>
    public enum GripWaveformPreset
    {
        /// <summary>プリセットなし。通常どおりキーボード操作で値が決まる。</summary>
        None,

        /// <summary>そっと：0.2〜0.3を維持。</summary>
        Soft,

        /// <summary>しっかり：0.5〜0.6を維持。</summary>
        Firm,

        /// <summary>渾身：0.9〜1.0を維持し、オーバーヒートを誘発する。</summary>
        Full,

        /// <summary>リズムスキル判定のテスト用に、握る/離すを一定間隔で繰り返す。</summary>
        RhythmTestPulse
    }
}
