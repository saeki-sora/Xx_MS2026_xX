using NUnit.Framework;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuPersonalityTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(12345)]
        [TestCase(-7)]
        public void Mixed_ThreeCpusAlwaysGetAllThreePersonalities(int matchSeed)
        {
            var labels = new System.Collections.Generic.HashSet<string>();
            for (var k = 0; k < 3; k++)
            {
                labels.Add(CpuPersonalities.For(CpuPersonalityMix.Mixed, k, matchSeed, 0f).Label);
            }

            Assert.AreEqual(3, labels.Count); // 世話焼き・欲張り・バランス
        }

        [Test]
        public void SameSeedSamePersonality_AllModesRespected()
        {
            Assert.AreEqual(CpuPersonalities.For(CpuPersonalityMix.Mixed, 2, 7).teamSpirit, CpuPersonalities.For(CpuPersonalityMix.Mixed, 2, 7).teamSpirit);
            Assert.AreEqual("欲張り", CpuPersonalities.For(CpuPersonalityMix.AllGreedy, 1, 3, 0f).Label);
            Assert.AreEqual("世話焼き", CpuPersonalities.For(CpuPersonalityMix.AllCaretakers, 1, 3, 0f).Label);
            Assert.AreEqual("バランス", CpuPersonalities.For(CpuPersonalityMix.AllBalanced, 1, 3, 0f).Label);
        }

        [Test]
        public void Jitter_StaysInRange()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var p = CpuPersonalities.For(CpuPersonalityMix.AllGreedy, 0, seed, 0.3f);
                Assert.That(p.teamSpirit, Is.InRange(0f, 1f));
                Assert.That(p.greed, Is.InRange(0f, 1f));
            }
        }
    }

    public sealed class CpuMoodMathTests
    {
        [Test]
        public void SafeMatch_FollowsPersonality()
        {
            var mood = CpuMoodMath.Evaluate(CpuPersonality.Greedy, 1f, 0, 1, 4);
            Assert.AreEqual(CpuPersonality.Greedy.teamSpirit, mood.Coop, 1e-5f);
            Assert.AreEqual(CpuPersonality.Greedy.greed, mood.Greed, 1e-5f);
        }

        [Test]
        public void DangerAndDownedMates_PushTowardCoop()
        {
            var safe = CpuMoodMath.Evaluate(CpuPersonality.Greedy, 1f, 0, 1, 4);
            var danger = CpuMoodMath.Evaluate(CpuPersonality.Greedy, 0.2f, 1, 1, 4);
            Assert.Greater(danger.Coop, safe.Coop + 0.5f);
            Assert.Less(danger.Greed, safe.Greed * 0.5f);
        }

        [Test]
        public void Rank_TopYieldsLastPushes()
        {
            var top = CpuMoodMath.Evaluate(CpuPersonality.Balanced, 1f, 0, 0, 4);
            var mid = CpuMoodMath.Evaluate(CpuPersonality.Balanced, 1f, 0, 1, 4);
            var last = CpuMoodMath.Evaluate(CpuPersonality.Balanced, 1f, 0, 3, 4);
            Assert.Less(top.Greed, mid.Greed);
            Assert.Greater(last.Greed, mid.Greed);
        }

        [Test]
        public void RankOf_CountsPlayersWithMoreKills()
        {
            var kills = new[] { 10, 30, 30, 5 };
            Assert.AreEqual(2, CpuMoodMath.Rank(kills, 0, 4));
            Assert.AreEqual(0, CpuMoodMath.Rank(kills, 1, 4)); // 同じ数なら上
            Assert.AreEqual(3, CpuMoodMath.Rank(kills, 3, 4));
        }

        [Test]
        public void ShareWeights_CoopLeavesContestedEnemies()
        {
            var weights = new[] { 10f, 4f };
            var contested = new[] { 6f, 0f };
            var results = new float[2];
            CpuMoodMath.ShareWeights(weights, contested, new CpuMood(1f, 0f), results);
            Assert.AreEqual(10f - 6f * 0.8f, results[0], 1e-4f);
            Assert.AreEqual(4f, results[1], 1e-4f);

            CpuMoodMath.ShareWeights(weights, contested, new CpuMood(0.3f, 1f), results);
            Assert.AreEqual(10f, results[0], 1e-4f); // 欲張りは任せない
        }

        [Test]
        public void BallFactor_GreedUp_YieldWhenOthersAim()
        {
            Assert.Greater(CpuMoodMath.BallFactor(new CpuMood(0.3f, 0.9f), 0), CpuMoodMath.BallFactor(new CpuMood(0.3f, 0.2f), 0));
            Assert.Less(CpuMoodMath.BallFactor(new CpuMood(0.9f, 0.2f), 1), CpuMoodMath.BallFactor(new CpuMood(0.9f, 0.2f), 0) * 0.5f);
            Assert.AreEqual(CpuMoodMath.BallFactor(new CpuMood(0.2f, 0.9f), 1), CpuMoodMath.BallFactor(new CpuMood(0.2f, 0.9f), 0), 1e-5f);
        }
    }
}
