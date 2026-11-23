using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>数がふえはじめた瞬間（まとまりの最初）だけ知らせる部品（N11 敵が出てくる）。</summary>
    public sealed class IncreaseBurstDetectorTests
    {
        [Test]
        public void FirstSampleDoesNotFire()
        {
            var d = new IncreaseBurstDetector();
            Assert.That(d.Update(50, 0f, 2f), Is.False);
        }

        [Test]
        public void FiresOnFirstIncreaseOnly()
        {
            var d = new IncreaseBurstDetector();
            d.Update(0, 0f, 2f);
            Assert.That(d.Update(3, 1f, 2f), Is.True);
            Assert.That(d.Update(6, 1.5f, 2f), Is.False);
            Assert.That(d.Update(9, 3f, 2f), Is.False); // 1.5 秒前にふえたので、まだ同じまとまり
        }

        [Test]
        public void FiresAgainAfterQuietGap()
        {
            var d = new IncreaseBurstDetector();
            d.Update(0, 0f, 2f);
            d.Update(3, 1f, 2f);
            d.Update(1, 2f, 2f); // 減っただけ
            Assert.That(d.Update(4, 3.5f, 2f), Is.True);
        }

        [Test]
        public void DecreaseNeverFires()
        {
            var d = new IncreaseBurstDetector();
            d.Update(10, 0f, 2f);
            Assert.That(d.Update(5, 5f, 2f), Is.False);
        }
    }
}
