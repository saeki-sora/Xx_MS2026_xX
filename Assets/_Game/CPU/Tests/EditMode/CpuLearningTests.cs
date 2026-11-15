using System.Collections.Generic;
using NUnit.Framework;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuLearningMathTests
    {
        [Test]
        public void BaseGrowth_ReachesOneByLastWave()
        {
            Assert.AreEqual(0.25f, CpuLearningMath.BaseGrowth(5), 1e-5f);
            Assert.AreEqual(1f, CpuLearningMath.BaseGrowth(1), 1e-5f);
            Assert.AreEqual(1f, CpuLearningMath.BaseGrowth(0), 1e-5f);
        }

        [Test]
        public void Growth_ScalesWithNeed()
        {
            var t = CpuLearningTuning.Default; // 0.5〜1.6
            Assert.AreEqual(0.125f, CpuLearningMath.Growth(0f, 5, t), 1e-5f);
            Assert.AreEqual(0.4f, CpuLearningMath.Growth(1f, 5, t), 1e-5f);
            Assert.Greater(CpuLearningMath.Growth(0.7f, 5, t), CpuLearningMath.Growth(0.3f, 5, t));
        }

        [Test]
        public void WaveCount_SettingThenPlanThenFallback()
        {
            var t = CpuLearningTuning.Default;
            Assert.AreEqual(7, CpuLearningMath.WaveCount(t, 7));
            Assert.AreEqual(CpuLearningMath.FallbackWaveCount, CpuLearningMath.WaveCount(t, 0));
            t.expectedWaves = 3;
            Assert.AreEqual(3, CpuLearningMath.WaveCount(t, 7));
        }

        [Test]
        public void Ramp_ClampsBetweenGoodAndBad()
        {
            Assert.AreEqual(0f, CpuLearningMath.Ramp(0.1f, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, CpuLearningMath.Ramp(0.325f, 0.15f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, CpuLearningMath.Ramp(0.9f, 0.15f, 0.5f), 1e-5f);
            // 逆向き（大きいほど良い）にも使える。
            Assert.AreEqual(1f, CpuLearningMath.Ramp(2f, 8f, 3f), 1e-5f);
        }
    }

    public sealed class CpuCoreReviewsTests
    {
        private static CpuWaveStats Wave(params (string key, float value)[] counts)
        {
            var wave = new CpuWaveStats { Seconds = 60f };
            foreach (var (key, value) in counts)
            {
                wave.Counters.Set(key, value);
            }

            return wave;
        }

        [Test]
        public void Aim_WhiffRatio()
        {
            Assert.AreEqual(0f, CpuCoreReviews.Aim(Wave((CpuStats.FiringSeconds, 20f), (CpuStats.WhiffSeconds, 2f))), 1e-5f);
            Assert.AreEqual(1f, CpuCoreReviews.Aim(Wave((CpuStats.FiringSeconds, 20f), (CpuStats.WhiffSeconds, 12f))), 1e-5f);
            Assert.AreEqual(CpuLearningMath.NeutralNeed, CpuCoreReviews.Aim(Wave()), 1e-5f); // 撃っていない
        }

        [Test]
        public void Heat_OverheatsAndCloseCalls()
        {
            Assert.AreEqual(0f, CpuCoreReviews.Heat(Wave((CpuStats.MaxHeat, 60f))), 1e-5f);
            Assert.AreEqual(1f, CpuCoreReviews.Heat(Wave((CpuStats.Overheats, 2f), (CpuStats.MaxHeat, 100f))), 1e-5f);
            Assert.AreEqual(0.6f, CpuCoreReviews.Heat(Wave((CpuStats.MaxHeat, 100f))), 1e-5f);
        }

        [Test]
        public void Skill_LowValueIsWeak()
        {
            Assert.AreEqual(0.2f, CpuCoreReviews.Skill(Wave((CpuSkillBehavior.UsedCount, 2f), (CpuSkillBehavior.UsedValue, 1.6f))), 1e-5f);
            Assert.AreEqual(0.6f, CpuCoreReviews.Skill(Wave((CpuSkillBehavior.WaitSeconds, 5f))), 1e-5f);
            Assert.AreEqual(CpuLearningMath.NeutralNeed, CpuCoreReviews.Skill(Wave()), 1e-5f);
        }

        [Test]
        public void Awareness_AimedShareOfVisibleTime()
        {
            Assert.AreEqual(1f, CpuCoreReviews.Awareness(Wave((CpuStats.BallVisibleSeconds, 10f))), 1e-5f);
            Assert.AreEqual(0f, CpuCoreReviews.Awareness(Wave((CpuStats.BallVisibleSeconds, 10f), (CpuSmashBallBehavior.AimSeconds, 4f))), 1e-5f);
            Assert.AreEqual(CpuLearningMath.NeutralNeed, CpuCoreReviews.Awareness(Wave()), 1e-5f);
        }

        [Test]
        public void Brushing_PulseCount()
        {
            Assert.AreEqual(1f, CpuCoreReviews.Brushing(Wave((CpuBrushingBehavior.Pulses, 5f))), 1e-5f);
            Assert.AreEqual(0f, CpuCoreReviews.Brushing(Wave((CpuBrushingBehavior.Pulses, 25f))), 1e-5f);
            Assert.AreEqual(CpuLearningMath.NeutralNeed, CpuCoreReviews.Brushing(Wave()), 1e-5f);
        }
    }

    public sealed class CpuLearnerTests
    {
        private static readonly CpuSkillArea[] Areas = { new CpuSkillArea("a", "分野A"), new CpuSkillArea("b", "分野B") };

        private static (CpuContext context, CpuLearner learner) Make(Dictionary<string, float> needs, int waves = 5)
        {
            var context = new CpuContext(0, CpuLevel.Normal, 1, new CpuHands(0));
            var learner = new CpuLearner(context, () => CpuLearningTuning.Default, () => waves, () => Areas,
                (area, wave) => needs.TryGetValue(area, out var n) ? n : CpuLearningMath.NeutralNeed);
            return (context, learner);
        }

        [Test]
        public void WaveEnd_RaisesWeakAreaMore_AndWritesToWaveRecord()
        {
            var (context, learner) = Make(new Dictionary<string, float> { { "a", 1f }, { "b", 0f } });
            context.Stats.BeginWave(0);
            var wave = context.Stats.EndWave(true); // 閉じると自動で振り返る

            Assert.AreEqual(1, learner.Reviews);
            Assert.AreEqual(0.4f, context.Familiarity.Get("a"), 1e-5f);
            Assert.AreEqual(0.125f, context.Familiarity.Get("b"), 1e-5f);
            Assert.AreEqual(1f, wave.Counters.Get(CpuLearner.NeedPrefix + "分野A"), 1e-5f);
            Assert.AreEqual(0.125f, wave.Counters.Get(CpuLearner.GrowthPrefix + "分野B"), 1e-5f);
            Assert.AreEqual(1.4f, context.ScaleOf("a"), 1e-5f); // ふつう（1）＋ 慣れ
        }

        [Test]
        public void Familiarity_CapsAtOne_GrowthRecordsActualIncrease()
        {
            var (context, _) = Make(new Dictionary<string, float> { { "a", 1f } }, waves: 2); // ふつうの伸び 1、苦手なら 1.6
            context.Stats.BeginWave(0);
            var wave = context.Stats.EndWave(true);
            Assert.AreEqual(1f, context.Familiarity.Get("a"), 1e-5f);
            Assert.AreEqual(1f, wave.Counters.Get(CpuLearner.GrowthPrefix + "分野A"), 1e-5f);
        }

        [Test]
        public void Disabled_DoesNotLearn()
        {
            var context = new CpuContext(0, CpuLevel.Normal, 1, new CpuHands(0));
            var tuning = CpuLearningTuning.Default;
            tuning.enabled = false;
            var learner = new CpuLearner(context, () => tuning, () => 5, () => Areas, (a, w) => 1f);
            context.Stats.EndWave(true);
            Assert.AreEqual(0, learner.Reviews);
            Assert.AreEqual(0f, context.Familiarity.Get("a"), 1e-5f);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var (context, learner) = Make(new Dictionary<string, float>());
            learner.Dispose();
            context.Stats.EndWave(true);
            Assert.AreEqual(0, learner.Reviews);
        }

        [Test]
        public void Player_SkillImprovesAfterReview()
        {
            var cpu = new CpuPlayer(1, CpuLevel.Normal, 3, 0.15f, new CpuSkillProfile());
            var before = cpu.Context.Skill.reactionSeconds;
            var learner = new CpuLearner(cpu.Context, () => CpuLearningTuning.Default, () => 5,
                () => new[] { new CpuSkillArea(CpuSkillAreas.Reaction, "反応") }, (area, wave) => 1f);
            learner.Review(new CpuWaveStats());
            cpu.RefreshSkill();
            Assert.Less(cpu.Context.Skill.reactionSeconds, before);
        }
    }

    public sealed class CpuStatsCarryTests
    {
        [Test]
        public void BetweenWaveCounts_CarryIntoNextWave()
        {
            var stats = new CpuStats();
            stats.BeginWave(0);
            stats.EndWave(true);
            stats.Add("歯みがきの回", 12f); // 合間の歯みがきタイム
            stats.BeginWave(1);
            Assert.AreEqual(1, stats.Current.WaveIndex);
            Assert.AreEqual(12f, stats.Current.Counters.Get("歯みがきの回"));
        }
    }
}
