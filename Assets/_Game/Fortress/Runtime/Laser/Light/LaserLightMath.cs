using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="LaserBeamLight"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class LaserLightMath
    {
        /// <summary>握りの強さ（太さ0〜1）から明るさの割合を求める。弱く握っても <paramref name="weakBrightness"/> までは光る。</summary>
        public static float Brightness(float thickness01, float weakBrightness)
        {
            return Mathf.Lerp(Mathf.Clamp01(weakBrightness), 1f, Mathf.Clamp01(thickness01));
        }

        /// <summary>
        /// 色の鮮やかさを変える。1でそのまま、1より大きいと濃く、0で灰色（明るさは保つ）。マイナスにならないよう切り詰める。
        /// </summary>
        public static Color Saturate(Color color, float saturation)
        {
            var luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            return new Color(
                Mathf.Max(0f, luma + (color.r - luma) * saturation),
                Mathf.Max(0f, luma + (color.g - luma) * saturation),
                Mathf.Max(0f, luma + (color.b - luma) * saturation),
                color.a);
        }
    }
}
