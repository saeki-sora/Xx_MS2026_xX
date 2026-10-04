using MS2026.Fortress.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CameraViewAreaTests
    {
        private const float Aspect = 16f / 9f;

        private static CameraViewSettings Ortho(Vector2 center, float size, float roll = 0f)
        {
            var view = CameraViewSettings.Default;
            view.projection = CameraProjection.Orthographic;
            view.center = center;
            view.orthographicSize = size;
            view.rollDegrees = roll;
            return view;
        }

        [Test]
        public void OrthographicView_CoversSizeTimesAspect()
        {
            var rect = CameraViewArea.VisibleRect(Ortho(new Vector2(2f, -1f), 14f), Aspect);

            Assert.AreEqual(2f, rect.center.x, 1e-4f);
            Assert.AreEqual(-1f, rect.center.y, 1e-4f);
            Assert.AreEqual(28f, rect.height, 1e-4f);
            Assert.AreEqual(28f * Aspect, rect.width, 1e-3f);
        }

        [Test]
        public void RolledView_UsesInnerSquare()
        {
            var rect = CameraViewArea.VisibleRect(Ortho(Vector2.zero, 10f, roll: 30f), Aspect);

            // どの角度でも映る、半径10の円に内接する正方形。
            Assert.AreEqual(rect.width, rect.height, 1e-4f);
            Assert.AreEqual(10f * 1.41421356f, rect.width, 1e-3f);
        }

        [Test]
        public void SharedRect_IsIntersectionOfPlayers()
        {
            var players = new[]
            {
                Ortho(new Vector2(-2f, 0f), 5f),
                Ortho(new Vector2(2f, 0f), 5f),
                Ortho(Vector2.zero, 5f),
                Ortho(Vector2.zero, 5f)
            };

            var rect = CameraViewArea.SharedPlayerRect(players, Ortho(Vector2.zero, 20f), 1f);

            Assert.AreEqual(-3f, rect.xMin, 1e-4f);
            Assert.AreEqual(3f, rect.xMax, 1e-4f);
            Assert.AreEqual(10f, rect.height, 1e-4f);
        }

        [Test]
        public void NoCommonArea_FallsBackToOverview()
        {
            var players = new[] { Ortho(new Vector2(-50f, 0f), 2f), Ortho(new Vector2(50f, 0f), 2f) };

            var rect = CameraViewArea.SharedPlayerRect(players, Ortho(Vector2.zero, 20f), 1f);

            Assert.AreEqual(40f, rect.width, 1e-4f);
        }

        [Test]
        public void Inset_ShrinksAndNeverInverts()
        {
            var rect = new Rect(0f, 0f, 10f, 4f);

            var inset = CameraViewArea.Inset(rect, 1f);
            Assert.AreEqual(8f, inset.width, 1e-4f);
            Assert.AreEqual(2f, inset.height, 1e-4f);

            var collapsed = CameraViewArea.Inset(rect, 5f);
            Assert.AreEqual(0f, collapsed.height, 1e-4f);
            Assert.AreEqual(rect.center, collapsed.center);
        }
    }
}
