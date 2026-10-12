using MS2026.UI.Game;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class KillHudLogicTests
    {
        [Test]
        public void Combo_CountsKillsWithinTheWindow_AndBreaksAfterIt()
        {
            var logic = new KillHudLogic { comboWindowSeconds = 2f };
            logic.Update(0, 0, 0f);
            Assert.AreEqual(0, logic.Combo);

            logic.Update(3, 3, 0.5f);
            Assert.AreEqual(3, logic.Combo);
            Assert.AreEqual(1f, logic.ComboRemaining01, 1e-4f);

            logic.Update(5, 5, 2f);
            Assert.AreEqual(5, logic.Combo, "2秒以内に倒したので続く");

            logic.Update(5, 5, 3f);
            Assert.AreEqual(0.5f, logic.ComboRemaining01, 1e-4f);

            logic.Update(5, 5, 4.1f);
            Assert.AreEqual(0, logic.Combo, "2秒倒さなかったので途切れる");
            Assert.AreEqual(0f, logic.ComboRemaining01);

            logic.Update(6, 6, 5f);
            Assert.AreEqual(1, logic.Combo, "途切れた後は1から");
        }

        [Test]
        public void FirstSeenCountsDoNotStartAComboOrCelebration()
        {
            var logic = new KillHudLogic();
            logic.Update(120, 800, 10f);
            Assert.AreEqual(0, logic.Combo, "途中から入った人の最初の数はコンボにしない");
            Assert.IsFalse(logic.MilestoneShowing, "最初から届いていた区切りはお祝いしない");
            Assert.AreEqual(500, logic.Milestone);
        }

        [Test]
        public void Milestone_ShowsForAWhileWhenReached()
        {
            var logic = new KillHudLogic { milestoneShowSeconds = 2f };
            logic.Update(0, 90, 0f);
            logic.Update(0, 101, 1f);
            Assert.IsTrue(logic.MilestoneShowing);
            Assert.AreEqual(100, logic.Milestone);

            logic.Update(0, 150, 2.9f);
            Assert.IsTrue(logic.MilestoneShowing);
            logic.Update(0, 160, 3.1f);
            Assert.IsFalse(logic.MilestoneShowing, "2秒で消える");

            logic.Update(0, 600, 4f);
            Assert.AreEqual(500, logic.Milestone, "一度に複数越えたら大きい方");
        }

        [Test]
        public void Milestones_ContinueEveryStepAfterTheLastOne()
        {
            var logic = new KillHudLogic { milestones = new[] { 100, 250, 500, 1000 }, milestoneStepAfterLast = 1000 };
            Assert.AreEqual(0, logic.LastReached(99));
            Assert.AreEqual(250, logic.LastReached(499));
            Assert.AreEqual(1000, logic.LastReached(1999));
            Assert.AreEqual(2000, logic.LastReached(2000));
            Assert.AreEqual(5000, logic.LastReached(5999));

            logic.milestoneStepAfterLast = 0;
            Assert.AreEqual(1000, logic.LastReached(9999), "0 なら最後の区切りまで");
        }

        [Test]
        public void NewMatch_ResetsWithoutCelebrating()
        {
            var logic = new KillHudLogic();
            logic.Update(0, 0, 0f);
            logic.Update(50, 1200, 1f);
            Assert.IsTrue(logic.MilestoneShowing);

            logic.Update(0, 0, 1.5f);
            Assert.AreEqual(0, logic.Combo);
            Assert.IsFalse(logic.MilestoneShowing);
            Assert.AreEqual(0, logic.Milestone);
        }
    }
}
