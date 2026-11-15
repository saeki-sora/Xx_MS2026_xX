using System.Collections.Generic;
using NUnit.Framework;

namespace MS2026.Cpu.Experimental.Tests
{
    public sealed class CpuRescueMathTests
    {
        private static CpuRescueMath.Tuning Tuning() => new CpuRescueMath.Tuning
        {
            ReviveAttention = 1f,
            HealAttention = 0.5f,
            HealBelowHealth = 0.6f,
            ShareWithHelpers = 0.4f,
            HitReach = 0.6f
        };

        private static CpuTeammateSight Mate(int player, float angle, CpuTeammateCondition condition, float health = 1f, int helpers = 0,
            bool inLine = true) => new CpuTeammateSight
        {
            Player = player, Angle = angle, Distance = 6f, Condition = condition, Health01 = health, Helpers = helpers, InLine = inLine
        };

        [Test]
        public void PicksDownedMateOnTarget()
        {
            var aim = CpuRescueMath.Pick(new List<CpuTeammateSight> { Mate(2, 10f, CpuTeammateCondition.Downed, 0f) }, 9f, +1f, 45f, Tuning());
            Assert.IsTrue(aim.Found);
            Assert.AreEqual(2, aim.Player);
            Assert.IsTrue(aim.Downed);
            Assert.IsTrue(aim.OnTarget);
            Assert.AreEqual(1f, aim.Want, 1e-4f);
        }

        [Test]
        public void IgnoresHealthyAndBlockedMates()
        {
            var mates = new List<CpuTeammateSight>
            {
                Mate(1, 10f, CpuTeammateCondition.Normal, 1f),
                Mate(2, 20f, CpuTeammateCondition.Downed, 0f, inLine: false)
            };
            Assert.IsFalse(CpuRescueMath.Pick(mates, 9f, +1f, 45f, Tuning()).Found);
        }

        [Test]
        public void HurtMate_WantGrowsWithDamage()
        {
            var slightly = CpuRescueMath.Pick(new List<CpuTeammateSight> { Mate(1, 10f, CpuTeammateCondition.Hurt, 0.5f) }, 9f, +1f, 45f, Tuning());
            var badly = CpuRescueMath.Pick(new List<CpuTeammateSight> { Mate(1, 10f, CpuTeammateCondition.Hurt, 0.1f) }, 9f, +1f, 45f, Tuning());
            Assert.IsTrue(slightly.Found && badly.Found);
            Assert.Greater(badly.Want, slightly.Want);
            Assert.IsFalse(badly.Downed);
        }

        [Test]
        public void HelpersReduceWant_AndDownedBeatsHurt()
        {
            var mates = new List<CpuTeammateSight>
            {
                Mate(1, 10f, CpuTeammateCondition.Downed, 0f, helpers: 1),
                Mate(3, 12f, CpuTeammateCondition.Downed, 0f)
            };
            var aim = CpuRescueMath.Pick(mates, 9f, +1f, 45f, Tuning());
            Assert.AreEqual(3, aim.Player); // 誰も助けていない方
        }

        [Test]
        public void NotRotating_OnlyCurrentDirection()
        {
            Assert.IsFalse(CpuRescueMath.Pick(new List<CpuTeammateSight> { Mate(1, 90f, CpuTeammateCondition.Downed, 0f) }, 0f, 0f, 45f, Tuning()).Found);
        }
    }
}
