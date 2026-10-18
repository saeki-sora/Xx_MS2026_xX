using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="LaserBeamLight"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class LaserLightMath
    {
        /// <summary>1本のビームに置く光の最大数。</summary>
        public const int MaxLightsPerBeam = 8;

        /// <summary>握りの強さ（太さ0〜1）から明るさの割合を求める。弱く握っても <paramref name="weakBrightness"/> までは光る。</summary>
        public static float Brightness(float thickness01, float weakBrightness)
        {
            return Mathf.Lerp(Mathf.Clamp01(weakBrightness), 1f, Mathf.Clamp01(thickness01));
        }

        /// <summary>長さ <paramref name="length"/> のビームに、間隔 <paramref name="spacing"/> で置く光の数（両端を含む。1〜最大数）。</summary>
        public static int LightCount(float length, float spacing)
        {
            if (length <= 0.001f)
            {
                return 1;
            }

            var count = Mathf.CeilToInt(length / Mathf.Max(0.1f, spacing)) + 1;
            return Mathf.Clamp(count, 1, MaxLightsPerBeam);
        }

        /// <summary>i 番目の光を置く位置（ビームの始点0〜終点1の割合）。光が1個なら真ん中。</summary>
        public static float LightPosition01(int index, int count)
        {
            return count <= 1 ? 0.5f : index / (float)(count - 1);
        }
    }
}
