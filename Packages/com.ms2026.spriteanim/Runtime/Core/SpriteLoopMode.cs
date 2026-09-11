namespace MS2026.SpriteAnim
{
    /// <summary>アニメーション終端に達したときの挙動。</summary>
    public enum SpriteLoopMode
    {
        /// <summary>最後のフレームまで再生して停止する。</summary>
        Once,

        /// <summary>先頭に戻ってループし続ける。</summary>
        Loop,

        /// <summary>往復（1,2,3,2,1,2,3...）でループし続ける。</summary>
        PingPong,

        /// <summary>最後のフレームで静止したまま IsPlaying は true のまま維持する。</summary>
        ClampForever,
    }
}
