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
        public void GameOverSkipsToNextQuickly()
        {
            Assert.AreEqual(ResultRules.StepBanner, ResultRules.StepAt(false, 1f));
            Assert.AreEqual(ResultRules.StepPanel, ResultRules.StepAt(false, 2.8f));
            Assert.AreEqual(ResultRules.StepNext, ResultRules.StepAt(false, 3.5f));
            Assert.Less(ResultRules.PanelTime(false), 3f);
        }
    }
}
