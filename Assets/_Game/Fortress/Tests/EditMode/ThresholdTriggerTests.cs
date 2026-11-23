using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>しきい値を上へ越えた瞬間に1回だけ知らせ、下がって戻るまで2回は知らせない部品（N8 熱・N9 コアのピンチ）。</summary>
    public sealed class ThresholdTriggerTests
    {
        [Test]
        public void FiresOnceWhenCrossingUp()
        {
            var t = new ThresholdTrigger();
            Assert.That(t.Update(0.5f, 0.8f, 0.1f), Is.False);
            Assert.That(t.Update(0.85f, 0.8f, 0.1f), Is.True);
            Assert.That(t.Update(0.9f, 0.8f, 0.1f), Is.False);
            Assert.That(t.Update(0.95f, 0.8f, 0.1f), Is.False);
        }

        [Test]
        public void DoesNotRearmInsideGap()
        {
            var t = new ThresholdTrigger();
            t.Update(0.85f, 0.8f, 0.1f);
            t.Update(0.75f, 0.8f, 0.1f); // 0.7 より上なので、まだ戻っていない
            Assert.That(t.Update(0.85f, 0.8f, 0.1f), Is.False);
        }

        [Test]
        public void RearmsAfterDroppingBelowGap()
        {
            var t = new ThresholdTrigger();
            t.Update(0.85f, 0.8f, 0.1f);
            t.Update(0.6f, 0.8f, 0.1f);
            Assert.That(t.Update(0.81f, 0.8f, 0.1f), Is.True);
        }

        [Test]
        public void StartingAboveDoesNotFire()
        {
            // 始まったときにもう越えている（Client が途中から見た等）なら鳴らさない。一度下がってから越えたら鳴らす。
            var t = new ThresholdTrigger();
            Assert.That(t.Update(0.9f, 0.8f, 0.1f), Is.False);
            t.Update(0.5f, 0.8f, 0.1f);
            Assert.That(t.Update(0.9f, 0.8f, 0.1f), Is.True);
        }
    }
}
