using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class KillBurstMathTests
    {
        [Test]
        public void QuantizeStepsAtTheFrameRate()
        {
            // 12コマ/秒なら 1/12 秒ごとの段になる（その間は同じ絵のまま）。
            Assert.AreEqual(0f, KillBurstMath.Quantize(0.05f, 12f), 1e-6f);
            Assert.AreEqual(1f / 12f, KillBurstMath.Quantize(0.1f, 12f), 1e-6f);
            Assert.AreEqual(2f / 12f, KillBurstMath.Quantize(0.17f, 12f), 1e-6f);
        }

        [Test]
        public void SixtyFramesOrMoreIsSmooth()
        {
            Assert.AreEqual(0.123f, KillBurstMath.Quantize(0.123f, 60f), 1e-6f);
        }

        [Test]
        public void DropsFlyOutFastThenSlowDown()
        {
            var start = KillBurstMath.DropDistance(0f);
            var early = KillBurstMath.DropDistance(0.25f) - start;
            var late = KillBurstMath.DropDistance(1f) - KillBurstMath.DropDistance(0.75f);
            Assert.AreEqual(0.25f, start, 1e-6f);
            Assert.Greater(early, late);
            Assert.AreEqual(1.35f, KillBurstMath.DropDistance(1f), 1e-5f);
        }
    }
}
