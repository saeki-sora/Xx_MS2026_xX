using System.Collections.Generic;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class WaveSpawnClockTests
    {
        private static EnemySpawnEntry Entry(float time, int count, float interval) =>
            new EnemySpawnEntry { triggerTime = time, spawnPointId = "A", count = count, intervalBetweenSpawns = interval };

        private static EnemySpawnEntry ByDuration(float time, int count, float seconds) =>
            new EnemySpawnEntry { triggerTime = time, spawnPointId = "A", count = count, timing = EnemySpawnTiming.Duration, durationSeconds = seconds };

        [Test]
        public void Duration_SetsTheIntervalFromTheCount()
        {
            Assert.AreEqual(0.5f, EnemyWaveStats.Interval(ByDuration(0f, 5, 2f)), 1e-5f);
            Assert.AreEqual(0f, EnemyWaveStats.Interval(ByDuration(0f, 1, 2f)), 1e-5f, "1体だけなら間隔なし");
            Assert.AreEqual(12f, EnemyWaveStats.EndTime(ByDuration(10f, 40, 2f)), 1e-5f, "秒数で決めた行は、数を変えても終わる時刻が同じ");
        }

        [Test]
        public void ShortIntervals_SpawnSeveralPerFrame()
        {
            // 200体を2秒（間隔 0.01秒）。60fps でも2秒で全部出る（以前は1フレーム1体で3.3秒かかった）。
            var clock = new WaveSpawnClock();
            clock.Begin(new List<EnemySpawnEntry> { ByDuration(0f, 200, 2f) }, false, 0f);
            var output = new List<EnemySpawnEntry>();
            for (var frame = 0; frame < 121; frame++)
            {
                clock.Advance(1f / 60f, output);
            }

            Assert.AreEqual(200, output.Count);
            Assert.IsFalse(clock.IsRunning, "くり返さないなら、全部出したら止まる");
        }

        [Test]
        public void Loop_StartsAfterTheLastSpawnPlusTheGap()
        {
            // 0秒から 1秒おきに3体（最後は2秒）＋待ち 1秒 → 次の周は 3秒から。
            var clock = new WaveSpawnClock();
            clock.Begin(new List<EnemySpawnEntry> { Entry(0f, 3, 1f) }, true, 1f);
            var output = new List<EnemySpawnEntry>();
            clock.Advance(2.9f, output);
            Assert.AreEqual(3, output.Count, "2.9秒では まだ次の周は始まらない");
            clock.Advance(0.2f, output);
            Assert.AreEqual(4, output.Count, "3.1秒で次の周の1体目");
            Assert.AreEqual(1, clock.Cycle);
            Assert.IsTrue(clock.IsRunning);
        }

        [Test]
        public void BigStep_KeepsTheCountExact()
        {
            var clock = new WaveSpawnClock();
            clock.Begin(new List<EnemySpawnEntry> { Entry(0f, 3, 1f) }, true, 1f);
            var output = new List<EnemySpawnEntry>();
            clock.Advance(9.5f, output); // 周の長さ3秒 → 0,3,6,9 秒の周が始まっている
            Assert.AreEqual(3 * 3 + 1, output.Count);
            Assert.AreEqual(3, clock.Cycle);
        }

        [Test]
        public void StartTime_SkipsEarlierRows()
        {
            var clock = new WaveSpawnClock();
            clock.Begin(new List<EnemySpawnEntry> { Entry(0f, 5, 0f), Entry(4f, 2, 0f) }, false, 0f, 4f);
            var output = new List<EnemySpawnEntry>();
            clock.Advance(0.01f, output);
            Assert.AreEqual(2, output.Count);
        }

        [Test]
        public void ZeroLengthLoop_DoesNotHang()
        {
            var clock = new WaveSpawnClock();
            clock.Begin(new List<EnemySpawnEntry> { Entry(0f, 1, 0f) }, true, 0f);
            var output = new List<EnemySpawnEntry>();
            clock.Advance(1f, output);
            Assert.LessOrEqual(output.Count, 1 + (int)(1f / WaveSpawnClock.MinLoopPeriod));
        }
    }
}
