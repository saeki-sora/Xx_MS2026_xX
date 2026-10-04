using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class EnemyNetTrackerTests
    {
        private sealed class FakeEnemy
        {
            public bool Supported = true;
            public ushort Type;
            public float X;
            public float Y;
        }

        private static bool Describe(FakeEnemy e, out ushort type, out float x, out float y)
        {
            type = e.Type;
            x = e.X;
            y = e.Y;
            return e.Supported;
        }

        [Test]
        public void Added_IsPendingUntilResolved()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            tracker.OnAdded(new FakeEnemy());

            Assert.IsNull(tracker.Drain());
            Assert.AreEqual(1, tracker.PendingCount);
        }

        [Test]
        public void Resolve_UsesValuesAtResolveTime()
        {
            // 生成直後は種類が未設定のことがあるため、送る直前の値を使う。
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var enemy = new FakeEnemy();
            tracker.OnAdded(enemy);
            enemy.Type = 3;
            enemy.X = 1.5f;

            tracker.ResolvePending(Describe);
            var events = tracker.Drain();

            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(EnemyNetEventKind.Spawn, events[0].Kind);
            Assert.AreEqual(3, events[0].TypeIndex);
            Assert.AreEqual(1.5f, events[0].X);
            Assert.AreEqual(1u, events[0].Id);
        }

        [Test]
        public void RemovedWhilePending_SendsNothing()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var enemy = new FakeEnemy();
            tracker.OnAdded(enemy);
            tracker.OnRemoved(enemy, 1);

            tracker.ResolvePending(Describe);

            Assert.IsNull(tracker.Drain());
            Assert.AreEqual(0, tracker.Ids.Count);
        }

        [Test]
        public void SpawnThenDespawn_KeepsOrderAndReason()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var enemy = new FakeEnemy();
            tracker.OnAdded(enemy);
            tracker.ResolvePending(Describe);
            tracker.OnRemoved(enemy, (byte)EnemyRemovalReason.Died);

            var events = tracker.Drain();

            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(EnemyNetEventKind.Despawn, events[1].Kind);
            Assert.AreEqual(events[0].Id, events[1].Id);
            Assert.AreEqual((byte)EnemyRemovalReason.Died, events[1].Reason);
        }

        [Test]
        public void Unsupported_IsNotTracked()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var enemy = new FakeEnemy { Supported = false };
            tracker.OnAdded(enemy);

            var unsupported = tracker.ResolvePending(Describe);
            tracker.OnRemoved(enemy, 1);

            Assert.AreEqual(1, unsupported);
            Assert.IsNull(tracker.Drain());
        }

        [Test]
        public void Ids_AreUniqueAndIncreasing()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var a = new FakeEnemy();
            var b = new FakeEnemy();
            tracker.OnAdded(a);
            tracker.OnAdded(a);
            tracker.OnAdded(b);

            tracker.ResolvePending(Describe);

            Assert.AreEqual(2, tracker.Ids.Count);
            Assert.AreNotEqual(tracker.Ids[a], tracker.Ids[b]);
        }

        [Test]
        public void DiscardEvents_KeepsIds()
        {
            var tracker = new EnemyNetTracker<FakeEnemy>();
            var enemy = new FakeEnemy();
            tracker.OnAdded(enemy);
            tracker.ResolvePending(Describe);

            tracker.DiscardEvents();

            Assert.IsNull(tracker.Drain());
            Assert.IsTrue(tracker.Ids.ContainsKey(enemy));
        }
    }
}
