using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class UiScreenStackTests
    {
        [Test]
        public void Back_ClosesTheFrontMostClosableScreen()
        {
            var stack = new UiScreenStack();
            stack.Push("Hud", "HUD", 100, false, false);
            stack.Push("Pause", "Popup", 300, false, true);
            stack.Push("Lobby", "Menu", 200, true, true);

            Assert.AreEqual("Pause", stack.TopForBack(), "手前の段（ポップアップ）が先");
            stack.Remove("Pause");
            Assert.AreEqual("Lobby", stack.TopForBack());
            stack.Remove("Lobby");
            Assert.IsNull(stack.TopForBack(), "HUDは戻るで閉じない");
        }

        [Test]
        public void OneAtATime_ReturnsScreensToClose()
        {
            var stack = new UiScreenStack();
            stack.Push("Title", "Menu", 200, true, true);
            var toClose = stack.Push("Lobby", "Menu", 200, true, true);

            CollectionAssert.AreEqual(new[] { "Title" }, toClose);
            Assert.IsFalse(stack.IsOpen("Title"));
            Assert.IsTrue(stack.IsOpen("Lobby"));
        }

        [Test]
        public void ReopeningMovesToFront()
        {
            var stack = new UiScreenStack();
            stack.Push("A", "Popup", 300, false, true);
            stack.Push("B", "Popup", 300, false, true);
            stack.Push("A", "Popup", 300, false, true);

            Assert.AreEqual("A", stack.TopForBack());
            Assert.AreEqual(2, stack.Open.Count);
        }
    }

    public sealed class UiTransitionTimelineTests
    {
        [Test]
        public void CoversHoldsUntilReadyThenReveals()
        {
            var timeline = new UiTransitionTimeline();
            timeline.Start(0.5f, 0.1f, 0.5f);

            var (covered, finished) = timeline.Tick(0.25f, true);
            Assert.IsFalse(covered);
            Assert.AreEqual(0.5f, timeline.Progress, 1e-4f);

            (covered, _) = timeline.Tick(0.3f, true);
            Assert.IsTrue(covered, "覆い終わった瞬間を知らせる");
            Assert.AreEqual(UiTransitionTimeline.Phase.Holding, timeline.Current);

            timeline.Tick(1f, false);
            Assert.AreEqual(UiTransitionTimeline.Phase.Holding, timeline.Current, "準備ができるまで開かない");

            timeline.Tick(0.01f, true);
            Assert.AreEqual(UiTransitionTimeline.Phase.Revealing, timeline.Current);

            (_, finished) = timeline.Tick(0.6f, true);
            Assert.IsTrue(finished);
            Assert.AreEqual(0f, timeline.Progress, 1e-4f);
            Assert.IsFalse(timeline.IsRunning);
        }
    }

    public sealed class UiValuesTests
    {
        [Test]
        public void Subscribe_GetsCurrentAndChangesOnlyWhenDifferent()
        {
            var calls = 0;
            var last = 0f;
            UiValues.Set("test.a", 1f);
            using (UiValues.Subscribe("test.a", v => { calls++; last = v.AsNumber; }))
            {
                Assert.AreEqual(1, calls, "今の値ですぐ1回");
                UiValues.Set("test.a", 1f);
                Assert.AreEqual(1, calls, "同じ値なら呼ばない");
                UiValues.Set("test.a", 2f);
                Assert.AreEqual(2, calls);
                Assert.AreEqual(2f, last);
            }

            UiValues.Set("test.a", 3f);
            Assert.AreEqual(2, calls, "Dispose 後は呼ばない");
        }

        [Test]
        public void Format_UsesStringFormat()
        {
            Assert.AreEqual("70%", UiValue.Of(70f).Format("{0:0}%"));
            Assert.AreEqual("ON", UiValue.Of(true).Format(null));
            Assert.AreEqual("P2", UiValue.Of("P2").Format("{0}"));
            Assert.AreEqual("壊れた書式", UiValue.Of(1f).Format("壊れた書式"));
        }

        [Test]
        public void AsBool_AndAsNumber()
        {
            Assert.IsTrue(UiValue.Of(0.6f).AsBool);
            Assert.IsFalse(UiValue.Of(0.4f).AsBool);
            Assert.AreEqual(12.5f, UiValue.Of("12.5").AsNumber);
            Assert.IsFalse(UiValue.Of("").AsBool);
        }
    }
}
