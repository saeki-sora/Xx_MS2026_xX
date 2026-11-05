using NUnit.Framework;
using Unity.Mathematics;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmSpiralTests
    {
        private static SwarmTypeParams Spiral(int mix)
        {
            return new SwarmTypeParams
            {
                maxSpeed = 1.5f,
                spiralRadius = 0.7f,
                spiralAngularSpeed = math.PI, // 0.5 周/秒
                spiralMixDirections = mix
            };
        }

        [Test]
        public void CircleSpeedIsRadiusTimesAngularSpeed()
        {
            var tp = Spiral(0);
            for (var t = 0f; t < 3f; t += 0.37f)
            {
                Assert.AreEqual(0.7f * math.PI, math.length(SwarmMath.SpiralVelocity(tp, 5, t)), 1e-4f);
            }
        }

        [Test]
        public void OneTurnReturnsToTheSameCirclePoint()
        {
            // 回る分の速度を1周ぶん足し合わせると0（輪の中心は動かない）。近づく分だけ進む。
            var tp = Spiral(0);
            var offset = float2.zero;
            const int steps = 2000;
            var dt = 2f / steps;
            for (var i = 0; i < steps; i++)
            {
                offset += SwarmMath.SpiralVelocity(tp, 7, i * dt) * dt;
            }

            Assert.Less(math.length(offset), 0.01f);
        }

        [Test]
        public void MixDirectionsMakesOddIdsTurnTheOtherWay()
        {
            float Turn(SwarmTypeParams tp, int id)
            {
                var a = SwarmMath.SpiralVelocity(tp, id, 0f);
                var b = SwarmMath.SpiralVelocity(tp, id, 0.05f);
                return a.x * b.y - a.y * b.x; // 正=左回り
            }

            Assert.Greater(Turn(Spiral(1), 4), 0f);
            Assert.Less(Turn(Spiral(1), 5), 0f);
            Assert.Greater(Turn(Spiral(0), 5), 0f);
        }

        [Test]
        public void TopSpeedIncludesCircleSpeed()
        {
            Assert.AreEqual(1.5f + 0.7f * math.PI, Spiral(0).TopSpeed, 1e-4f);
            Assert.AreEqual(1.5f, new SwarmTypeParams { maxSpeed = 1.5f }.TopSpeed, 1e-6f);
        }
    }
}
