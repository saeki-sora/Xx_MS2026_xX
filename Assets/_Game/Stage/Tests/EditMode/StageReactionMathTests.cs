using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageReactionMathTests
    {
        [Test]
        public void Heat_NearbyHitsMergeIntoOnePoint()
        {
            var buffer = new HeatPointBuffer(8);
            buffer.Add(Vector3.zero, 0.3f, 0.4f, 0.5f);
            buffer.Add(new Vector3(0.1f, 0f, 0f), 0.3f, 0.4f, 0.5f);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(0.6f, buffer[0].Glow, 1e-4f);
        }

        [Test]
        public void Heat_GlowCoolsButScorchStays()
        {
            var buffer = new HeatPointBuffer(8);
            buffer.Add(Vector3.zero, 1f, 0.4f, 0.5f);

            for (var i = 0; i < 100; i++)
            {
                buffer.Tick(0.1f, 1f, 0.5f);
            }

            Assert.AreEqual(1, buffer.Count, "焦げが残っているので点は消えない");
            Assert.AreEqual(0f, buffer[0].Glow, 1e-4f);
            Assert.Greater(buffer[0].Scorch, 0.1f);
            Assert.IsFalse(buffer.IsHot);
        }

        [Test]
        public void Heat_WhenFull_ReplacesTheWeakestPoint()
        {
            var buffer = new HeatPointBuffer(2);
            buffer.Add(new Vector3(0f, 0f, 0f), 1f, 0.1f, 0.1f);
            buffer.Add(new Vector3(5f, 0f, 0f), 0.1f, 0.1f, 0.1f);
            buffer.Add(new Vector3(10f, 0f, 0f), 0.5f, 0.1f, 0.1f);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(0f, buffer[0].Position.x, 1e-4f, "強い点は残る");
            Assert.AreEqual(10f, buffer[1].Position.x, 1e-4f, "弱い点が置き換わる");
        }

        [Test]
        public void Heat_PointsWithoutGlowOrScorchAreRemoved()
        {
            var buffer = new HeatPointBuffer(4);
            buffer.Add(Vector3.zero, 0.01f, 0.1f, 0.1f);

            buffer.Tick(1f, 10f, 0f);

            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Spring_SettlesOnTarget()
        {
            var spring = new DampedSpring2D();
            for (var i = 0; i < 600; i++)
            {
                spring.Step(new Vector2(3f, -1f), 90f, 7f, 1f / 60f);
            }

            Assert.AreEqual(3f, spring.Value.x, 0.01f);
            Assert.AreEqual(-1f, spring.Value.y, 0.01f);
        }

        [Test]
        public void Spring_LargeDeltaTimeStaysStable()
        {
            var spring = new DampedSpring2D();
            spring.Kick(new Vector2(50f, 0f));
            for (var i = 0; i < 20; i++)
            {
                spring.Step(Vector2.zero, 300f, 5f, 0.5f);
            }

            Assert.Less(spring.Value.magnitude, 1f, "大きなdtでも発散しない");
        }
    }
}
