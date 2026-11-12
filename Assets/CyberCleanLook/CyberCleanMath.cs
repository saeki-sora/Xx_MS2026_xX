using UnityEngine;

namespace MS2026.CyberCleanLook
{
    /// <summary>画面演出の動きの計算（Unity の状態を読まない）。</summary>
    public static class CyberCleanMath
    {
        /// <summary>泡が1粒ふくらむのにかける割合（覆う段階の長さに対して）。</summary>
        public const float BubbleGrow = 0.3f;

        /// <summary>泡が1粒はじけるのにかける割合（はじける段階の長さに対して）。</summary>
        public const float BubblePop = 0.2f;

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            var rest = 1f - t;
            return 1f - rest * rest * rest;
        }

        /// <summary>少し行き過ぎて戻る（ポンと出る）。</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.7f;
            var u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        /// <summary>0→1→0 の山。</summary>
        public static float Bump(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI);
        }

        /// <summary>
        /// 覆う段階（t01 は 0〜1）での泡の大きさ。左の泡から順にふくらみ、t01=1 で全部が出そろう。
        /// x01 は画面の左端0〜右端1、jitter は泡ごとのばらつき（0〜1）。glossy は上に浮かぶ大きな泡（少し遅れて出る）。
        /// </summary>
        public static float CoverScale(float x01, float jitter, bool glossy, float t01)
        {
            var start = glossy ? 0.3f + jitter * 0.4f : Mathf.Clamp01(x01) * 0.6f + jitter * 0.1f;
            return t01 < start ? 0f : EaseOutBack((t01 - start) / BubbleGrow);
        }

        /// <summary>
        /// はじける段階（t01 は 0〜1）で、この泡がはじけ始める時。大きな泡が先にはじけ、小さな泡は左から順に、ばらつきながらはじける。
        /// t01=1 までに全部はじけ終わる。
        /// </summary>
        public static float PopStart(float x01, float jitter, bool glossy)
        {
            return glossy ? jitter * 0.35f : Mathf.Clamp01(x01) * 0.55f + jitter * 0.25f;
        }

        /// <summary>はじける途中（u は 0〜1）の泡の大きさの倍率。少しふくらんでから消える。</summary>
        public static float PopScale(float u) => 1f + 0.18f * Mathf.Clamp01(u);

        /// <summary>はじける途中（u は 0〜1）の泡の濃さ。</summary>
        public static float PopAlpha(float u)
        {
            u = Mathf.Clamp01(u);
            return 1f - u * u;
        }

        /// <summary>ぷるぷる揺れる、横と縦の倍率（体積が同じに見えるよう、横に伸びると縦が縮む）。</summary>
        public static Vector2 Wobble(float time, float phase, float amount)
        {
            var w = Mathf.Sin(time * 7f + phase) * amount;
            return new Vector2(1f + w, 1f - w);
        }
    }
}
