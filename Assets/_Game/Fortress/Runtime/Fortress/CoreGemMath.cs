using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="CoreGemLook"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class CoreGemMath
    {
        /// <summary>HP（0〜1）から、脈の速さ・赤さに使う「傷み具合」（0〜1）。HP が減るほど大きく、後半ほど急に強くなる。</summary>
        public static float Damage(float health01)
        {
            var d = 1f - Mathf.Clamp01(health01);
            return d * (0.5f + 0.5f * d);
        }

        /// <summary>HP（0〜1）から、ひびの広がり（0〜1）。<paramref name="crackStartHealth01"/> 以上は0、HP 0 で1。</summary>
        public static float CrackAmount(float health01, float crackStartHealth01)
        {
            if (crackStartHealth01 <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((crackStartHealth01 - Mathf.Clamp01(health01)) / crackStartHealth01);
        }

        /// <summary>ダメージを受けてからの白い光の強さ（1→0）。</summary>
        public static float HitFlash(float secondsSinceHit, float flashSeconds)
        {
            if (flashSeconds <= 0f || secondsSinceHit < 0f || secondsSinceHit >= flashSeconds)
            {
                return 0f;
            }

            var t = 1f - secondsSinceHit / flashSeconds;
            return t * t;
        }
    }
}
