using MS2026.CharacterSelect;
using NUnit.Framework;

namespace MS2026.CharacterSelect.Tests.EditMode
{
    public sealed class PasteCurtainFrameTimingTests
    {
        // 2026-10-22 の連番: 60枚・30fps、24コマ目で覆いきり、32コマ目から抜ける。
        private static PasteCurtainFrameTiming Delivered() => new PasteCurtainFrameTiming(60, 30f, 24, 32);

        [Test]
        public void Seconds_FollowTheFrameNumbers()
        {
            var timing = Delivered();
            Assert.AreEqual(0.8f, timing.CoverSeconds, 1e-4f);
            Assert.AreEqual(7f / 30f, timing.HoldSeconds, 1e-4f, "24〜31コマを1回出し終えるまで");
            Assert.AreEqual(28f / 30f, timing.RevealSeconds, 1e-4f);
        }

        [Test]
        public void Cover_StopsAtTheCoveredFrame()
        {
            var timing = Delivered();
            Assert.AreEqual(0, timing.CoverFrameAt(0f));
            Assert.AreEqual(15, timing.CoverFrameAt(0.5f + 1e-4f));
            Assert.AreEqual(24, timing.CoverFrameAt(5f));
        }

        [Test]
        public void Hold_StopsOneBeforeTheRevealFrame()
        {
            var timing = Delivered();
            Assert.AreEqual(24, timing.HoldFrameAt(0f));
            Assert.AreEqual(31, timing.HoldFrameAt(10f), "読み込みが長くても、抜け始めるコマの1つ前で止めて待つ");
        }

        [Test]
        public void Reveal_EndsWithMinusOneAfterTheLastFrame()
        {
            var timing = Delivered();
            Assert.AreEqual(32, timing.RevealFrameAt(0f));
            Assert.AreEqual(59, timing.RevealFrameAt(27f / 30f + 1e-4f));
            Assert.AreEqual(-1, timing.RevealFrameAt(1f));
        }

        [Test]
        public void OutOfRangeSettings_AreClamped()
        {
            var timing = new PasteCurtainFrameTiming(10, 0f, 50, 2);
            Assert.AreEqual(1f, timing.FrameRate);
            Assert.AreEqual(9, timing.CoveredFrame, "覆いきるコマは最後のコマまで");
            Assert.AreEqual(9, timing.RevealFrame, "抜け始めは覆いきった後");
            Assert.AreEqual(9, timing.HoldFrameAt(3f));
        }

        [Test]
        public void FewerThanTwoFrames_IsNotUsable()
        {
            Assert.IsFalse(new PasteCurtainFrameTiming(1, 30f, 0, 0).IsUsable);
            Assert.IsFalse(new PasteCurtainFrameTiming(0, 30f, 24, 32).IsUsable);
            Assert.IsTrue(Delivered().IsUsable);
        }
    }
}
