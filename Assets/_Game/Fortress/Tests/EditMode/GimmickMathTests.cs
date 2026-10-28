using System.Collections.Generic;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class GimmickMathTests
    {
        private const float Eps = 1e-3f;

        [Test]
        public void StepGearStaysStillBeforeTheFirstTurn()
        {
            var angle = GimmickMath.StepAngle(5.9f, 6f, 8f, 1.5f, 90f, out var turning, out var index);
            Assert.AreEqual(0f, angle, Eps);
            Assert.IsFalse(turning);
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void StepGearTurnsOneStepThenWaits()
        {
            // 6秒から 1.5秒かけて 90°、8秒止まる → 周期 9.5秒
            var half = GimmickMath.StepAngle(6.75f, 6f, 8f, 1.5f, 90f, out var turning, out var index);
            Assert.AreEqual(45f, half, Eps, "半分の時間で半分（なめらかな動きでも真ん中は半分）");
            Assert.IsTrue(turning);
            Assert.AreEqual(0, index);

            var done = GimmickMath.StepAngle(10f, 6f, 8f, 1.5f, 90f, out turning, out index);
            Assert.AreEqual(90f, done, Eps);
            Assert.IsFalse(turning);

            var second = GimmickMath.StepAngle(6f + 9.5f + 1.5f, 6f, 8f, 1.5f, 90f, out turning, out index);
            Assert.AreEqual(180f, second, Eps, "2回目が終わったら 180°");
            Assert.AreEqual(1, index);
        }

        [Test]
        public void StepGearAngleStaysPreciseAfterManyTurns()
        {
            var angle = GimmickMath.StepAngle(6f + 9.5f * 4000f + 5f, 6f, 8f, 1.5f, 90f, out _, out _);
            Assert.AreEqual(0f, angle % 90f, Eps, "何千回回っても 90° の倍数で止まる");
        }

        [Test]
        public void ContinuousAndFollow()
        {
            Assert.AreEqual(0f, GimmickMath.ContinuousAngle(1f, 2f, 10f), Eps);
            Assert.AreEqual(30f, GimmickMath.ContinuousAngle(5f, 2f, 10f), Eps);
            Assert.AreEqual(-60f, GimmickMath.FollowAngle(30f, 16, 8), Eps, "歯が半分なら2倍の速さで反対向き");
        }

        private static List<GimmickMath.Stop> TwoStops()
        {
            return new List<GimmickMath.Stop>
            {
                new GimmickMath.Stop(0f, 0f, 0f, 1f, 2f),
                new GimmickMath.Stop(4f, 0f, 90f, 2f, 3f)
            };
        }

        [Test]
        public void MoverPingPongGoesAndComesBack()
        {
            var stops = TwoStops();
            var legs = new List<GimmickMath.Leg>();
            GimmickMath.BuildLegs(stops, 1, legs);
            Assert.AreEqual(2, legs.Count);

            var before = GimmickMath.EvaluateMover(3.9f, 4f, stops, legs, true);
            Assert.AreEqual(0f, before.X, Eps);
            Assert.IsFalse(before.Moving);

            var middle = GimmickMath.EvaluateMover(5f, 4f, stops, legs, true);
            Assert.AreEqual(2f, middle.X, Eps, "2秒かけて 4m → 1秒で半分");
            Assert.AreEqual(45f, middle.Angle, Eps);
            Assert.IsTrue(middle.Moving);
            Assert.AreEqual(1, middle.TargetStop);

            var arrived = GimmickMath.EvaluateMover(7f, 4f, stops, legs, true);
            Assert.AreEqual(4f, arrived.X, Eps);
            Assert.IsFalse(arrived.Moving);

            // 戻り: 4 + 2 + 3 = 9秒から 2秒かけて戻る（行きと同じ秒数）
            var back = GimmickMath.EvaluateMover(10f, 4f, stops, legs, true);
            Assert.AreEqual(2f, back.X, Eps);
            Assert.AreEqual(0, back.TargetStop);

            // 1周 = (2+3) + (2+2) = 9秒 → 4+9+1 = 14秒は次の周の行きの途中
            var nextRound = GimmickMath.EvaluateMover(14f, 4f, stops, legs, true);
            Assert.AreEqual(GimmickMath.EvaluateMover(5f, 4f, stops, legs, true).X, nextRound.X, Eps);
        }

        [Test]
        public void MoverOnceStopsAtTheLastStop()
        {
            var stops = TwoStops();
            var legs = new List<GimmickMath.Leg>();
            GimmickMath.BuildLegs(stops, 2, legs);
            Assert.AreEqual(1, legs.Count);
            var later = GimmickMath.EvaluateMover(100f, 4f, stops, legs, false);
            Assert.AreEqual(4f, later.X, Eps);
            Assert.IsFalse(later.Moving);
        }

        [Test]
        public void MoverLoopReturnsWithTheStartsMoveSeconds()
        {
            var stops = TwoStops();
            var legs = new List<GimmickMath.Leg>();
            GimmickMath.BuildLegs(stops, 0, legs);
            Assert.AreEqual(2, legs.Count);
            Assert.AreEqual(1f, legs[1].Move, Eps, "最後から出発点へは、出発点の「動く秒」で戻る");
            Assert.AreEqual(2f, legs[1].Wait, Eps);
        }

        [Test]
        public void IntervalsListWhenThingsMove()
        {
            var result = new List<(float start, float end)>();
            GimmickMath.StepIntervals(6f, 8f, 1.5f, 30f, result);
            Assert.AreEqual(3, result.Count, "6, 15.5, 25 秒");
            Assert.AreEqual(15.5f, result[1].start, Eps);
            Assert.AreEqual(17f, result[1].end, Eps);

            var stops = TwoStops();
            var legs = new List<GimmickMath.Leg>();
            GimmickMath.BuildLegs(stops, 1, legs);
            result.Clear();
            GimmickMath.MoverIntervals(4f, legs, true, 20f, result);
            Assert.AreEqual(4f, result[0].start, Eps);
            Assert.AreEqual(6f, result[0].end, Eps);
            Assert.AreEqual(9f, result[1].start, Eps);
            Assert.AreEqual(13f, result[2].start, Eps);
        }
    }
}
