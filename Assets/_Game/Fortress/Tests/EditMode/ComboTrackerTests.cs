using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class ComboTrackerTests
    {
        [Test]
        public void CountsComboAndKeepsTheMaximum()
        {
            var combo = new ComboTracker { windowSeconds = 2f };
            combo.Update(0, 0f);
            combo.Update(5, 1f);
            combo.Update(8, 2.5f);
            Assert.AreEqual(8, combo.Combo);
            Assert.AreEqual(8, combo.Max);

            combo.Update(8, 5f);
            Assert.AreEqual(0, combo.Combo, "2秒倒さなかったので途切れる");
            combo.Update(10, 6f);
            Assert.AreEqual(2, combo.Combo);
            Assert.AreEqual(8, combo.Max, "最大は残る");
        }

        [Test]
        public void FirstSeenCountIsNotACombo()
        {
            var combo = new ComboTracker();
            combo.Update(300, 10f);
            Assert.AreEqual(0, combo.Combo);
            Assert.AreEqual(0, combo.Max);
        }

        [Test]
        public void ResetStartsANewMatch()
        {
            var combo = new ComboTracker();
            combo.Update(0, 0f);
            combo.Update(20, 1f);
            combo.Reset();
            Assert.AreEqual(0, combo.Max);
            combo.Update(0, 2f);
            combo.Update(3, 2.5f);
            Assert.AreEqual(3, combo.Max);
        }
    }
}
