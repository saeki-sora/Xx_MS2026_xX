using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class LaserLightMathTests
    {
        [Test]
        public void BrightnessGoesFromWeakGripToFull()
        {
            Assert.AreEqual(0.45f, LaserLightMath.Brightness(0f, 0.45f), 1e-5f);
            Assert.AreEqual(1f, LaserLightMath.Brightness(1f, 0.45f), 1e-5f);
            Assert.AreEqual(1f, LaserLightMath.Brightness(3f, 0.45f), 1e-5f);
        }

        [Test]
        public void LightCountCoversBothEndsAndIsCapped()
        {
            Assert.AreEqual(1, LaserLightMath.LightCount(0f, 2.5f));
            Assert.AreEqual(2, LaserLightMath.LightCount(2f, 2.5f));
            Assert.AreEqual(5, LaserLightMath.LightCount(10f, 2.5f));
            Assert.AreEqual(LaserLightMath.MaxLightsPerBeam, LaserLightMath.LightCount(100f, 2.5f));
        }

        [Test]
        public void LightPositionsSpanTheBeam()
        {
            Assert.AreEqual(0.5f, LaserLightMath.LightPosition01(0, 1));
            Assert.AreEqual(0f, LaserLightMath.LightPosition01(0, 5));
            Assert.AreEqual(1f, LaserLightMath.LightPosition01(4, 5));
            Assert.AreEqual(0.5f, LaserLightMath.LightPosition01(2, 5), 1e-5f);
        }
    }

    public sealed class CoreDangerMathTests
    {
        [Test]
        public void DangerIsZeroAboveStartAndOneAtZeroHealth()
        {
            Assert.AreEqual(0f, CoreDangerMath.Danger(1f, 0.6f));
            Assert.AreEqual(0f, CoreDangerMath.Danger(0.6f, 0.6f));
            Assert.AreEqual(1f, CoreDangerMath.Danger(0f, 0.6f), 1e-5f);
            Assert.Greater(CoreDangerMath.Danger(0.2f, 0.6f), CoreDangerMath.Danger(0.4f, 0.6f));
        }

        [Test]
        public void PulseOnlyBelowThreshold()
        {
            for (var t = 0f; t < 3f; t += 0.031f)
            {
                Assert.AreEqual(1f, CoreDangerMath.Pulse(0.5f, 0.25f, 1.4f, t));
                Assert.AreEqual(1f, CoreDangerMath.Pulse(0.1f, 0f, 1.4f, t));
                Assert.That(CoreDangerMath.Pulse(0.1f, 0.25f, 1.4f, t), Is.InRange(1f, 1.6f + 1e-4f));
            }
        }

        [Test]
        public void PulsePeaksOnTheBeat()
        {
            // 1拍目の山（拍の8%の位置）で一番強い。
            var peak = CoreDangerMath.Pulse(0f, 0.25f, 1f, 0.08f);
            var rest = CoreDangerMath.Pulse(0f, 0.25f, 1f, 0.7f);
            Assert.Greater(peak, 1.5f);
            Assert.AreEqual(1f, rest, 1e-5f);
        }
    }
}
