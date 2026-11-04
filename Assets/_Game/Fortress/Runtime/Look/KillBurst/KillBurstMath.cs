using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>撃破の破裂の、時間と形の計算（Unity の描画に頼らない部分。テストできるように分けてある）。</summary>
    public static class KillBurstMath
    {
        /// <summary>コマ打ち: 経過秒を 1/fps 秒ごとの段に切り下げる（fps が 60 以上ならそのまま）。</summary>
        public static float Quantize(float age, float framesPerSecond)
        {
            if (framesPerSecond >= 59.5f || framesPerSecond <= 0f)
            {
                return age;
            }

            return Mathf.Floor(age * framesPerSecond) / framesPerSecond;
        }

        /// <summary>しずくが中心から飛んだ距離（大きさ1のとき）。最初に勢いよく飛び、だんだん止まる。</summary>
        public static float DropDistance(float progress01)
        {
            var t = Mathf.Clamp01(progress01);
            return 0.25f + 1.1f * (1f - (1f - t) * (1f - t) * (1f - t));
        }

        /// <summary>白い星が出ている長さ（コマ数）。最初の2コマだけ。</summary>
        public const int StarFrames = 2;
    }
}
