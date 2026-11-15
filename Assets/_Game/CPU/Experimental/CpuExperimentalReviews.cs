using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】実験中の分野の振り返り（そのウェーブの記録から「苦手さ」0〜1）。登録は <see cref="CpuExperimentalInstaller"/>。
    /// 移動・倒れる仕組みが不採用になったら、その分野の振り返りもここから消す。
    /// </summary>
    public static class CpuExperimentalReviews
    {
        /// <summary>動き: ウェーブの時間のうち、囲まれていた割合（5% 以下ならうまい、30% 以上なら苦手）。</summary>
        public static float Move(CpuWaveStats wave)
        {
            if (wave.Seconds < 5f)
            {
                return CpuLearningMath.NeutralNeed;
            }

            return CpuLearningMath.Ramp(wave.Counters.Get(CpuMovePlanner.SurroundedSeconds) / wave.Seconds, 0.05f, 0.3f);
        }

        /// <summary>救助: 自分が倒れた回数（2回で苦手いっぱい）。倒れなければ、少しうまい。</summary>
        public static float Rescue(CpuWaveStats wave)
        {
            var downs = wave.Counters.Get(CpuStruggleBehavior.Downs);
            return downs > 0f ? Mathf.Lerp(0.5f, 1f, CpuLearningMath.Ramp(downs, 1f, 2f)) : 0.3f;
        }
    }
}
