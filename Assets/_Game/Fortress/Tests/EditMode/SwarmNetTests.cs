using MS2026.Fortress.Net;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmIdPoolTests
    {
        [Test]
        public void AllocatesEveryIdOnceThenRunsOut()
        {
            var pool = new SwarmIdPool(3);

            Assert.IsTrue(pool.TryAllocate(out var a));
            Assert.IsTrue(pool.TryAllocate(out var b));
            Assert.IsTrue(pool.TryAllocate(out var c));
            Assert.IsFalse(pool.TryAllocate(out var none));

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, new[] { a, b, c });
            Assert.AreEqual(-1, none);
        }

        [Test]
        public void ReleasedIds_AreReusedOldestFirst()
        {
            // 消えたばかりの番号がすぐ再利用されないよう、先に返された番号から使う。
            var pool = new SwarmIdPool(4);
            for (var i = 0; i < 4; i++)
            {
                pool.TryAllocate(out _);
            }

            pool.Release(2);
            pool.Release(0);

            pool.TryAllocate(out var first);
            pool.TryAllocate(out var second);
            Assert.AreEqual(2, first);
            Assert.AreEqual(0, second);
        }

        [Test]
        public void Reset_MakesAllIdsAvailable()
        {
            var pool = new SwarmIdPool(5);
            pool.TryAllocate(out _);
            pool.TryAllocate(out _);

            pool.Reset();

            Assert.AreEqual(5, pool.AvailableCount);
        }

        [Test]
        public void OutOfRangeRelease_IsIgnored()
        {
            var pool = new SwarmIdPool(2);
            pool.Release(7);
            pool.Release(-1);

            Assert.AreEqual(2, pool.AvailableCount);
        }
    }

    public sealed class SwarmNetQuantizeTests
    {
        [TestCase(0f)]
        [TestCase(12.3456f)]
        [TestCase(-33.9f)]
        [TestCase(65f)]
        public void Position_RoundTripsWithinPrecision(float value)
        {
            var restored = SwarmNetQuantize.FromShort(SwarmNetQuantize.ToShort(value));

            Assert.AreEqual(value, restored, 1f / SwarmNetQuantize.PositionScale);
        }

        [Test]
        public void Position_ClampsOutsideRange()
        {
            var restored = SwarmNetQuantize.FromShort(SwarmNetQuantize.ToShort(1000f));

            Assert.AreEqual(SwarmNetQuantize.MaxPosition, restored, 1e-3f);
        }

        [Test]
        public void Facing_RoundTripsWithinOneStep()
        {
            var direction = new Vector2(-0.6f, 0.8f);

            var restored = SwarmNetQuantize.FromAngleByte(SwarmNetQuantize.ToAngleByte(direction));

            Assert.Less(Vector2.Angle(direction, restored), 360f / 256f);
        }

        [Test]
        public void Half_RoundTrips()
        {
            Assert.AreEqual(1.15f, SwarmNetQuantize.FromHalf(SwarmNetQuantize.ToHalf(1.15f)), 1e-3f);
            Assert.AreEqual(7.5f, SwarmNetQuantize.FromHalf(SwarmNetQuantize.ToHalf(7.5f)), 1e-2f);
        }
    }

    public sealed class SwarmNetEventQueueTests
    {
        private static SwarmNetEvent Spawn(ushort id) => new SwarmNetEvent { Id = id, TypeOrReason = 1 };

        [Test]
        public void Take_RespectsLimitAndOrder()
        {
            var queue = new SwarmNetEventQueue();
            for (ushort i = 0; i < 5; i++)
            {
                queue.EnqueueSpawn(Spawn(i));
            }

            var first = queue.Take(3);
            var rest = queue.Take(10);

            Assert.AreEqual(new ushort[] { 0, 1, 2 }, new[] { first[0].Id, first[1].Id, first[2].Id });
            Assert.AreEqual(2, rest.Length);
            Assert.AreEqual(4, rest[1].Id);
            Assert.IsNull(queue.Take(10));
        }

        [Test]
        public void DespawnOfUnsentSpawn_CancelsBoth()
        {
            var queue = new SwarmNetEventQueue();
            queue.EnqueueSpawn(Spawn(7));
            queue.EnqueueSpawn(Spawn(8));
            queue.EnqueueDespawn(7, (byte)EnemyRemovalReason.Died);

            var events = queue.Take(10);

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(8, events[0].Id);
            Assert.AreEqual(SwarmNetEventKind.Spawn, events[0].Kind);
        }

        [Test]
        public void DespawnOfSentSpawn_IsSent()
        {
            var queue = new SwarmNetEventQueue();
            queue.EnqueueSpawn(Spawn(3));
            queue.Take(10);

            queue.EnqueueDespawn(3, (byte)EnemyRemovalReason.ReachedCore);
            var events = queue.Take(10);

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(SwarmNetEventKind.Despawn, events[0].Kind);
            Assert.AreEqual((byte)EnemyRemovalReason.ReachedCore, events[0].TypeOrReason);
        }

        [Test]
        public void CancelledEntries_DoNotCountTowardsLimit()
        {
            var queue = new SwarmNetEventQueue();
            queue.EnqueueSpawn(Spawn(1));
            queue.EnqueueSpawn(Spawn(2));
            queue.EnqueueSpawn(Spawn(3));
            queue.EnqueueDespawn(1, 0);
            queue.EnqueueDespawn(2, 0);

            var events = queue.Take(1);

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(3, events[0].Id);
        }

        [Test]
        public void LargeBacklog_StaysConsistentAcrossCompaction()
        {
            // 溜まりすぎたときの詰め直し(4096件ごと)をまたいでも、未送信の出現の取り消しが正しく効く。
            var queue = new SwarmNetEventQueue();
            for (var i = 0; i < 10000; i++)
            {
                queue.EnqueueSpawn(Spawn((ushort)i));
            }

            queue.Take(5000);
            queue.EnqueueDespawn(9000, 0);
            var rest = queue.Take(100000);

            Assert.AreEqual(4999, rest.Length);
            foreach (var e in rest)
            {
                Assert.AreNotEqual(9000, e.Id);
            }
        }
    }
}
