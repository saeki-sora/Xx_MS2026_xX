using System;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>学びの数（<see cref="CpuSettings.learning"/>）。</summary>
    [Serializable]
    public struct CpuLearningTuning
    {
        [Tooltip("OFF にすると学ばない（試合の始めの上手さのまま）。")]
        public bool enabled;

        [Tooltip("いちばんうまくいった分野でも、1ウェーブにこの割合は伸びる（1 = ふつうの伸び）。")]
        [Range(0f, 2f)]
        public float minGrowth;

        [Tooltip("いちばん失敗が多かった分野は、1ウェーブにこの割合まで伸びる（1 = ふつうの伸び）。")]
        [Range(0f, 3f)]
        public float maxGrowth;

        [Tooltip("1試合のウェーブの数とみなす数（0 = ステージのウェーブの計画から。計画が無いステージは 5）。\n" +
                 "ふつうの伸びは「1 ÷ (ウェーブの数 − 1)」で、最後のウェーブのあたりで上限に近づく。")]
        [Min(0)]
        public int expectedWaves;

        public static CpuLearningTuning Default => new CpuLearningTuning
        {
            enabled = true,
            minGrowth = 0.5f,
            maxGrowth = 1.6f,
            expectedWaves = 0
        };
    }

    /// <summary>
    /// 学びの決まり（テストできるように Unity のオブジェクトに触らない）。
    /// ・ふつうの伸び（1ウェーブ）= 1 ÷ (ウェーブの数 − 1)。全部の分野がふつうに伸びると、最後のウェーブの始めで慣れが 1（その強さの上限）になる
    /// ・分野ごとの伸び = ふつうの伸び × lerp(minGrowth, maxGrowth, 苦手さ)。苦手さ（0〜1）は振り返り（<see cref="CpuPlugins.RegisterAreaReview"/>）が出す
    /// </summary>
    public static class CpuLearningMath
    {
        /// <summary>苦手さが分からない分野の苦手さ（ふつう）。</summary>
        public const float NeutralNeed = 0.5f;

        public const int FallbackWaveCount = 5;

        /// <summary>ふつうの伸び（1ウェーブ）。</summary>
        public static float BaseGrowth(int waveCount)
        {
            return 1f / Mathf.Max(1, waveCount - 1);
        }

        /// <summary>その分野の1ウェーブの伸び。</summary>
        public static float Growth(float need01, int waveCount, in CpuLearningTuning tuning)
        {
            var share = Mathf.Lerp(tuning.minGrowth, Mathf.Max(tuning.minGrowth, tuning.maxGrowth), Mathf.Clamp01(need01));
            return BaseGrowth(waveCount) * share;
        }

        /// <summary>学びに使うウェーブの数（設定 → ステージの計画 → 5）。</summary>
        public static int WaveCount(in CpuLearningTuning tuning, int planWaveCount)
        {
            if (tuning.expectedWaves > 0)
            {
                return tuning.expectedWaves;
            }

            return planWaveCount > 0 ? planWaveCount : FallbackWaveCount;
        }

        /// <summary>割合 a / b（b が 0 なら fallback）。</summary>
        public static float Ratio(float a, float b, float fallback) => b > 1e-4f ? a / b : fallback;

        /// <summary>value が good 以下なら 0、bad 以上なら 1、その間はまっすぐ。</summary>
        public static float Ramp(float value, float good, float bad)
        {
            if (Mathf.Approximately(good, bad))
            {
                return value >= bad ? 1f : 0f;
            }

            return Mathf.Clamp01((value - good) / (bad - good));
        }
    }
}
