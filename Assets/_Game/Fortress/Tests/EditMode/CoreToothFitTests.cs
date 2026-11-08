using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class CoreToothFitTests
    {
        [Test]
        public void Rotation_MayaUp_PointsModelUpTowardCamera()
        {
            // Maya の「上」(+Y) は、ゲームの「上」(−Z、カメラ側) になる。
            var up = CoreToothFit.Rotation(true, 0f) * Vector3.up;
            Assert.That(Vector3.Distance(up, Vector3.back), Is.LessThan(1e-4f));
        }

        [Test]
        public void Rotation_WithoutMayaUp_KeepsModel()
        {
            var up = CoreToothFit.Rotation(false, 0f) * Vector3.up;
            Assert.That(Vector3.Distance(up, Vector3.up), Is.LessThan(1e-4f));
        }

        [Test]
        public void Rotation_Yaw_TurnsAroundFloorNormal()
        {
            var side = CoreToothFit.Rotation(true, 90f) * Vector3.right;
            Assert.That(Vector3.Distance(side, Vector3.up), Is.LessThan(1e-4f));
        }

        [Test]
        public void Rotation_TiltForward_LeansTopTowardScreenUp()
        {
            var up = CoreToothFit.Rotation(true, 0f, 30f, 0f) * Vector3.up;
            Assert.That(up.y, Is.GreaterThan(0.4f));
            Assert.That(up.z, Is.LessThan(0f));
            Assert.That(Mathf.Abs(up.x), Is.LessThan(1e-4f));
        }

        [Test]
        public void Rotation_TiltSide_LeansTopTowardRight()
        {
            var up = CoreToothFit.Rotation(true, 0f, 0f, 30f) * Vector3.up;
            Assert.That(up.x, Is.GreaterThan(0.4f));
            Assert.That(up.z, Is.LessThan(0f));
            Assert.That(Mathf.Abs(up.y), Is.LessThan(1e-4f));
        }

        [Test]
        public void Place_AddsOffsetAndKeepsBottomOnFloor()
        {
            var bounds = new Bounds(new Vector3(0.3f, -0.2f, -5f), new Vector3(1f, 1f, 2f));
            var position = CoreToothFit.Place(bounds, 2f, 0f, new Vector2(0.5f, -0.25f));
            var center = bounds.center * 2f + position;
            Assert.That(center.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(center.y, Is.EqualTo(-0.25f).Within(1e-5f));
            Assert.That(bounds.max.z * 2f + position.z, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ScaleFor_UsesUprightFootprint()
        {
            var upright = new Bounds(Vector3.zero, new Vector3(0.02f, 0.01f, 0.011f));
            Assert.That(CoreToothFit.ScaleFor(upright, 2.5f) * 0.02f, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void Solve_ScalesLongerFootprintSideToWidth()
        {
            // 床の上で 0.02 × 0.01、高さ 0.011 の外形を、幅 2.5 にする。
            var bounds = new Bounds(new Vector3(0f, 0f, -0.0725f), new Vector3(0.02f, 0.01f, 0.011f));
            CoreToothFit.Solve(bounds, 2.5f, 0f, out var scale, out _);
            Assert.That(scale * 0.02f, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void Solve_CentersOnCoreAndPutsBottomOnFloor()
        {
            var bounds = new Bounds(new Vector3(0.3f, -0.2f, -5f), new Vector3(1f, 1f, 2f));
            CoreToothFit.Solve(bounds, 1f, 0f, out var scale, out var position);
            var center = bounds.center * scale + position;
            var bottom = bounds.max.z * scale + position.z;
            Assert.That(center.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(center.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(bottom, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Solve_SinkMovesBottomBelowFloor()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            CoreToothFit.Solve(bounds, 1f, 0.2f, out var scale, out var position);
            Assert.That(bounds.max.z * scale + position.z, Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void Solve_EmptyBounds_DoesNotDivideByZero()
        {
            CoreToothFit.Solve(new Bounds(Vector3.zero, Vector3.zero), 2.5f, 0f, out var scale, out var position);
            Assert.That(scale, Is.EqualTo(1f));
            Assert.That(float.IsNaN(position.x), Is.False);
        }

        [Test]
        public void PatternFrame_HeightIsMeasuredAlongUp()
        {
            var bounds = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 2f, 8f));
            CoreToothFit.PatternFrame(bounds, Vector3.up, out var center, out var invHeight);
            Assert.That(center, Is.EqualTo(bounds.center));
            Assert.That(invHeight, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void RingRadius_KeepsSettingForSmallCore()
        {
            // 今の丸い絵と同じ幅 2.5m（半径 1.25）なら、設定の 3.5m のまま。
            Assert.That(CoreToothFit.RingRadius(3.5f, 1.25f), Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void RingRadius_GrowsForBigTooth()
        {
            Assert.That(CoreToothFit.RingRadius(3.5f, 2.4f), Is.GreaterThan(3.5f));
            Assert.That(CoreToothFit.RingRadius(0.1f, 0f), Is.EqualTo(0.5f));
        }

        [Test]
        public void FacingLocal_TurnsLike1PSeatForEachSeat()
        {
            // 2P の席（画面の回転 -90）: 1P の画面の上（+Y）が、2P の画面の上（+X）に来る。
            var p2 = CoreToothFit.FacingLocal(Quaternion.identity, -90f);
            AssertVector(Vector3.right, p2 * Vector3.up);

            // 3P の席（-180）: 上下が逆。4P の席（90）: 1P の上が -X。
            AssertVector(Vector3.down, CoreToothFit.FacingLocal(Quaternion.identity, -180f) * Vector3.up);
            AssertVector(Vector3.left, CoreToothFit.FacingLocal(Quaternion.identity, 90f) * Vector3.up);

            // 床の軸（Z）は変わらない（めり込み・根元の向きはそのまま）。
            AssertVector(Vector3.forward, p2 * Vector3.forward);
        }

        [Test]
        public void FacingLocal_TurnsAroundWorldZEvenWhenParentIsRotated()
        {
            var parent = Quaternion.Euler(10f, 20f, 30f);
            var local = CoreToothFit.FacingLocal(parent, -90f);
            var world = parent * local;
            var expected = Quaternion.AngleAxis(-90f, Vector3.forward) * parent;
            Assert.Less(Quaternion.Angle(expected, world), 0.01f);
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"expected {expected} but was {actual}");
        }
    }
}
