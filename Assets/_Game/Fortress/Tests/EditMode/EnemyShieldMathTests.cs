using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class EnemyShieldMathTests
    {
        [Test]
        public void BoxesMakeAUShapeOpenAtTheBack()
        {
            var settings = new EnemyShieldSettings { frontDistance = 2f, halfWidth = 1.5f, sideLength = 1f, thickness = 0.2f };
            var boxes = new ShieldBox[3];
            EnemyShieldMath.Boxes(settings, boxes);

            Assert.AreEqual(new Vector2(2f, 0f), boxes[0].center, "正面の板は前");
            Assert.AreEqual(3.2f, boxes[0].size.y, 1e-4f, "正面の板は横の板の外側まで");
            Assert.AreEqual(new Vector2(1.5f, 1.5f), boxes[1].center, "横の板は正面から後ろへ半分");
            Assert.AreEqual(new Vector2(1.5f, -1.5f), boxes[2].center);
            Assert.AreEqual(1.2f, boxes[1].size.x, 1e-4f);
            Assert.Less(boxes[1].center.x - boxes[1].size.x * 0.5f, 2f, "後ろ（-X）側は空いている");
        }

        [Test]
        public void EscortSlotsFillRingsOfSixThenTwelve()
        {
            for (var k = 0; k < 6; k++)
            {
                Assert.AreEqual(0.5f + 0.06f, math.length(EnemyShieldMath.EscortSlot(k, 0.5f)), 1e-4f, "1つ目の輪");
            }

            for (var k = 6; k < 18; k++)
            {
                Assert.AreEqual(1.06f, math.length(EnemyShieldMath.EscortSlot(k, 0.5f)), 1e-4f, "2つ目の輪");
            }

            Assert.AreEqual(1.56f, math.length(EnemyShieldMath.EscortSlot(18, 0.5f)), 1e-4f, "3つ目の輪");
            Assert.Greater(math.distance(EnemyShieldMath.EscortSlot(0, 0.5f), EnemyShieldMath.EscortSlot(1, 0.5f)), 0.4f, "重ならない");
        }

        [Test]
        public void FocusIsWhoeverHitTheMost()
        {
            var threat = new[] { 0f, 0.5f, 0.2f, 0f };
            Assert.AreEqual(1, EnemyShieldMath.ChooseFocus(threat, -1, 1.3f, 3));
        }

        [Test]
        public void FocusFallsBackToTheNearestTurretWhenNobodyHits()
        {
            var threat = new float[4];
            Assert.AreEqual(2, EnemyShieldMath.ChooseFocus(threat, -1, 1.3f, 2));
        }

        [Test]
        public void FocusOnlySwitchesWhenTheOtherIsClearlyAhead()
        {
            var threat = new[] { 1f, 1.2f, 0f, 0f };
            Assert.AreEqual(0, EnemyShieldMath.ChooseFocus(threat, 0, 1.3f, 3), "少し上回っただけでは向きを変えない");
            threat[1] = 1.4f;
            Assert.AreEqual(1, EnemyShieldMath.ChooseFocus(threat, 0, 1.3f, 3));
        }

        [Test]
        public void DecayHalvesAfterTheMemoryTimesLnTwo()
        {
            var threat = new[] { 1f, 0f, 0f, 0f };
            EnemyShieldMath.Decay(threat, 2f * Mathf.Log(2f), 2f);
            Assert.AreEqual(0.5f, threat[0], 1e-4f);
        }

        [Test]
        public void TurnTowardTakesTheShortWayAndStopsAtTheTarget()
        {
            Assert.AreEqual(180f, EnemyShieldMath.TurnToward(170f, -170f, 10f), 1e-3f, "170°→-170° は +方向へ（近い方へ）回る");
            Assert.AreEqual(30f, EnemyShieldMath.TurnToward(0f, 30f, 90f), 1e-3f, "行き過ぎない");
            Assert.AreEqual(-5f, EnemyShieldMath.TurnToward(0f, -90f, 5f), 1e-3f);
        }

        [Test]
        public void SegmentDistanceUsesTheClosestPoint()
        {
            Assert.AreEqual(1f, EnemyShieldMath.SegmentDistanceSq(new float2(0f, 1f), new float2(-2f, 0f), new float2(2f, 0f)), 1e-5f);
            Assert.AreEqual(4f, EnemyShieldMath.SegmentDistanceSq(new float2(4f, 0f), new float2(-2f, 0f), new float2(2f, 0f)), 1e-5f, "端より先は端までの距離");
        }
    }
}
