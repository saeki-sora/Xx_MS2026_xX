using MS2026.Fortress;
using MS2026.UI.Game;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class WaveHudLogicTests
    {
        [Test]
        public void ClockRoundsUpAndShowsDashesWithoutALimit()
        {
            Assert.AreEqual("02:48", WaveHudLogic.Clock(167.2f));
            Assert.AreEqual("00:00", WaveHudLogic.Clock(0f));
            Assert.AreEqual("01:00", WaveHudLogic.Clock(60f));
            Assert.AreEqual("--:--", WaveHudLogic.Clock(-1f));
        }

        [Test]
        public void CountdownOnlyAtTheEndOfTheAnnounce()
        {
            Assert.AreEqual(0, WaveHudLogic.Countdown(WavePhase.Announce, 3.5f, 2f));
            Assert.AreEqual(2, WaveHudLogic.Countdown(WavePhase.Announce, 1.6f, 2f));
            Assert.AreEqual(1, WaveHudLogic.Countdown(WavePhase.Announce, 0.4f, 2f));
            Assert.AreEqual(0, WaveHudLogic.Countdown(WavePhase.Combat, 0.4f, 2f));
            Assert.AreEqual(0, WaveHudLogic.Countdown(WavePhase.Announce, 1f, 0f));
        }

        [Test]
        public void TitlesAndHype()
        {
            Assert.AreEqual("WAVE 2", WaveHudLogic.AnnounceTitle(2, false, ""));
            Assert.AreEqual("FINAL WAVE", WaveHudLogic.AnnounceTitle(5, true, null));
            Assert.AreEqual("ボス", WaveHudLogic.AnnounceTitle(5, true, "ボス"));
            Assert.AreEqual("WAVE 1", WaveHudLogic.AnnounceTitle(1, true, ""), "1つしかない計画では FINAL にしない");
            Assert.IsTrue(WaveHudLogic.NearGoal(WavePhase.Combat, 5, 5));
            Assert.IsFalse(WaveHudLogic.NearGoal(WavePhase.Combat, 6, 5));
            Assert.IsFalse(WaveHudLogic.NearGoal(WavePhase.Combat, 0, 5));
            Assert.IsFalse(WaveHudLogic.NearGoal(WavePhase.Combat, 3, 0), "0 なら出さない");
            Assert.IsTrue(WaveHudLogic.ShowGo(WavePhase.Combat, 0.2f, 0.9f));
            Assert.IsFalse(WaveHudLogic.ShowGo(WavePhase.Combat, 2f, 0.9f));
            Assert.IsFalse(WaveHudLogic.ShowGo(WavePhase.Combat, 0f, 0f), "0 秒なら出さない");
            Assert.IsTrue(WaveHudLogic.TimeWarning(true, 9f, 10f));
            Assert.IsFalse(WaveHudLogic.TimeWarning(true, 9f, 0f));
            Assert.IsFalse(WaveHudLogic.TimeWarning(false, 9f, 10f), "時計が止まっていれば出さない");
        }

        [Test]
        public void BrushResultListsHealAndCoating()
        {
            Assert.AreEqual("HP +12　コーティング +5", WaveHudLogic.BrushResultText(12.2f, 4.6f, 1f));
            Assert.AreEqual("HP +3", WaveHudLogic.BrushResultText(3f, 0f, 0.3f));
            Assert.AreEqual("みがけなかった…", WaveHudLogic.BrushResultText(0f, 0f, 0f));
        }

        [Test]
        public void ResultSubtitleTellsWhy()
        {
            StringAssert.Contains("WAVE 3", WaveHudLogic.ResultSubtitle(MatchEndReason.TimeUp, 3, 5));
            StringAssert.Contains("コア", WaveHudLogic.ResultSubtitle(MatchEndReason.CoreDestroyed, 0, 0));
            Assert.AreEqual("", WaveHudLogic.ResultSubtitle(MatchEndReason.KillGoal, 0, 0));
        }
    }
}
