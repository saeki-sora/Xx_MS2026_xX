using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 本体の分野の振り返り（そのウェーブの記録から「苦手さ」0〜1 を出す）。目安の数は仮（段11 の調整で直す）。
    /// 記録が足りない（撃っていない・ボールが見えなかった など）ときは、ふつう（0.5）。
    /// </summary>
    public static class CpuCoreReviews
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Aim, Aim);
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Heat, Heat);
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Reaction, Reaction);
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Skill, Skill);
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Awareness, Awareness);
            CpuPlugins.RegisterAreaReview(CpuSkillAreas.Brushing, Brushing);
        }

        /// <summary>狙い: 撃っている時間のうち空振り（今の向きに敵がいない）の割合。15% 以下ならうまい、50% 以上なら苦手。</summary>
        public static float Aim(CpuWaveStats wave)
        {
            var c = wave.Counters;
            var firing = c.Get(CpuStats.FiringSeconds);
            if (firing < 1f)
            {
                return CpuLearningMath.NeutralNeed;
            }

            return CpuLearningMath.Ramp(c.Get(CpuStats.WhiffSeconds) / firing, 0.15f, 0.5f);
        }

        /// <summary>熱の読み: オーバーヒート（2回で苦手いっぱい）。オーバーヒートしなくても、熱が 85% を超えていたら少し苦手。</summary>
        public static float Heat(CpuWaveStats wave)
        {
            var c = wave.Counters;
            var overheats = CpuLearningMath.Ramp(c.Get(CpuStats.Overheats), 0f, 2f);
            var close = CpuLearningMath.Ramp(c.Get(CpuStats.MaxHeat), 85f, 100f) * 0.6f;
            return Mathf.Max(overheats, close);
        }

        /// <summary>反応: 狙いと熱の読みの平均（全体の慣れ。反応は単独では測りにくいので、撃ち方の出来で決める）。</summary>
        public static float Reaction(CpuWaveStats wave)
        {
            return (Aim(wave) + Heat(wave)) * 0.5f;
        }

        /// <summary>スキルの判断: 使ったスキルの効きの平均が低いほど苦手。使わずに待っていただけなら少し苦手。</summary>
        public static float Skill(CpuWaveStats wave)
        {
            var c = wave.Counters;
            var used = c.Get(CpuSkillBehavior.UsedCount);
            if (used > 0f)
            {
                return Mathf.Clamp01(1f - c.Get(CpuSkillBehavior.UsedValue) / used);
            }

            return c.Get(CpuSkillBehavior.WaitSeconds) > 3f ? 0.6f : CpuLearningMath.NeutralNeed;
        }

        /// <summary>周りを見る: ボールが見えていた時間のうち、狙えた時間の割合（30% 以上ならうまい、5% 以下なら苦手）。</summary>
        public static float Awareness(CpuWaveStats wave)
        {
            var c = wave.Counters;
            var visible = c.Get(CpuStats.BallVisibleSeconds);
            if (visible < 1f)
            {
                return CpuLearningMath.NeutralNeed;
            }

            return 1f - CpuLearningMath.Ramp(c.Get(CpuSmashBallBehavior.AimSeconds) / visible, 0.05f, 0.3f);
        }

        /// <summary>
        /// 歯みがき: 合間の歯みがきタイムの「にぎにぎ」の回数（8 回以下なら苦手、22 回以上ならうまい）。
        /// 歯みがきタイムはウェーブの合間なので、次のウェーブの記録に入る（そのウェーブの終わりに振り返る）。
        /// </summary>
        public static float Brushing(CpuWaveStats wave)
        {
            var pulses = wave.Counters.Get(CpuBrushingBehavior.Pulses);
            return pulses > 0f ? 1f - CpuLearningMath.Ramp(pulses, 8f, 22f) : CpuLearningMath.NeutralNeed;
        }
    }
}
