using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CoreBreakSlowMotionTests
    {
        [Test]
        public void HoldsSlowThenEasesBackToNormal()
        {
            Assert.AreEqual(0.2f, CoreBreakSlowMotion.ScaleAt(0.3f, 0.2f, 1f, 0.7f, 1.1f), 1e-6f);
            var mid = CoreBreakSlowMotion.ScaleAt(0.7f + 0.55f, 0.2f, 1f, 0.7f, 1.1f);
            Assert.AreEqual(0.6f, mid, 1e-4f);
            Assert.AreEqual(1f, CoreBreakSlowMotion.ScaleAt(5f, 0.2f, 1f, 0.7f, 1.1f), 1e-6f);
        }
    }
}
