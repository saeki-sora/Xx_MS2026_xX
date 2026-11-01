using MS2026.Fortress;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class FortressSeatMathTests
    {
        // ステージ1: P1 上, P2 右, P3 下, P4 左（コアは真ん中）。
        private static readonly Vector2[] Ring = { new Vector2(0f, 7f), new Vector2(7f, 0f), new Vector2(0f, -7f), new Vector2(-7f, 0f) };

        // ステージ2（お風呂場）: P1 左上, P2 右上, P3 左下, P4 右下。
        private static readonly Vector2[] Corners = { new Vector2(-13f, 4f), new Vector2(15f, 4f), new Vector2(-14f, -9.5f), new Vector2(14f, -9.5f) };

        [Test]
        public void ClockwiseOrder_Ring_IsNumberOrder()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, FortressSeatMath.ClockwiseOrder(Ring, Vector2.zero));
        }

        [Test]
        public void ClockwiseOrder_Corners_GoesAroundNotZigzag()
        {
            // 左上 → 右上 → 右下（席 3）→ 左下（席 2）
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 2 }, FortressSeatMath.ClockwiseOrder(Corners, new Vector2(0f, -2.5f)));
        }

        [Test]
        public void SeatOf_Clockwise_MovesOneSeatPerShift()
        {
            var order = FortressSeatMath.ClockwiseOrder(Corners, new Vector2(0f, -2.5f));
            Assert.AreEqual(0, FortressSeatMath.SeatOf(0, 0, order, true));
            Assert.AreEqual(1, FortressSeatMath.SeatOf(0, 1, order, true)); // P1: 左上 → 右上
            Assert.AreEqual(3, FortressSeatMath.SeatOf(1, 1, order, true)); // P2: 右上 → 右下
            Assert.AreEqual(2, FortressSeatMath.SeatOf(3, 1, order, true)); // P4: 右下 → 左下
            Assert.AreEqual(0, FortressSeatMath.SeatOf(2, 1, order, true)); // P3: 左下 → 左上
            Assert.AreEqual(0, FortressSeatMath.SeatOf(0, 4, order, true)); // 4回で一周
        }

        [Test]
        public void SeatOf_CounterClockwise_GoesBackward()
        {
            var order = FortressSeatMath.ClockwiseOrder(Ring, Vector2.zero);
            Assert.AreEqual(3, FortressSeatMath.SeatOf(0, 1, order, false)); // 上 → 左
            Assert.AreEqual(0, FortressSeatMath.SeatOf(1, 1, order, false)); // 右 → 上
        }

        [Test]
        public void SeatOf_EveryShift_SeatsStayUnique()
        {
            var order = FortressSeatMath.ClockwiseOrder(Corners, new Vector2(0f, -2.5f));
            for (var shift = 0; shift < 8; shift++)
            {
                var used = new bool[4];
                for (var p = 0; p < 4; p++)
                {
                    var seat = FortressSeatMath.SeatOf(p, shift, order, true);
                    Assert.IsFalse(used[seat], $"shift {shift}: seat {seat} twice");
                    used[seat] = true;
                }
            }
        }

        [Test]
        public void SeatOf_UnknownPlayer_StaysPut()
        {
            Assert.AreEqual(5, FortressSeatMath.SeatOf(5, 1, new[] { 0, 1, 2, 3 }, true));
            Assert.AreEqual(2, FortressSeatMath.SeatOf(2, 1, null, true));
        }

        [Test]
        public void StepBetween_OneApart_JumpsOtherwiseSnaps()
        {
            Assert.AreEqual(0, FortressSeatMath.StepBetween(1, 1, 4));
            Assert.AreEqual(1, FortressSeatMath.StepBetween(3, 0, 4));
            Assert.AreEqual(-1, FortressSeatMath.StepBetween(0, 3, 4));
            Assert.AreEqual(int.MinValue, FortressSeatMath.StepBetween(0, 2, 4));
        }

        [Test]
        public void Evaluate_ArcsThenSquashesThenRests()
        {
            var start = FortressSeatMath.Evaluate(0f, 0.9f, 0.25f, 0.3f);
            Assert.AreEqual(0f, start.Progress01, 1e-5f);
            Assert.IsFalse(start.Landed);

            var middle = FortressSeatMath.Evaluate(0.45f, 0.9f, 0.25f, 0.3f);
            Assert.AreEqual(0.5f, middle.Progress01, 1e-4f);
            Assert.AreEqual(1f, middle.Height01, 1e-4f);

            var squash = FortressSeatMath.Evaluate(0.9f + 0.15f, 0.9f, 0.25f, 0.3f);
            Assert.IsTrue(squash.Landed);
            Assert.AreEqual(1f, squash.Progress01, 1e-5f);
            Assert.AreEqual(0f, squash.Height01, 1e-5f);
            Assert.Greater(squash.Squash, 0f);

            var rest = FortressSeatMath.Evaluate(1.3f, 0.9f, 0.25f, 0.3f);
            Assert.IsTrue(rest.Finished);
            Assert.AreEqual(0f, rest.Squash, 1e-5f);
        }
    }
}
