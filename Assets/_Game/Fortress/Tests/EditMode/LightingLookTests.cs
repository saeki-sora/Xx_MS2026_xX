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
        public void SaturateKeepsGrayAndStrengthensColor()
        {
            var red = new UnityEngine.Color(1f, 0.3f, 0.3f);
            var same = LaserLightMath.Saturate(red, 1f);
            Assert.AreEqual(red.r, same.r, 1e-5f);
            Assert.AreEqual(red.g, same.g, 1e-5f);
            var strong = LaserLightMath.Saturate(red, 1.5f);
            Assert.Less(strong.g, red.g);
            Assert.GreaterOrEqual(strong.g, 0f);
            var gray = LaserLightMath.Saturate(red, 0f);
            Assert.AreEqual(gray.r, gray.g, 1e-5f);
            Assert.AreEqual(gray.g, gray.b, 1e-5f);
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
