using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class LaserLookMathTests
    {
        [Test]
        public void FlowGetsFasterWithThickness()
        {
            var look = new LaserLookSettings();
            Assert.AreEqual(look.flowSpeedWeak, LaserLookMath.FlowSpeed(look, 0f), 1e-5f);
            Assert.AreEqual(look.flowSpeedStrong, LaserLookMath.FlowSpeed(look, 1f), 1e-5f);
            Assert.Greater(LaserLookMath.FlowSpeed(look, 0.6f), LaserLookMath.FlowSpeed(look, 0.3f));
        }

        [Test]
        public void FlowClampsThicknessOutsideZeroToOne()
        {
            var look = new LaserLookSettings();
            Assert.AreEqual(look.flowSpeedWeak, LaserLookMath.FlowSpeed(look, -1f), 1e-5f);
            Assert.AreEqual(look.flowSpeedStrong, LaserLookMath.FlowSpeed(look, 2f), 1e-5f);
        }
    }
}
