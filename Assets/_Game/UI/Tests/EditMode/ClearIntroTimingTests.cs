using NUnit.Framework;
using MS2026.UI.Game;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class ClearIntroTimingTests
    {
        [Test]
        public void MatchesRequestedMilestones()
        {
            Assert.AreEqual(0f, ClearIntroTiming.Zoom(0f));
            Assert.AreEqual(1f, ClearIntroTiming.Zoom(0.8f));
            Assert.AreEqual(0f, ClearIntroTiming.Fill(2.9f));
            Assert.AreEqual(1.8f, ClearIntroTiming.Covered - ClearIntroTiming.FillStart, 0.001f);
            Assert.AreEqual(1f, ClearIntroTiming.Fill(4.7f));
            Assert.AreEqual(0f, ClearIntroTiming.Open(4.7f), "ひと呼吸は全面を覆う");
            Assert.AreEqual(0f, ClearIntroTiming.Open(5.5f));
            Assert.AreEqual(1f, ClearIntroTiming.Open(6.9f));
            Assert.AreEqual(0f, ClearIntroTiming.Noise(9.3f));
            Assert.AreEqual(1f, ClearIntroTiming.Noise(10.7f));
            Assert.AreEqual(0f, ClearIntroTiming.CoreSlide(ClearIntroTiming.CoreSlideStart));
            Assert.That(ClearIntroTiming.CoreSlide(ClearIntroTiming.CoreSlideStart + 0.525f),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.AreEqual(1f, ClearIntroTiming.CoreSlide(ClearIntroTiming.Finished));
        }

        [Test]
        public void FoamRisesFromBottomAndPopsFromCenter()
        {
            Assert.AreEqual(0f, ClearFoamMotion.Rise(2.8f, 0, 0));
            Assert.Greater(ClearFoamMotion.Rise(3.1f, 0, 0), ClearFoamMotion.Rise(3.1f, 5, 35));
            for (var row = 0; row < 12; row++)
                Assert.AreEqual(1f, ClearFoamMotion.Rise(4.7f, row, row * 18));
            Assert.AreEqual(0f, ClearFoamMotion.Pop(5.5f, UnityEngine.Vector2.zero));
            Assert.Greater(ClearFoamMotion.Pop(5.7f, UnityEngine.Vector2.zero),
                ClearFoamMotion.Pop(5.7f, UnityEngine.Vector2.one));
            Assert.AreEqual(1f, ClearFoamMotion.Pop(6.9f, UnityEngine.Vector2.one));
        }

        [Test]
        public void BubblesBeginFullyBelowScreenAndSettleWithoutJumping()
        {
            var size = new UnityEngine.Vector2(1920f, 1080f);
            for (var i = 0; i < 504; i++)
            {
                var start = ClearFoamMotion.RisingPosition(ClearIntroTiming.FillStart, size, i, 28, 18);
                var radius = ClearFoamMotion.Diameter(size, i, 28, 18) * 0.5f;
                Assert.LessOrEqual(start.y + radius, -size.y * 0.5f);
                var end = ClearFoamMotion.RisingPosition(ClearIntroTiming.Covered, size, i, 28, 18);
                var target = ClearFoamMotion.FilledPosition(size, i, 28, 18, ClearIntroTiming.Covered);
                Assert.That((end - target).magnitude, Is.LessThan(0.01f));
                for (var t = ClearIntroTiming.FillStart; t < ClearIntroTiming.Covered; t += 0.03f)
                {
                    var a = ClearFoamMotion.RisingPosition(t, size, i, 28, 18);
                    var b = ClearFoamMotion.RisingPosition(t + 0.001f, size, i, 28, 18);
                    Assert.That((a - b).magnitude, Is.LessThan(5f), "上昇中の段替わりで位置が飛ばない");
                }
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1440f, 1080f)]
        [TestCase(2560f, 1080f)]
        public void FilledFoamHasNoGapsAcrossViewport(float width, float height)
        {
            var size = new UnityEngine.Vector2(width, height);
            const int columns = 28, rows = 18;
            foreach (var time in new[] { 4.7f, 5f, 5.49f })
            {
                var centers = new UnityEngine.Vector2[columns * rows];
                var radiusSquared = new float[centers.Length];
                for (var i = 0; i < centers.Length; i++)
                {
                    centers[i] = ClearFoamMotion.FilledPosition(size, i, columns, rows, time);
                    var radius = ClearFoamMotion.Diameter(size, i, columns, rows) * 0.5f;
                    radiusSquared[i] = radius * radius;
                }
                // 四隅・上下左右の端も含め、実際の泡の円で覆えているかを確認する。
                for (var y = 0; y <= 24; y++)
                for (var x = 0; x <= 40; x++)
                {
                    var point = new UnityEngine.Vector2((x / 40f - 0.5f) * width, (y / 24f - 0.5f) * height);
                    var covered = false;
                    for (var i = 0; i < centers.Length && !covered; i++)
                        covered = (point - centers[i]).sqrMagnitude <= radiusSquared[i];
                    Assert.IsTrue(covered, $"Foam gap at {point}, time={time}, viewport={size}");
                }
            }
        }
    }
}

