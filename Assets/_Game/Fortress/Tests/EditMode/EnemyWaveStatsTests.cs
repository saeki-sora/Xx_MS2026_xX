using System.Collections.Generic;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class EnemyWaveStatsTests
    {
        private static EnemySpawnEntry Entry(float time, string point, int count, float interval) =>
            new EnemySpawnEntry { triggerTime = time, spawnPointId = point, count = count, intervalBetweenSpawns = interval };

        [Test]
        public void EndTime_AddsIntervalsBetweenSpawns()
        {
            Assert.AreEqual(10f, EnemyWaveStats.EndTime(Entry(10f, "A", 1, 0.5f)), 1e-5f);
            Assert.AreEqual(14.5f, EnemyWaveStats.EndTime(Entry(10f, "A", 10, 0.5f)), 1e-5f);
            Assert.AreEqual(10f, EnemyWaveStats.EndTime(Entry(10f, "A", 10, 0f)), 1e-5f, "間隔0は同時");
        }

        [Test]
        public void TotalsAndLastSpawn()
        {
            var entries = new List<EnemySpawnEntry> { Entry(0f, "A", 5, 1f), Entry(3f, "B", 2, 0f), null };
            Assert.AreEqual(7, EnemyWaveStats.TotalEnemies(entries));
            Assert.AreEqual(4f, EnemyWaveStats.LastSpawnTime(entries), 1e-5f);
            Assert.AreEqual(0, EnemyWaveStats.TotalEnemies(null));
        }

        [Test]
        public void SpawnedBy_CountsOneAtStartThenOnePerInterval()
        {
            var entries = new List<EnemySpawnEntry> { Entry(2f, "A", 5, 1f), Entry(3f, "B", 4, 0f) };
            Assert.AreEqual(0, EnemyWaveStats.SpawnedBy(entries, 1.9f));
            Assert.AreEqual(1, EnemyWaveStats.SpawnedBy(entries, 2f));
            Assert.AreEqual(2 + 4, EnemyWaveStats.SpawnedBy(entries, 3f));
            Assert.AreEqual(5 + 4, EnemyWaveStats.SpawnedBy(entries, 100f));
        }

        [Test]
        public void Lanes_AreDistinctAndNaturallySorted()
        {
            var entries = new List<EnemySpawnEntry> { Entry(0, "Spawn_10", 1, 0), Entry(0, "Spawn_2", 1, 0), Entry(0, "Spawn_2", 1, 0), Entry(0, null, 1, 0) };
            CollectionAssert.AreEqual(new[] { "", "Spawn_2", "Spawn_10" }, EnemyWaveStats.Lanes(entries));
        }

        [Test]
        public void TickStep_UsesOneTwoFiveSteps()
        {
            Assert.AreEqual(1f, EnemyWaveStats.TickStep(10f, 10), 1e-4f);
            Assert.AreEqual(5f, EnemyWaveStats.TickStep(40f, 10), 1e-4f);
            Assert.AreEqual(20f, EnemyWaveStats.TickStep(150f, 10), 1e-4f);
            Assert.AreEqual(0.5f, EnemyWaveStats.TickStep(4f, 10), 1e-4f);
        }
    }
}
