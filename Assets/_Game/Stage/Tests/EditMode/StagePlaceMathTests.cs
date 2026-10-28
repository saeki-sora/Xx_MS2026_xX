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
        public void SnapGroupLift_SnapsTheLowestObject()
        {
            // 皿（0.12m 浮き）と、その上の石けん（0.13m 浮き）を一緒に 0.1m 下げる → 皿が床に付く量だけ下げる
            Assert.AreEqual(-0.12f, StagePlaceMath.SnapGroupLift(0.12f, -0.1f, snap: false), 1e-5f, "吸着なしでも、いちばん下が床の近くなら床にくっつける");
            Assert.AreEqual(-0.07f, StagePlaceMath.SnapGroupLift(0.12f, -0.06f, snap: true), 1e-5f, "いちばん下が 0.05 刻みになる量");
            Assert.AreEqual(0.2f, StagePlaceMath.SnapGroupLift(0.12f, 0.2f, snap: false), 1e-5f, "床から遠ければそのまま");
        }

        [Test]
        public void ClosestLocalAxis_PicksTheAxisPointingThatWay()
        {
            Assert.AreEqual(0, StagePlaceMath.ClosestLocalAxis(Quaternion.identity, Vector3.right));
            // Maya の向きを床に合わせた物（X まわりに −90°）: 自分の Y がワールドの高さ（Z）の向き
            Assert.AreEqual(1, StagePlaceMath.ClosestLocalAxis(Quaternion.Euler(-90f, 0f, 0f), Vector3.forward));
            // 床の上で 30° 回した物の幅（ワールド X）は、まだ自分の X に近い
            Assert.AreEqual(0, StagePlaceMath.ClosestLocalAxis(Quaternion.Euler(0f, 0f, 30f), Vector3.right));
            // 60° 回すと、自分の Y の方が近い
            Assert.AreEqual(1, StagePlaceMath.ClosestLocalAxis(Quaternion.Euler(0f, 0f, 60f), Vector3.right));
        }

        [Test]
        public void GroupDropDistance_FirstObjectToTouchTheFloorRestsOnIt()
        {
            // 見える床は z=0.33（上は −Z）。皿の底 z=−0.2、その上の石けんの底 z=−0.3 → 皿が床に着くまで 0.53 下げる
            Assert.AreEqual(0.53f, StagePlaceMath.GroupDropDistance(new[] { 0.33f, 0.33f }, new[] { -0.2f, -0.3f }), 1e-5f);
            Assert.AreEqual(-0.1f, StagePlaceMath.GroupDropDistance(new[] { 0f }, new[] { 0.1f }), 1e-5f, "床に沈んでいれば持ち上げる");
            Assert.AreEqual(0f, StagePlaceMath.GroupDropDistance(new float[0], new float[0]), 1e-6f, "物が無ければ動かさない");
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

        [Test]
        public void TiltUp_ZeroIsStraightUpAndAxesLeanAsDocumented()
        {
            AssertVector(Vector3.back, StagePlaceMath.TiltUp(Vector2.zero), "傾き0なら床から上（−Z）");

            var x = StagePlaceMath.TiltUp(new Vector2(30f, 0f));
            Assert.Greater(x.y, 0.4f, "X+ は +Y 側へ倒れる");
            Assert.AreEqual(0f, x.x, 1e-5f);

            var y = StagePlaceMath.TiltUp(new Vector2(0f, 30f));
            Assert.Less(y.x, -0.4f, "Y+ は −X 側へ倒れる");
            Assert.AreEqual(0f, y.y, 1e-5f);
        }

        [Test]
        public void TiltFromUp_InvertsTiltUp()
        {
            foreach (var tilt in new[] { new Vector2(10f, -20f), new Vector2(-45f, 60f), new Vector2(80f, 0f), new Vector2(0f, -170f) })
            {
                var back = StagePlaceMath.TiltFromUp(StagePlaceMath.TiltUp(tilt));
                Assert.AreEqual(tilt.x, back.x, 1e-3f, $"{tilt}");
                Assert.AreEqual(tilt.y, back.y, 1e-3f, $"{tilt}");
            }
        }

        [Test]
        public void TiltFromLean_LeansTowardOffset()
        {
            Assert.AreEqual(Vector2.zero, StagePlaceMath.TiltFromLean(Vector2.zero, 1f), "ずらさなければまっすぐ");

            // 高さ1mのてっぺんを +X へ 1m ずらすと、+X 側へ45°倒れる。
            var up = StagePlaceMath.TiltUp(StagePlaceMath.TiltFromLean(new Vector2(1f, 0f), 1f));
            AssertVector(new Vector3(0.7071f, 0f, -0.7071f), up, "+X へ45°");

            var limited = StagePlaceMath.TiltUp(StagePlaceMath.TiltFromLean(new Vector2(0f, 100f), 1f, 60f));
            Assert.AreEqual(60f, Vector3.Angle(Vector3.back, limited), 0.01f, "倒れる角度は上限まで");
        }

        [Test]
        public void WrapTilt_KeepsAnglesWithinHalfTurn()
        {
            var wrapped = StagePlaceMath.WrapTilt(new Vector2(350f, -190f));
            Assert.AreEqual(-10f, wrapped.x, 1e-3f);
            Assert.AreEqual(170f, wrapped.y, 1e-3f);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"{message}: {actual} (期待 {expected})");
        }
    }
}
