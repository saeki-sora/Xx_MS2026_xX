using MS2026.UI.Game;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class ResultRulesTests
    {
        [Test]
        public void RankFollowsCoreDurability()
        {
            Assert.AreEqual("S", ResultRules.Rank(1f));
            Assert.AreEqual("S", ResultRules.Rank(0.9f));
            Assert.AreEqual("A", ResultRules.Rank(0.89f));
            Assert.AreEqual("A", ResultRules.Rank(0.7f));
            Assert.AreEqual("B", ResultRules.Rank(0.4f));
            Assert.AreEqual("C", ResultRules.Rank(0.39f));
            Assert.AreEqual("C", ResultRules.Rank(0f));
        }

        [Test]
        public void ClearRevealsStepByStep()
        {
            Assert.AreEqual(ResultRules.StepNone, ResultRules.StepAt(true, -0.1f));
            Assert.AreEqual(ResultRules.StepBanner, ResultRules.StepAt(true, 0f));
            Assert.AreEqual(ResultRules.StepPanel, ResultRules.StepAt(true, 2.7f));
            Assert.AreEqual(ResultRules.StepRank, ResultRules.StepAt(true, 8f));
            Assert.AreEqual(ResultRules.StepNext, ResultRules.StepAt(true, 30f));

            var last = 0;
            for (var t = 0f; t < 10f; t += 0.1f)
            {
                var step = ResultRules.StepAt(true, t);
                Assert.GreaterOrEqual(step, last, "段階は戻らない");
                last = step;
            }
        }

        [Test]
        public void GameOverWaitsForAnimation()
        {
            Assert.AreEqual(ResultRules.StepBanner, ResultRules.StepAt(false, 1f));
            Assert.AreEqual(ResultRules.StepBanner, ResultRules.StepAt(false, 4.99f));
            Assert.AreEqual(ResultRules.StepPanel, ResultRules.StepAt(false, 5f));
            Assert.AreEqual(ResultRules.StepNext, ResultRules.StepAt(false, 8.7f));
            Assert.AreEqual(5f, ResultRules.PanelTime(false));
        }

        [Test]
        public void AllPlayersAreRankedIncludingZeroKills()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 0, 3 }, ResultRules.PlayersByKills(new[] { 0, 12, 12 }, 4));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, ResultRules.PlayersByKills(null, 4));
        }

        [Test]
        public void GaugeStopsAtActualFinalHealth()
        {
            Assert.AreEqual(1f, ResultRules.GameOverGaugeHealth(0.9f, 0f));
            Assert.That(ResultRules.GameOverGaugeHealth(1.45f, 0f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.AreEqual(0f, ResultRules.GameOverGaugeHealth(1.9f, 0f));
            Assert.That(ResultRules.GameOverGaugeHealth(5f, 0.35f), Is.EqualTo(0.35f).Within(0.001f));
        }
    }
}
