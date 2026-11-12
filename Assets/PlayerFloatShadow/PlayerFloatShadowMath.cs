using UnityEngine;

namespace MS2026.PlayerFloatShadow
{
    /// <summary>浮いた影の計算（Unity の状態を読まない）。</summary>
    public static class PlayerFloatShadowMath
    {
        /// <summary>今の高さ。time は秒、phase はプレイヤーごとのずらし（0〜1）。</summary>
        public static float Height(float time, float hover, float bob, float seconds, float phase)
        {
            var period = Mathf.Max(0.01f, seconds);
            return Mathf.Max(0f, hover + bob * Mathf.Sin((time / period + phase) * Mathf.PI * 2f));
        }

        /// <summary>
        /// 光の進む向き（ワールド）から、床の上で影がずれる量を求める。
        /// 床は XY で、光が床へ向かうとき z が +。太陽が真上で向きが決まらないときは fallback の向きを使う。
        /// </summary>
        public static Vector2 Offset(Vector3 lightForward, float height, Vector2 fallback, float minimum, float maximum)
        {
            var flat = new Vector2(lightForward.x, lightForward.y);
            var down = lightForward.z;
            Vector2 direction;
            float distance;
            if (down > 0.001f && flat.sqrMagnitude > 0.0001f)
            {
                direction = flat.normalized;
                distance = height * flat.magnitude / down;
            }
            else
            {
                direction = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.down;
                distance = height;
            }

            return direction * Mathf.Clamp(distance, minimum, Mathf.Max(minimum, maximum));
        }

        /// <summary>高さの割合 high（0=一番低い、1=一番高い）で広がる倍率。</summary>
        public static float Spread(float high, float amount) => 1f + Mathf.Clamp01(high) * amount;

        /// <summary>高さの割合 high で薄くなる倍率。</summary>
        public static float Fade(float high, float amount) => 1f - Mathf.Clamp01(high) * amount;
    }
}
