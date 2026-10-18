using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmFxMathTests
    {
        [Test]
        public void SparkSlowsDownBeforeHoming()
        {
            var v = SwarmFxMath.StepSparkVelocity(new Vector2(4f, 0f), Vector2.zero, new Vector2(0f, 10f), 0.05f, 0.2f, 16f, 0.02f);
            Assert.Less(v.magnitude, 4f);
            Assert.AreEqual(0f, v.y, 1e-5f);
        }

        [Test]
        public void SparkTurnsTowardTargetAfterDelay()
        {
            var v = new Vector2(4f, 0f);
            var p = Vector2.zero;
            var target = new Vector2(0f, 10f);
            for (var i = 0; i < 30; i++)
            {
                v = SwarmFxMath.StepSparkVelocity(v, p, target, 0.3f + i * 0.02f, 0.2f, 16f, 0.02f);
                p += v * 0.02f;
            }

            Assert.Greater(Vector2.Dot(v.normalized, (target - p).normalized), 0.9f);
        }

        [Test]
        public void SparkWithoutTargetOnlySlowsDown()
        {
            var v = SwarmFxMath.StepSparkVelocity(new Vector2(0f, 3f), Vector2.zero, null, 1f, 0.2f, 16f, 0.1f);
            Assert.Less(v.magnitude, 3f);
            Assert.Greater(v.y, 0f);
        }

        [Test]
        public void FlashGrowsAndFades()
        {
            Assert.AreEqual(0.6f, SwarmFxMath.FlashScale(0f), 1e-5f);
            Assert.AreEqual(1.4f, SwarmFxMath.FlashScale(1f), 1e-5f);
            Assert.AreEqual(1f, SwarmFxMath.FlashAlpha(0f), 1e-5f);
            Assert.AreEqual(0f, SwarmFxMath.FlashAlpha(1f), 1e-5f);
        }

        [Test]
        public void SparkAlphaFadesInAndOut()
        {
            Assert.AreEqual(0f, SwarmFxMath.SparkAlpha(0f, 2f), 1e-5f);
            Assert.AreEqual(1f, SwarmFxMath.SparkAlpha(1f, 2f), 1e-5f);
            Assert.AreEqual(0f, SwarmFxMath.SparkAlpha(2f, 2f), 1e-5f);
            Assert.AreEqual(0f, SwarmFxMath.SparkAlpha(0.5f, 0f), 1e-5f);
        }
    }
}
