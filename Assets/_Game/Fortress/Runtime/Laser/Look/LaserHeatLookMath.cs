using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary><see cref="LaserHeatLookSettings"/> の計算部分（Unityの部品に触らない純粋な計算。テストあり）。</summary>
    public static class LaserHeatLookMath
    {
        /// <summary>
        /// 点滅の明るさ（1＝いつもの明るさ、小さいほど暗い）。熱が <paramref name="warnAt"/> 未満なら点滅しない。
        /// 速さは危ない所で start、オーバーヒート直前で end（回/秒）。<paramref name="phase"/> は <see cref="AdvancePhase"/> で進めた値。
        /// </summary>
        public static float Blink(float phase, float heat01, float warnAt, float depth)
        {
            if (!IsWarning(heat01, warnAt) || depth <= 0f)
            {
                return 1f;
            }

            // 0〜1 の三角波（なめらかにするため 0.5 - 0.5cos）。1 のとき一番暗い。
            var wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            return 1f - Mathf.Clamp01(depth) * wave;
        }

        /// <summary>
        /// 点滅の位相を進める。速さが途中で変わっても位相が飛ばないよう、時間×速さではなく足し算で進める。
        /// 危ない所の下では 0 に戻す（次に越えたとき、明るい所から点滅が始まる）。
        /// </summary>
        public static float AdvancePhase(float phase, float deltaTime, float heat01, float warnAt, float perSecondStart, float perSecondEnd)
        {
            if (!IsWarning(heat01, warnAt))
            {
                return 0f;
            }

            var t = warnAt >= 1f ? 1f : Mathf.Clamp01((heat01 - warnAt) / (1f - warnAt));
            var rate = Mathf.Lerp(perSecondStart, perSecondEnd, t);
            return Mathf.Repeat(phase + Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime), 1f);
        }

        public static bool IsWarning(float heat01, float warnAt) => warnAt < 1f && heat01 >= warnAt;

        /// <summary>芯の太さに足す量（ビームの幅に対する割合）。熱 0 で 0、オーバーヒートで <paramref name="widenAtFull"/>。</summary>
        public static float CoreWiden(float heat01, float widenAtFull) => Mathf.Clamp01(heat01) * Mathf.Max(0f, widenAtFull);

        /// <summary>
        /// 輪に描く割合（0〜1）。撃てるときは熱の量、オーバーヒート中は「止まっている残り時間」の割合。
        /// </summary>
        public static float RingFill(bool overheated, float heat01, float silenceRemaining, float silenceDuration)
        {
            if (overheated)
            {
                return silenceDuration > 0f ? Mathf.Clamp01(silenceRemaining / silenceDuration) : 0f;
            }

            return Mathf.Clamp01(heat01);
        }

        /// <summary>輪を見せたいか。オーバーヒート中はいつも見せる。それ以外は熱が <paramref name="showFrom"/> 以上のとき。</summary>
        public static bool RingWanted(bool overheated, float heat01, float showFrom) => overheated || heat01 >= Mathf.Max(0.0001f, showFrom);
    }
}
