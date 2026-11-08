using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class FoamPopMathTests
    {
        [Test]
        public void BubbleCountDropsFromNormalToCrowdedAsKillsPileUp()
        {
            Assert.AreEqual(5, FoamPopMath.BubbleCount(0f, 5, 2, 3.5f, 7f));
            Assert.AreEqual(5, FoamPopMath.BubbleCount(3.5f, 5, 2, 3.5f, 7f));
            Assert.AreEqual(2, FoamPopMath.BubbleCount(7f, 5, 2, 3.5f, 7f));
            Assert.AreEqual(2, FoamPopMath.BubbleCount(50f, 5, 2, 3.5f, 7f));
            var middle = FoamPopMath.BubbleCount(5.25f, 5, 2, 3.5f, 7f);
            Assert.That(middle, Is.InRange(3, 4));
        }

        [Test]
        public void BubbleCountWithSameThresholdsSwitchesAtOnce()
        {
            Assert.AreEqual(5, FoamPopMath.BubbleCount(2f, 5, 2, 3f, 3f));
            Assert.AreEqual(2, FoamPopMath.BubbleCount(3f, 5, 2, 3f, 3f));
        }

        [Test]
        public void CrowdCountFadesOverTime()
        {
            Assert.AreEqual(4f, FoamPopMath.Decay(4f, 0f), 1e-5f);
            Assert.Less(FoamPopMath.Decay(4f, FoamPopMath.CrowdDecaySeconds), 1.6f);
            Assert.Less(FoamPopMath.Decay(4f, 1f), 0.01f);
        }

        [Test]
        public void BubbleGrowsHoldsThenShrinksToDotSize()
        {
            Assert.AreEqual(0f, FoamPopMath.BubbleScale(0f, 0.2f, 0.08f));
            Assert.AreEqual(0f, FoamPopMath.BubbleScale(-0.01f, 0.2f, 0.08f));
            Assert.Greater(FoamPopMath.BubbleScale(0.04f, 0.2f, 0.08f), 1f, "ふくらむ途中で少し行きすぎる");
            Assert.AreEqual(1f, FoamPopMath.BubbleScale(0.1f, 0.2f, 0.08f), 1e-5f);
            Assert.AreEqual(FoamPopMath.ShrinkTo, FoamPopMath.BubbleScale(0.28f, 0.2f, 0.08f), 1e-4f);
        }

        [Test]
        public void WrapPopsOutThenVanishes()
        {
            Assert.AreEqual(0.7f, FoamPopMath.WrapScale(0f), 1e-5f);
            Assert.AreEqual(1f, FoamPopMath.WrapScale(0.3f), 1e-5f);
            Assert.AreEqual(0f, FoamPopMath.WrapScale(1f), 1e-5f);
        }

        [Test]
        public void ManySoundOnlyFromThreshold()
        {
            Assert.IsFalse(FoamPopMath.UseManySound(7, 8));
            Assert.IsTrue(FoamPopMath.UseManySound(8, 8));
            Assert.IsTrue(FoamPopMath.UseManySound(1, 0));
        }
    }
}
