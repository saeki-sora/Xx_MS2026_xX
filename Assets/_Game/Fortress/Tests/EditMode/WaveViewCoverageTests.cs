using MS2026.Fortress.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class WaveViewCoverageTests
    {
        private static CameraViewSettings Ortho(Vector2 center, float halfHeight, float roll = 0f)
        {
            var view = CameraViewSettings.Default;
            view.center = center;
            view.projection = CameraProjection.Orthographic;
            view.orthographicSize = halfHeight;
            view.rollDegrees = roll;
            view.tiltDegrees = 0f;
            return view;
        }

        [Test]
        public void VisibleInAnyView()
        {
            var coverage = new WaveViewCoverage();
            coverage.Set(new[] { Ortho(new Vector2(0f, 0f), 10f), Ortho(new Vector2(40f, 0f), 10f) }, 16f / 9f);
            Assert.IsTrue(coverage.IsVisible(new Vector2(17f, 9f), 0f), "1人目の画面（横 ±17.8）");
            Assert.IsTrue(coverage.IsVisible(new Vector2(40f, -9f), 0f), "2人目の画面");
            Assert.IsFalse(coverage.IsVisible(new Vector2(0f, 12f), 0f), "どの画面にも入っていない");
            Assert.IsTrue(coverage.IsVisible(new Vector2(0f, 11f), 1.5f), "余白の中なら映っている扱い");
        }

        [Test]
        public void RolledViewSwapsWidthAndHeight()
        {
            var coverage = new WaveViewCoverage();
            coverage.Set(new[] { Ortho(Vector2.zero, 10f, 90f) }, 16f / 9f);
            Assert.IsTrue(coverage.IsVisible(new Vector2(0f, 17f), 0f), "90度回した画面は、縦に長く映る");
            Assert.IsFalse(coverage.IsVisible(new Vector2(17f, 0f), 0f));
        }

        [Test]
        public void NoViewsMeansNothingIsVisible()
        {
            var coverage = new WaveViewCoverage();
            coverage.Set((CameraViewPreset)null, 1.7f);
            Assert.IsFalse(coverage.IsVisible(Vector2.zero, 0f));
        }
    }
}
