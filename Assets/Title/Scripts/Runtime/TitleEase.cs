using UnityEngine;

namespace MS2026.Title
{
    /// <summary>動きの「緩急」の種類。</summary>
    public enum TitleEaseType
    {
        [InspectorName("一定の速さ")] Linear,
        [InspectorName("最後にゆっくり")] OutCubic,
        [InspectorName("少し行き過ぎて戻る")] OutBack,
        [InspectorName("弾む")] OutBounce,
        [InspectorName("ゆっくり始まりゆっくり終わる")] InOutSine,
        [InspectorName("だんだん速く")] InCubic,
    }

    public static class TitleEase
    {
        public static float Evaluate(TitleEaseType type, float t)
        {
            t = Mathf.Clamp01(t);
            switch (type)
            {
                case TitleEaseType.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case TitleEaseType.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    var u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case TitleEaseType.OutBounce:
                    return OutBounce(t);
                case TitleEaseType.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case TitleEaseType.InCubic:
                    return t * t * t;
                default:
                    return t;
            }
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
