using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class BombBlastQueueTests
    {
        private static BombBlast At(float due, float x = 0f)
        {
            return new BombBlast { dueTime = due, center = new Vector2(x, 0f), radius = 1f, enemyDamage = 1f, owner = -1 };
        }

        [Test]
        public void TakesOnlyBlastsWhoseTimeHasCome()
        {
            var queue = new BombBlastQueue();
            queue.Add(At(1f, 1f));
            queue.Add(At(0.5f, 2f));
            queue.Add(At(2f, 3f));
            var output = new List<BombBlast>();

            Assert.AreEqual(2, queue.TakeDue(1f, 10, output));
            Assert.AreEqual(1f, output[0].center.x, "入れた順に取り出す");
            Assert.AreEqual(2f, output[1].center.x);
            Assert.AreEqual(1, queue.Count, "まだの分は残る");

            Assert.AreEqual(0, queue.TakeDue(1.5f, 10, output));
            Assert.AreEqual(1, queue.TakeDue(2f, 10, output));
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void CarriesOverflowToTheNextCall()
        {
            var queue = new BombBlastQueue();
            for (var i = 0; i < 5; i++)
            {
                queue.Add(At(0f, i));
            }

            var output = new List<BombBlast>();
            Assert.AreEqual(3, queue.TakeDue(0f, 3, output));
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(2, queue.TakeDue(0f, 3, output));
            Assert.AreEqual(3f, output[0].center.x, "上限で残った分は、次の呼び出しで順番通りに出る");
        }

        [Test]
        public void ClearDropsEverything()
        {
            var queue = new BombBlastQueue();
            queue.Add(At(0f));
            queue.Clear();
            Assert.AreEqual(0, queue.Count);
        }
    }

    public sealed class SwarmBombMathTests
    {
        [Test]
        public void PulseFlashesAtTheStartOfEachCycleAndFades()
        {
            Assert.AreEqual(1f, SwarmBombMath.BlinkPulse(3f), 1e-5f);
            Assert.Greater(SwarmBombMath.BlinkPulse(3.1f), SwarmBombMath.BlinkPulse(3.3f));
            Assert.AreEqual(0f, SwarmBombMath.BlinkPulse(3.7f), 1e-5f, "周期の後半は消えている");
        }

        [Test]
        public void BlinksFasterWhenApproachingTheCore()
        {
            // 1秒で 1m 近づくときの位相の進み = 点滅の回数/秒。
            float Rate(float from) =>
                SwarmBombMath.BlinkPhase(1f, 0f, from - 1f, 1.5f, 4f, 14f) - SwarmBombMath.BlinkPhase(0f, 0f, from, 1.5f, 4f, 14f);

            Assert.AreEqual(1.5f, Rate(30f), 1e-4f, "速くなり始める距離より遠ければ元の速さ");
            Assert.Greater(Rate(4f), Rate(10f), "近いほど速い");
        }

        [Test]
        public void StandingStillKeepsTheBaseRate()
        {
            var a = SwarmBombMath.BlinkPhase(100f, 0.3f, 2f, 1.5f, 4f, 14f);
            var b = SwarmBombMath.BlinkPhase(101f, 0.3f, 2f, 1.5f, 4f, 14f);
            Assert.AreEqual(1.5f, b - a, 1e-3f, "時刻が大きくても、止まっていれば位相は一定の速さで進む（飛ばない）");
        }

        [Test]
        public void SeedIsStableAndInRange()
        {
            for (var id = 0; id < 1000; id += 37)
            {
                var s = SwarmBombMath.Seed(id);
                Assert.GreaterOrEqual(s, 0f);
                Assert.Less(s, 1f);
                Assert.AreEqual(s, SwarmBombMath.Seed(id), "同じ番号ならどのPCでも同じ");
            }
        }
    }
}
