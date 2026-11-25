using NUnit.Framework;

namespace MS2026.SoundStudio.Tests
{
    /// <summary>鳴った回数の数え方（5）と、音の大きさの計算（6）。</summary>
    public sealed class SoundBalanceMathTests
    {
        [Test]
        public void PlayStats_CountsTotalAndMaxPerSecond()
        {
            var stats = new SoundPlayStats();
            stats.Record("a", 0.0);
            stats.Record("a", 0.2);
            stats.Record("a", 0.5);
            stats.Record("a", 2.0);
            Assert.That(stats.Total("a"), Is.EqualTo(4));
            Assert.That(stats.MaxPerSecond("a"), Is.EqualTo(3));
        }

        [Test]
        public void PlayStats_OneSecondWindowSlides()
        {
            var stats = new SoundPlayStats();
            stats.Record("a", 0.0);
            stats.Record("a", 0.9);
            stats.Record("a", 1.5); // 0.0 は 1 秒より前なので数えない
            Assert.That(stats.MaxPerSecond("a"), Is.EqualTo(2));
        }

        [Test]
        public void PlayStats_UnknownKeyIsZero_AndClearResets()
        {
            var stats = new SoundPlayStats();
            Assert.That(stats.Total("x"), Is.EqualTo(0));
            stats.Record("x", 0);
            stats.Clear();
            Assert.That(stats.Total("x"), Is.EqualTo(0));
            Assert.That(stats.Keys, Is.Empty);
        }

        [Test]
        public void ToDb_KnownValues()
        {
            Assert.That(LoudnessMath.ToDb(1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(LoudnessMath.ToDb(0.5f), Is.EqualTo(-6.0206f).Within(1e-3f));
            Assert.That(LoudnessMath.ToDb(0f), Is.EqualTo(LoudnessMath.Silence));
        }

        [Test]
        public void RmsAndPeak()
        {
            var samples = new[] { 0.5f, -0.5f, 0.5f, -0.5f };
            Assert.That(LoudnessMath.Rms(samples, samples.Length), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(LoudnessMath.Peak(new[] { 0.1f, -0.8f, 0.3f }, 3), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(LoudnessMath.Rms(new float[0], 0), Is.EqualTo(0f));
        }

        [Test]
        public void Median_OddAndEven()
        {
            Assert.That(LoudnessMath.Median(new[] { 3f, 1f, 2f }), Is.EqualTo(2f));
            Assert.That(LoudnessMath.Median(new[] { 4f, 1f, 2f, 3f }), Is.EqualTo(2.5f));
            Assert.That(LoudnessMath.Median(new float[0]), Is.EqualTo(0f));
        }

        [TestCase(-10f, -20f, ExpectedResult = LoudnessMark.Loud)]
        [TestCase(-30f, -20f, ExpectedResult = LoudnessMark.Quiet)]
        [TestCase(-17f, -20f, ExpectedResult = LoudnessMark.Normal)]
        [TestCase(-14f, -20f, ExpectedResult = LoudnessMark.Loud)] // ちょうど 6 dB 上は「大きい」
        public LoudnessMark Classify(float db, float median)
        {
            return LoudnessMath.Classify(db, median, 6f);
        }

        [Test]
        public void WithVolume_AddsDataVolumeInDb()
        {
            Assert.That(LoudnessMath.WithVolume(-20f, 0.5f), Is.EqualTo(-26.0206f).Within(1e-3f));
            Assert.That(LoudnessMath.WithVolume(-20f, 0f), Is.EqualTo(LoudnessMath.Silence));
        }
    }
}
