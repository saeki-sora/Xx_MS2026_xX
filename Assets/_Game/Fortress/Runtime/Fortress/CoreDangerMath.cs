using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="CoreDangerLook"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class CoreDangerMath
    {
        /// <summary>
        /// コアの残り HP（0〜1）から、危なさ（0〜1）を求める。<paramref name="startHealth01"/> 以上は0、HP 0 で1。
        /// 始めはじわっと、0に近いほど強くなる。
        /// </summary>
        public static float Danger(float health01, float startHealth01)
        {
            var start = Mathf.Max(0.0001f, startHealth01);
            var t = Mathf.Clamp01((start - Mathf.Clamp01(health01)) / start);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// ふちの鼓動の倍率（1〜1.6）。HP が <paramref name="pulseBelow"/> 以上、または鼓動の速さが0なら1。
        /// 1拍に「ドクン（強）・ドクン（弱）」の2回の山がある。
        /// </summary>
        public static float Pulse(float health01, float pulseBelow, float beatsPerSecond, float time)
        {
            if (pulseBelow <= 0f || beatsPerSecond <= 0f || health01 >= pulseBelow)
            {
                return 1f;
            }

            var phase = time * beatsPerSecond;
            phase -= Mathf.Floor(phase);
            var beat = Bump(phase, 0.08f) + 0.55f * Bump(phase, 0.3f);
            var closeness = Mathf.Clamp01((pulseBelow - health01) / pulseBelow);
            return 1f + 0.6f * beat * (0.4f + 0.6f * closeness);
        }

        // phase=center で1、±0.1 で0 になる山。
        private static float Bump(float phase, float center)
        {
            var d = Mathf.Abs(phase - center) / 0.1f;
            return d >= 1f ? 0f : 1f - d * d;
        }
    }
}
