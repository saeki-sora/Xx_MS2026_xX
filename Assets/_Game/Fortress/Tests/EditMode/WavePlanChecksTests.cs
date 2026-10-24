using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class WavePlanChecksTests
    {
        private static WaveDefinition Wave(int target, bool repeat, params EnemySpawnEntry[] entries) => new WaveDefinition
        {
            targetKills = target,
            repeatUntilCleared = repeat,
            repeatGapSeconds = 2f,
            spawnEntries = entries.ToList()
        };

        private static EnemySpawnEntry Entry(float time, int count, float seconds, string point = "A") => new EnemySpawnEntry
        {
            triggerTime = time,
            spawnPointId = point,
            count = count,
            timing = EnemySpawnTiming.Duration,
            durationSeconds = seconds,
            enemyType = null
        };

        [Test]
        public void EstimatesRateAndTimeToSpawnTheTarget()
        {
            // 1周 = 0〜4秒に 20体 + 待ち 2秒 → 6秒で20体。
            var wave = Wave(30, true, Entry(0f, 10, 4f), Entry(2f, 10, 2f));
            Assert.AreEqual(20, WavePlanChecks.CycleEnemies(wave));
            Assert.AreEqual(4f, WavePlanChecks.CycleSeconds(wave), 1e-4f);
            Assert.AreEqual(20f / 6f, WavePlanChecks.EnemiesPerSecond(wave), 1e-4f);
            // 30体目 = 2周目（6秒から）の10体目なので、6〜10秒の間。
            var seconds = WavePlanChecks.SecondsUntilTargetSpawned(wave);
            Assert.Greater(seconds, 6f);
            Assert.Less(seconds, 10f);
        }

        [Test]
        public void NotRepeatingAndTooFewNeverEnds()
        {
            var issues = WavePlanChecks.CheckWaves(new List<WaveDefinition> { Wave(30, false, Entry(0f, 20, 4f)) });
            Assert.IsTrue(issues.Any(i => i.Level == WavePlanIssueLevel.Error && i.Wave == 0));
            Assert.AreEqual(-1f, WavePlanChecks.SecondsUntilTargetSpawned(Wave(30, false, Entry(0f, 20, 4f))));
        }

        [Test]
        public void TightMarginIsAWarning()
        {
            var issues = WavePlanChecks.CheckWaves(new List<WaveDefinition> { Wave(30, false, Entry(0f, 32, 4f)) });
            Assert.IsTrue(issues.Any(i => i.Level == WavePlanIssueLevel.Warning));
            Assert.IsFalse(issues.Any(i => i.Level == WavePlanIssueLevel.Error));
        }

        [Test]
        public void EmptyWavesAndMissingPointsAreReported()
        {
            var issues = WavePlanChecks.CheckWaves(new List<WaveDefinition> { Wave(10, true), Wave(10, true, Entry(0f, 5, 1f, "B")) }, new HashSet<string> { "A" });
            Assert.IsTrue(issues.Any(i => i.Wave == 0 && i.Level == WavePlanIssueLevel.Error));
            Assert.IsTrue(issues.Any(i => i.Wave == 1 && i.Message.Contains("B")));
            Assert.IsTrue(WavePlanChecks.CheckWaves(new List<WaveDefinition>()).Any(i => i.Wave == -1));
        }
    }
}
