using System.Collections.Generic;
using MS2026.Stage.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StagePlaceMathTests
    {
        [Test]
        public void SnapToGrid_RoundsToNearestStep()
        {
            Assert.AreEqual(new Vector2(0.25f, -0.5f), StagePlaceMath.SnapToGrid(new Vector2(0.3f, -0.6f), 0.25f));
            Assert.AreEqual(0.33f, StagePlaceMath.SnapValue(0.33f, 0f), 1e-6f, "刻み0なら吸着しない");
        }

        [Test]
        public void EdgeSnap_TouchesNeighbourEdge()
        {
            var moving = Rect.MinMaxRect(2.1f, 0f, 3.1f, 1f);
            var other = Rect.MinMaxRect(0f, 0f, 2f, 1f);
            var result = StagePlaceMath.EdgeSnap(moving, new List<Rect> { other }, 0.2f);

            Assert.IsTrue(result.SnappedX);
            Assert.AreEqual(-0.1f, result.Offset.x, 1e-5f, "左端が相手の右端にくっつく");
            Assert.AreEqual(2f, result.GuideX, 1e-5f);
        }

        [Test]
        public void EdgeSnap_IgnoresFarAwayObjects()
        {
            var moving = Rect.MinMaxRect(2.1f, 10f, 3.1f, 11f);
            var other = Rect.MinMaxRect(0f, 0f, 2f, 1f);
            var result = StagePlaceMath.EdgeSnap(moving, new List<Rect> { other }, 0.2f);

            Assert.IsFalse(result.SnappedX, "縦に遠く離れた物の端にはそろえない");
        }

        [Test]
        public void ProjectToFloor_TallPointShiftsAwayFromCamera()
        {
            // カメラは原点の真上10m（z=-10）。(2,0) の真上 2m の点は、床では外側（+x）へずれて見える。
            var camera = new Vector3(0f, 0f, -10f);
            Assert.IsTrue(StagePlaceMath.ProjectToFloor(new Vector3(2f, 0f, -2f), camera, Vector3.forward, false, out var floor));
            Assert.AreEqual(2.5f, floor.x, 1e-4f);
            Assert.AreEqual(0f, floor.y, 1e-4f);

            Assert.IsTrue(StagePlaceMath.ProjectToFloor(new Vector3(2f, 0f, -2f), camera, Vector3.forward, true, out var ortho));
            Assert.AreEqual(2f, ortho.x, 1e-4f, "真上から遠近なしならずれない");
        }

        [Test]
        public void ConvexHull_DropsInnerPoints()
        {
            var points = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f) };
            var hull = StagePlaceMath.ConvexHull(points);

            Assert.AreEqual(4, hull.Count);
            Assert.IsFalse(hull.Contains(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void DistanceToPolygon_ZeroInsideAndEdgeDistanceOutside()
        {
            var square = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

            Assert.AreEqual(0f, StagePlaceMath.DistanceToPolygon(square, new Vector2(0.5f, 0.5f), out _), 1e-5f);
            Assert.AreEqual(2f, StagePlaceMath.DistanceToPolygon(square, new Vector2(3f, 0.5f), out var closest), 1e-5f);
            Assert.AreEqual(new Vector2(1f, 0.5f), closest);
        }
    }
}
