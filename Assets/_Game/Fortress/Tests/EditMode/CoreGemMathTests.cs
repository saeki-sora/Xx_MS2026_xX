using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CoreGemMathTests
    {
        [Test]
        public void DamageGrowsAsHealthDrops()
        {
            Assert.AreEqual(0f, CoreGemMath.Damage(1f), 1e-5f);
            Assert.AreEqual(1f, CoreGemMath.Damage(0f), 1e-5f);
            Assert.Greater(CoreGemMath.Damage(0.2f), CoreGemMath.Damage(0.6f));
        }

        [Test]
        public void CracksStartBelowThreshold()
        {
            Assert.AreEqual(0f, CoreGemMath.CrackAmount(0.9f, 0.8f));
            Assert.AreEqual(0f, CoreGemMath.CrackAmount(0.8f, 0.8f));
            Assert.AreEqual(1f, CoreGemMath.CrackAmount(0f, 0.8f), 1e-5f);
            Assert.AreEqual(0.5f, CoreGemMath.CrackAmount(0.4f, 0.8f), 1e-5f);
            Assert.AreEqual(0f, CoreGemMath.CrackAmount(0f, 0f));
        }

        [Test]
        public void HitFlashFadesOut()
        {
            Assert.AreEqual(1f, CoreGemMath.HitFlash(0f, 0.25f), 1e-5f);
            Assert.Greater(CoreGemMath.HitFlash(0.05f, 0.25f), CoreGemMath.HitFlash(0.2f, 0.25f));
            Assert.AreEqual(0f, CoreGemMath.HitFlash(0.25f, 0.25f));
            Assert.AreEqual(0f, CoreGemMath.HitFlash(float.PositiveInfinity, 0.25f));
            Assert.AreEqual(0f, CoreGemMath.HitFlash(0f, 0f));
        }
    }
}
