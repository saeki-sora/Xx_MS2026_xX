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
}
