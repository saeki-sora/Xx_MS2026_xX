using System;

namespace MS2026.SpriteAnim
{
    /// <summary>特定のアニメーション間で切り替わるときのクロスフェード時間を上書きするルール。from/to に "*" を指定すると任意にマッチする。</summary>
    [Serializable]
    public struct TransitionRule
    {
        public string from;
        public string to;
        public float crossFadeSeconds;
    }
}
