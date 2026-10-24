using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class BrushingMathTests
    {
        [Test]
        public void PulseNeedsAReleaseBeforeTheNextOne()
        {
            var counter = new GripPulseCounter();
            Assert.IsTrue(counter.Feed(0.8f, 0f));
            Assert.IsFalse(counter.Feed(0.9f, 0.1f), "握ったまま");
            Assert.IsFalse(counter.Feed(0.45f, 0.2f), "まだ離しきっていない");
            Assert.IsFalse(counter.Feed(0.6f, 0.3f));
            Assert.IsFalse(counter.Feed(0.2f, 0.4f));
            Assert.IsTrue(counter.Feed(0.7f, 0.5f));
            Assert.AreEqual(2, counter.Count);
        }

        [Test]
        public void BouncesAreIgnored()
        {
            var counter = new GripPulseCounter();
            counter.Feed(0.8f, 0f);
            counter.Feed(0.1f, 0.01f);
            Assert.IsFalse(counter.Feed(0.8f, 0.03f), "0.06秒より短い");
            Assert.AreEqual(1, counter.Count);
        }

        [Test]
        public void RewardHealsFirstThenCoats()
        {
            BrushingMath.Reward(1f, 100f, 90f, 0.25f, 0.2f, out var heal, out var coating);
            Assert.AreEqual(10f, heal, 1e-4f);
            Assert.AreEqual(15f, coating, 1e-4f);
            BrushingMath.Reward(1f, 100f, 100f, 0.5f, 0.2f, out heal, out coating);
            Assert.AreEqual(0f, heal, 1e-4f);
            Assert.AreEqual(20f, coating, 1e-4f, "コーティングは上限まで");
            BrushingMath.Reward(0.5f, 100f, 40f, 0.25f, 0.2f, out heal, out coating);
            Assert.AreEqual(12.5f, heal, 1e-4f);
            Assert.AreEqual(0f, coating, 1e-4f);
        }

        [Test]
        public void SyncNeedsEveryoneInTheMaskAndAtLeastTwo()
        {
            var last = new[] { 9.9f, 9.8f, float.NegativeInfinity, float.NegativeInfinity };
            Assert.IsTrue(BrushingMath.IsSync(last, 0b0011, 10f, 0.3f));
            Assert.IsFalse(BrushingMath.IsSync(last, 0b0111, 10f, 0.3f), "P3 がまだ");
            Assert.IsFalse(BrushingMath.IsSync(last, 0b0001, 10f, 0.3f), "1人ではシンクロにならない");
            Assert.AreEqual(0.5f, BrushingMath.Clean01(10, 20), 1e-4f);
            Assert.AreEqual(1f, BrushingMath.Clean01(30, 20), 1e-4f);
        }
    }
}
