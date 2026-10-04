using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CameraViewMathTests
    {
        private const float Aspect = 16f / 9f;
        private const float Tolerance = 1e-3f;
        private static readonly Rect FullScreen = new Rect(0f, 0f, 1f, 1f);

        private static CameraViewSettings Ortho(Vector2 center, float size, float roll = 0f)
        {
            var view = CameraViewSettings.Default;
            view.center = center;
            view.orthographicSize = size;
            view.rollDegrees = roll;
            return view;
        }

        [Test]
        public void 中心の点はビューポートの中央に映る()
        {
            var view = Ortho(new Vector2(3f, -2f), 5f, 37f);

            Assert.IsTrue(CameraViewMath.TryWorldToViewport(view, Aspect, new Vector3(3f, -2f, 0f), out var viewport));
            Assert.AreEqual(0.5f, viewport.x, Tolerance);
            Assert.AreEqual(0.5f, viewport.y, Tolerance);
        }

        [Test]
        public void 正投影の上端はOrthographicSizeの距離にある()
        {
            var view = Ortho(Vector2.zero, 5f);

            Assert.IsTrue(CameraViewMath.TryWorldToViewport(view, Aspect, new Vector3(0f, 5f, 0f), out var viewport));
            Assert.AreEqual(1f, viewport.y, Tolerance);
        }

        [Test]
        public void 回転90度では右にある物が画面の下に映る()
        {
            var view = Ortho(Vector2.zero, 5f, 90f);

            Assert.IsTrue(CameraViewMath.TryWorldToViewport(view, Aspect, new Vector3(4f, 0f, 0f), out var viewport));
            Assert.AreEqual(0.5f, viewport.x, Tolerance);
            Assert.Less(viewport.y, 0.5f);
        }

        [TestCase(0f, -10f, 0f)]
        [TestCase(10f, 0f, 90f)]
        [TestCase(0f, 10f, 180f)]
        [TestCase(-10f, 0f, -90f)]
        public void 砲台を画面下に置く回転角(float x, float y, float expected)
        {
            var roll = CameraViewMath.RollToPlaceAtBottom(Vector2.zero, new Vector2(x, y));

            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, roll), Tolerance);
        }

        [Test]
        public void 砲台を画面下に置く回転を適用すると砲台は画面の下中央に映る()
        {
            var turret = new Vector2(6f, 6f);
            var view = Ortho(Vector2.zero, 12f, CameraViewMath.RollToPlaceAtBottom(Vector2.zero, turret));

            Assert.IsTrue(CameraViewMath.TryWorldToViewport(view, Aspect, turret, out var viewport));
            Assert.AreEqual(0.5f, viewport.x, Tolerance);
            Assert.Less(viewport.y, 0.5f);
        }

        [Test]
        public void 傾けた透視投影でも地面との往復変換が一致する()
        {
            var view = CameraViewSettings.Default.WithProjection(CameraProjection.Perspective);
            view.center = new Vector2(1f, 2f);
            view.tiltDegrees = 30f;
            view.rollDegrees = 45f;

            var sample = new Vector2(0.3f, 0.7f);
            Assert.IsTrue(CameraViewMath.TryViewportToGround(view, Aspect, sample, out var ground));
            Assert.IsTrue(CameraViewMath.TryWorldToViewport(view, Aspect, ground, out var back));
            Assert.AreEqual(sample.x, back.x, Tolerance);
            Assert.AreEqual(sample.y, back.y, Tolerance);
        }

        [Test]
        public void 地平線より上を映すと地面の四隅が取れない()
        {
            var view = CameraViewSettings.Default.WithProjection(CameraProjection.Perspective);
            view.fieldOfView = 120f;
            view.tiltDegrees = 70f;

            Assert.IsFalse(CameraViewMath.GetGroundQuad(view, Aspect, new Vector3[4]));
        }

        [Test]
        public void 自動フィットで全ての点が安全域に収まる()
        {
            var points = new List<Vector2> { new Vector2(-10f, 3f), new Vector2(8f, -6f), new Vector2(2f, 12f) };
            var fitted = CameraViewMath.FitToPoints(Ortho(Vector2.zero, 1f, 30f), Aspect, points, 0.9f, 0f);
            var safe = new Rect(0.05f, 0.05f, 0.9f, 0.9f);

            foreach (var point in points)
            {
                Assert.IsTrue(CameraViewMath.IsVisible(fitted, Aspect, point, new Rect(safe.x - Tolerance, safe.y - Tolerance, safe.width + Tolerance * 2f, safe.height + Tolerance * 2f)), point.ToString());
            }

            Assert.AreEqual(30f, fitted.rollDegrees, Tolerance);
        }

        [Test]
        public void 投影方式を切り替えても映る広さは変わらない()
        {
            var ortho = Ortho(Vector2.zero, 8f);
            var perspective = ortho.WithProjection(CameraProjection.Perspective);

            Assert.AreEqual(CameraProjection.Perspective, perspective.projection);
            Assert.AreEqual(ortho.VisibleHalfHeight, perspective.VisibleHalfHeight, Tolerance);
            Assert.AreEqual(8f, perspective.WithProjection(CameraProjection.Orthographic).orthographicSize, Tolerance);
        }

        [Test]
        public void 補間は両端で元の視点に一致し回転は近い向きに回る()
        {
            var from = Ortho(Vector2.zero, 5f, 170f);
            var to = Ortho(new Vector2(10f, 0f), 15f, -170f);

            Assert.IsTrue(CameraViewSettings.Lerp(from, to, 0f).Approximately(from));
            Assert.IsTrue(CameraViewSettings.Lerp(from, to, 1f).Approximately(to));

            var middle = CameraViewSettings.Lerp(from, to, 0.5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, middle.rollDegrees), Tolerance);
            Assert.AreEqual(10f, middle.orthographicSize, Tolerance);
        }

        [Test]
        public void 範囲外の値は丸められる()
        {
            var view = CameraViewSettings.Default;
            view.orthographicSize = -3f;
            view.fieldOfView = 500f;
            view.rollDegrees = 270f;

            var sanitized = view.Sanitized();
            Assert.AreEqual(CameraViewSettings.MinOrthographicSize, sanitized.orthographicSize, Tolerance);
            Assert.AreEqual(CameraViewSettings.MaxFieldOfView, sanitized.fieldOfView, Tolerance);
            Assert.AreEqual(-90f, sanitized.rollDegrees, Tolerance);
        }
    }
}
