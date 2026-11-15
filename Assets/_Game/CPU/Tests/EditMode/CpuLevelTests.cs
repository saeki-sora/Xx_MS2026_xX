using NUnit.Framework;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuLevelCurveTests
    {
        [Test]
        public void Evaluate_ConnectsFourAnchors()
        {
            var curve = new CpuLevelCurve(0f, 10f, 20f, 40f);
            Assert.AreEqual(0f, curve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(5f, curve.Evaluate(0.5f), 1e-4f);
            Assert.AreEqual(10f, curve.Evaluate(1f), 1e-4f);
            Assert.AreEqual(30f, curve.Evaluate(2.5f), 1e-4f);
            Assert.AreEqual(40f, curve.Evaluate(3f), 1e-4f);
            Assert.AreEqual(40f, curve.Evaluate(9f), 1e-4f);
            Assert.AreEqual(0f, curve.Evaluate(-1f), 1e-4f);
        }

        [Test]
        public void Levels_HaveTouchingNonOverlappingBands()
        {
            var curve = new CpuLevelCurve(0f, 10f, 20f, 40f);
            Assert.AreEqual(0f, curve.Evaluate(CpuLevel.Easy, 0f), 1e-4f);
            Assert.AreEqual(10f, curve.Evaluate(CpuLevel.Easy, 1f), 1e-4f);
            Assert.AreEqual(10f, curve.Evaluate(CpuLevel.Normal, 0f), 1e-4f);
            Assert.AreEqual(20f, curve.Evaluate(CpuLevel.Normal, 1f), 1e-4f);
            Assert.AreEqual(20f, curve.Evaluate(CpuLevel.Hard, 0f), 1e-4f);
            Assert.AreEqual(40f, curve.Evaluate(CpuLevel.Hard, 1f), 1e-4f);
        }

        [Test]
        public void Scale_ClampsFamiliarity()
        {
            Assert.AreEqual(1f, CpuLevels.Scale(CpuLevel.Normal, -2f), 1e-5f);
            Assert.AreEqual(3f, CpuLevels.Scale(CpuLevel.Hard, 5f), 1e-5f);
            Assert.AreEqual(0.5f, CpuLevels.Scale(CpuLevel.Easy, 0.5f), 1e-5f);
        }
    }

    public sealed class CpuFamiliarityTests
    {
        [Test]
        public void StartsAtStartValue_SetAndAddClamp()
        {
            var f = new CpuFamiliarity(0.2f);
            Assert.AreEqual(0.2f, f.Get("aim"), 1e-5f);
            var changed = 0;
            f.Changed += _ => changed++;
            f.Add("aim", 0.5f);
            Assert.AreEqual(0.7f, f.Get("aim"), 1e-5f);
            f.Add("aim", 5f);
            Assert.AreEqual(1f, f.Get("aim"), 1e-5f);
            f.Set("aim", 1f); // 同じなら知らせない
            Assert.AreEqual(2, changed);
            Assert.AreEqual(2, f.Version);
        }
    }

    public sealed class CpuSkillProfileTests
    {
        [Test]
        public void Evaluate_UsesEachFieldsArea()
        {
            var profile = new CpuSkillProfile();
            var skill = profile.Evaluate(area => area == CpuSkillAreas.Aim ? 0f : 3f);
            Assert.AreEqual(CpuSkillProfile.Beginner.leadSeconds, skill.leadSeconds, 1e-5f);     // 狙いは初めて遊ぶ人
            Assert.AreEqual(CpuSkillProfile.Expert.reactionSeconds, skill.reactionSeconds, 1e-5f); // 反応はとても上手い人
            Assert.AreEqual(CpuSkillProfile.Expert.brushSync, skill.brushSync, 1e-5f);
        }

        [Test]
        public void Anchors_GetBetterTowardExpert()
        {
            var anchors = new[] { CpuSkillProfile.Beginner, CpuSkillProfile.Novice, CpuSkillProfile.Skilled, CpuSkillProfile.Expert };
            for (var i = 1; i < anchors.Length; i++)
            {
                Assert.LessOrEqual(anchors[i].reactionSeconds, anchors[i - 1].reactionSeconds);
                Assert.GreaterOrEqual(anchors[i].leadSeconds, anchors[i - 1].leadSeconds);
                Assert.LessOrEqual(anchors[i].heatPanic01, anchors[i - 1].heatPanic01);
                Assert.GreaterOrEqual(anchors[i].skillPatienceSeconds, anchors[i - 1].skillPatienceSeconds);
                Assert.GreaterOrEqual(anchors[i].smashBallAttention, anchors[i - 1].smashBallAttention);
                Assert.GreaterOrEqual(anchors[i].brushPulsesPerSecond, anchors[i - 1].brushPulsesPerSecond);
                Assert.GreaterOrEqual(anchors[i].minGrip, anchors[i - 1].minGrip); // 上手いほど強く握る（ダメージは太さに比例）
                Assert.LessOrEqual(anchors[i].startThreshold, anchors[i - 1].startThreshold); // 上手いほど少しの敵でも撃つ
            }
        }

        [Test]
        public void Player_StartsAtBandBottom_AndRefreshesOnFamiliarity()
        {
            var cpu = new CpuPlayer(1, CpuLevel.Normal, 3, 0.15f, new CpuSkillProfile());
            Assert.AreEqual(CpuSkillProfile.Novice.reactionSeconds, cpu.Context.Skill.reactionSeconds, 1e-5f);
            Assert.AreEqual(CpuSkillProfile.Novice.gripChangePerSecond, cpu.Hands.GripChangePerSecond, 1e-5f);

            cpu.Context.Familiarity.Set(CpuSkillAreas.Reaction, 1f);
            cpu.RefreshSkill();
            Assert.AreEqual(CpuSkillProfile.Skilled.reactionSeconds, cpu.Context.Skill.reactionSeconds, 1e-5f);
            Assert.AreEqual(CpuSkillProfile.Novice.leadSeconds, cpu.Context.Skill.leadSeconds, 1e-5f); // 狙いはまだ上がっていない
        }

        [Test]
        public void Player_StartFamiliarity_MovesWithinBand()
        {
            var cpu = new CpuPlayer(0, CpuLevel.Hard, 1, 0.15f, new CpuSkillProfile(), startFamiliarity: 1f);
            Assert.AreEqual(CpuSkillProfile.Expert.reactionSeconds, cpu.Context.Skill.reactionSeconds, 1e-5f);
            Assert.AreEqual(3f, cpu.Context.ScaleOf("anything"), 1e-5f);
        }
    }
}
