using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class DestructibleEventQueueTests
    {
        private const byte Laser = (byte)DamageSource.Laser;

        [Test]
        public void Empty_DrainReturnsNull()
        {
            Assert.IsNull(new DestructibleEventQueue().Drain());
        }

        [Test]
        public void ConsecutiveDamage_IsMergedIntoOne()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(0, 1f, Laser, 1, 29f);
            queue.OnHealthChanged(0, 29f);
            queue.OnDamaged(0, 2f, Laser, 1, 27f);
            queue.OnHealthChanged(0, 27f);

            var events = queue.Drain();

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(DestructibleNetEventKind.Damage, events[0].Kind);
            Assert.AreEqual(3f, events[0].Amount, 1e-5f);
            Assert.AreEqual(27f, events[0].Health, 1e-5f);
            Assert.AreEqual(1, events[0].Attacker);
        }

        [Test]
        public void DifferentObstacles_AreKeptSeparate()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(0, 1f, Laser, 0, 9f);
            queue.OnDamaged(1, 1f, Laser, 2, 9f);
            queue.OnDamaged(0, 1f, Laser, 0, 8f);

            var events = queue.Drain();

            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(0, events[0].Index);
            Assert.AreEqual(2f, events[0].Amount, 1e-5f);
            Assert.AreEqual(1, events[1].Index);
        }

        [Test]
        public void DamageThenDestroy_KeepsOrder()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(3, 5f, Laser, 2, 0f);
            queue.OnHealthChanged(3, 0f);
            queue.OnDestroyed(3, Laser, 2);

            var events = queue.Drain();

            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(DestructibleNetEventKind.Damage, events[0].Kind);
            Assert.AreEqual(DestructibleNetEventKind.Destroy, events[1].Kind);
            Assert.AreEqual(2, events[1].Attacker);
        }

        [Test]
        public void DamageAfterDestroyAndRegenerate_IsNotMergedIntoEarlierDamage()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(0, 5f, Laser, 0, 0f);
            queue.OnDestroyed(0, Laser, 0);
            queue.OnRegenerated(0);
            queue.OnDamaged(0, 1f, Laser, 1, 9f);

            var events = queue.Drain();

            Assert.AreEqual(4, events.Length);
            Assert.AreEqual(DestructibleNetEventKind.Damage, events[3].Kind);
            Assert.AreEqual(1f, events[3].Amount, 1e-5f);
        }

        [Test]
        public void HealthChangeRightBeforeRegenerate_IsDropped()
        {
            // 再生は「耐久の変化 → 再生」の順で通知されるが、耐久は再生に含まれるので送らない。
            var queue = new DestructibleEventQueue();
            queue.OnHealthChanged(0, 30f);
            queue.OnRegenerated(0);

            var events = queue.Drain();

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(DestructibleNetEventKind.Regenerate, events[0].Kind);
        }

        [Test]
        public void SelfRepair_IsSentAsHealth()
        {
            var queue = new DestructibleEventQueue();
            queue.OnHealthChanged(2, 10f);
            queue.OnHealthChanged(2, 11f);

            var events = queue.Drain();

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(DestructibleNetEventKind.Health, events[0].Kind);
            Assert.AreEqual(11f, events[0].Health, 1e-5f);
        }

        [Test]
        public void UnknownAttacker_DoesNotOverwriteKnownOne()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(0, 1f, Laser, 3, 9f);
            queue.OnDamaged(0, 1f, (byte)DamageSource.Chain, -1, 8f);

            var events = queue.Drain();

            Assert.AreEqual(3, events[0].Attacker);
        }

        [Test]
        public void FlagEvents_KeepValueAndOrder()
        {
            var queue = new DestructibleEventQueue();
            queue.OnInvulnerabilityChanged(1, true);
            queue.OnActiveChanged(1, false);
            queue.OnInvulnerabilityChanged(1, false);

            var events = queue.Drain();

            Assert.AreEqual(3, events.Length);
            Assert.IsTrue(events[0].Flag);
            Assert.AreEqual(DestructibleNetEventKind.Active, events[1].Kind);
            Assert.IsFalse(events[1].Flag);
            Assert.IsFalse(events[2].Flag);
        }

        [Test]
        public void Drain_EmptiesQueue()
        {
            var queue = new DestructibleEventQueue();
            queue.OnDamaged(0, 1f, Laser, 0, 9f);

            queue.Drain();

            Assert.AreEqual(0, queue.Count);
            Assert.IsNull(queue.Drain());
        }
    }
}
