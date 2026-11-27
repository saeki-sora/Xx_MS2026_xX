using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>熱の見せ方（案A：芯の色・砲台の輪・点滅）の計算。</summary>
    public sealed class LaserHeatLookMathTests
    {
        [Test]
        public void DoesNotBlinkBelowTheWarningHeat()
        {
            Assert.AreEqual(1f, LaserHeatLookMath.Blink(0.5f, 0.79f, 0.8f, 0.6f));
            Assert.AreEqual(0f, LaserHeatLookMath.AdvancePhase(0.4f, 0.1f, 0.5f, 0.8f, 3f, 9f));
        }

        [Test]
        public void BlinkDarkensByTheDepthAtTheMiddleOfTheCycle()
        {
            Assert.AreEqual(1f, LaserHeatLookMath.Blink(0f, 0.9f, 0.8f, 0.6f), 1e-4f);
            Assert.AreEqual(0.4f, LaserHeatLookMath.Blink(0.5f, 0.9f, 0.8f, 0.6f), 1e-4f);
        }

        [Test]
        public void BlinkGetsFasterAsTheHeatNearsTheLimit()
        {
            var slow = LaserHeatLookMath.AdvancePhase(0f, 0.1f, 0.8f, 0.8f, 3f, 9f);
            var fast = LaserHeatLookMath.AdvancePhase(0f, 0.1f, 0.99f, 0.8f, 3f, 9f);
            Assert.AreEqual(0.3f, slow, 1e-4f);
            Assert.That(fast, Is.GreaterThan(slow));
        }

        [Test]
        public void RingShowsHeatWhileFiringAndRemainingStopTimeWhileOverheated()
        {
            Assert.AreEqual(0.4f, LaserHeatLookMath.RingFill(false, 0.4f, 0f, 3f), 1e-4f);
            Assert.AreEqual(0.5f, LaserHeatLookMath.RingFill(true, 0.9f, 1.5f, 3f), 1e-4f);
            Assert.AreEqual(0f, LaserHeatLookMath.RingFill(true, 0.9f, 1f, 0f));
        }

        [Test]
        public void RingHidesWhenCoolButAlwaysShowsWhileOverheated()
        {
            Assert.IsFalse(LaserHeatLookMath.RingWanted(false, 0.01f, 0.03f));
            Assert.IsTrue(LaserHeatLookMath.RingWanted(false, 0.05f, 0.03f));
            Assert.IsTrue(LaserHeatLookMath.RingWanted(true, 0f, 0.03f));
        }

        [Test]
        public void CoreWidensWithHeat()
        {
            Assert.AreEqual(0f, LaserHeatLookMath.CoreWiden(0f, 0.22f));
            Assert.AreEqual(0.11f, LaserHeatLookMath.CoreWiden(0.5f, 0.22f), 1e-4f);
            Assert.AreEqual(0.22f, LaserHeatLookMath.CoreWiden(2f, 0.22f), 1e-4f);
        }
    }
}
