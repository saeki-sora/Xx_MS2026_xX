using NUnit.Framework;
using UnityEngine;

namespace MS2026.Cpu.Experimental.Tests
{
    public sealed class CpuMoveMathTests
    {
        private static CpuMoveTuning Tuning() => new CpuMoveTuning
        {
            AvoidWeight = 1f,
            ThreatFullCount = 6f,
            ThreatIgnoreCount = 1f,
            HomeFreeRadius = 2f,
            HomePullRange = 2f,
            HomeWeight = 1f,
            DeadZone = 0.1f
        };

        private static CpuSight SightWithEnemies(float angleDegrees, float inner, float outer = 0f)
        {
            var sight = new CpuSight(60, 10) { Range = 20f };
            var sector = CpuSectorMath.SectorOf(angleDegrees, 60);
            sight.Weights[sector] = inner + outer;
            sight.RingWeights[sector * 10] = inner;
            sight.RingWeights[sector * 10 + 1] = outer;
            return sight;
        }

        [Test]
        public void Threat_PointsTowardEnemies()
        {
            var sight = SightWithEnemies(3f, 4f, 2f); // 右（0〜6 度）
            var threat = CpuMoveMath.Threat(sight, 0.5f, out var count);
            Assert.AreEqual(5f, count, 1e-4f);
            Assert.Greater(threat.x, 4.9f);
            Assert.AreEqual(0f, threat.y, 0.6f);
        }

        [Test]
        public void Plan_MovesAwayFromCrowd()
        {
            var sight = SightWithEnemies(3f, 6f);
            var threat = CpuMoveMath.Threat(sight, 0.5f, out var count);
            var plan = CpuMoveMath.Plan(Vector2.zero, true, Vector2.zero, threat, count, Tuning());
            Assert.Less(plan.x, -0.5f); // 右に敵 → 左へ
        }

        [Test]
        public void Plan_IgnoresFewEnemies()
        {
            var sight = SightWithEnemies(3f, 1f);
            var threat = CpuMoveMath.Threat(sight, 0.5f, out var count);
            Assert.AreEqual(Vector2.zero, CpuMoveMath.Plan(Vector2.zero, true, Vector2.zero, threat, count, Tuning()));
        }

        [Test]
        public void Plan_ReturnsHomeWhenFar_StaysWhenNear()
        {
            Assert.AreEqual(Vector2.zero, CpuMoveMath.Plan(new Vector2(1.5f, 0f), true, Vector2.zero, Vector2.zero, 0f, Tuning()));
            var back = CpuMoveMath.Plan(new Vector2(5f, 0f), true, Vector2.zero, Vector2.zero, 0f, Tuning());
            Assert.AreEqual(-1f, back.x, 1e-4f);
        }

        [Test]
        public void Plan_SurroundedEvenly_AvoidsWeakly()
        {
            var sight = new CpuSight(60, 10) { Range = 20f };
            for (var s = 0; s < 60; s += 15)
            {
                sight.RingWeights[s * 10] = 3f; // 4 方向に同じだけ
                sight.Weights[s] = 3f;
            }

            var threat = CpuMoveMath.Threat(sight, 0.5f, out var count);
            var plan = CpuMoveMath.Plan(Vector2.zero, true, Vector2.zero, threat, count, Tuning());
            Assert.Less(plan.magnitude, 0.5f);
        }
    }
}
