using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>光る泡（<see cref="FoamPopLook"/>）の、時間・大きさ・数の計算（Unity の描画に頼らない部分。テストできるように分けてある）。</summary>
    public static class FoamPopMath
    {
        /// <summary>混み具合を数える時間の長さ（秒）。この時間で数が約 1/3 に減っていく。</summary>
        public const float CrowdDecaySeconds = 0.15f;

        /// <summary>泡がふくらむ秒数（少し大きくなりすぎてから戻る）。</summary>
        public const float GrowSeconds = 0.05f;

        /// <summary>縮み終わったときの泡の大きさ（元の何倍か）。ここで光の点に変わる。</summary>
        public const float ShrinkTo = 0.4f;

        /// <summary>まわりの混み具合 <paramref name="crowd"/> から、1体あたりの泡の数を決める（空いていれば bubbleCount、混むほど crowdedBubbleCount へ）。</summary>
        public static int BubbleCount(float crowd, int bubbleCount, int crowdedBubbleCount, float crowdStart, float crowdFull)
        {
            float t;
            if (crowdFull > crowdStart)
            {
                t = Mathf.Clamp01((crowd - crowdStart) / (crowdFull - crowdStart));
            }
            else
            {
                t = crowd >= crowdStart ? 1f : 0f;
            }

            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(bubbleCount, crowdedBubbleCount, t)));
        }

        /// <summary>混み具合の数を、経過した秒数ぶん減らす。</summary>
        public static float Decay(float value, float elapsedSeconds)
        {
            return elapsedSeconds <= 0f ? value : value * Mathf.Exp(-elapsedSeconds / CrowdDecaySeconds);
        }

        /// <summary>少し行きすぎてから戻る動き（0→1）。</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.9f;
            var u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        /// <summary>
        /// 泡の大きさの倍率。ふくらむ（<see cref="GrowSeconds"/>）→ そのまま → <paramref name="shrinkStart"/> 秒から <paramref name="shrinkSeconds"/> 秒で
        /// <see cref="ShrinkTo"/> 倍まで縮む。age がまだ 0 より前なら 0。
        /// </summary>
        public static float BubbleScale(float age, float shrinkStart, float shrinkSeconds)
        {
            if (age <= 0f)
            {
                return 0f;
            }

            if (age < shrinkStart)
            {
                return age < GrowSeconds ? EaseOutBack(age / GrowSeconds) : 1f;
            }

            var k = shrinkSeconds > 0f ? Mathf.Clamp01((age - shrinkStart) / shrinkSeconds) : 1f;
            return Mathf.Lerp(1f, ShrinkTo, k);
        }

        /// <summary>包む泡のかたまりの大きさの倍率（t は 0〜1）。最初の3割ですばやく広がり、残りで縮んで消える。</summary>
        public static float WrapScale(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.3f)
            {
                return Mathf.Lerp(0.7f, 1f, t / 0.3f);
            }

            var k = (t - 0.3f) / 0.7f;
            return 1f - k * k;
        }

        /// <summary>まとめて鳴らす効果音で、「たくさん」の音を使うか。</summary>
        public static bool UseManySound(int kills, int manyKillsCount)
        {
            return kills >= Mathf.Max(1, manyKillsCount);
        }
    }
}
