using NUnit.Framework;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuCountersTests
    {
        [Test]
        public void KeepsInsertionOrderAndValues()
        {
            var c = new CpuCounters();
            c.Add("b", 1f);
            c.Add("a", 2f);
            c.Add("b", 3f);
            c.Max("m", 5f);
            c.Max("m", 2f);
            c.Set("s", 7f);
            CollectionAssert.AreEqual(new[] { "b", "a", "m", "s" }, c.Keys);
            Assert.AreEqual(4f, c.Get("b"));
            Assert.AreEqual(5f, c.Get("m"));
            Assert.AreEqual(0f, c.Get("none"));
            Assert.IsFalse(c.Has("none"));
        }

        [Test]
        public void MaxOnNewKey_TakesValueEvenIfNegative()
        {
            var c = new CpuCounters();
            c.Max("m", -3f);
            Assert.AreEqual(-3f, c.Get("m"));
        }
    }

    public sealed class CpuStatsTests
    {
        [Test]
        public void EndWave_ClosesAndRaisesEvent()
        {
            var stats = new CpuStats();
            CpuWaveStats raised = null;
            stats.WaveFinished += w => raised = w;

            stats.BeginWave(2);
            stats.Add(CpuStats.FiringSeconds, 1.5f);
            stats.Tick(10f);
            var done = stats.EndWave(true);

            Assert.AreSame(done, raised);
            Assert.AreEqual(2, done.WaveIndex);
            Assert.IsTrue(done.Cleared);
            Assert.AreEqual(10f, done.Seconds);
            Assert.AreEqual(1.5f, done.Counters.Get(CpuStats.FiringSeconds));
            Assert.AreEqual(1, stats.Finished.Count);
            Assert.AreEqual(0f, stats.Current.Counters.Get(CpuStats.FiringSeconds)); // 次の記録は空から
        }

        [Test]
        public void BeginWave_DropsPartialCounts()
        {
            var stats = new CpuStats();
            stats.Add("x", 3f);
            stats.BeginWave(0);
            Assert.AreEqual(0f, stats.Current.Counters.Get("x"));
            Assert.AreEqual(0, stats.Finished.Count);
        }
    }

    public sealed class CpuCsvTests
    {
        [Test]
        public void Build_UnionOfKeysInFirstSeenOrder_BlankWhenMissing()
        {
            var a = new CpuResultRow();
            a.Fixed.Add("1");
            a.Fixed.Add("P1");
            a.Counters.Add("撃った秒", 1.234f);

            var b = new CpuResultRow();
            b.Fixed.Add("1");
            b.Fixed.Add("P2");
            b.Counters.Add("空振り秒", 2f);
            b.Counters.Add("撃った秒", 3f);

            var csv = CpuCsv.Build(new[] { "試合", "プレイヤー" }, new[] { a, b });
            var lines = csv.Split(new[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual("試合,プレイヤー,撃った秒,空振り秒", lines[0]);
            Assert.AreEqual("1,P1,1.23,", lines[1]);
            Assert.AreEqual("1,P2,3,2", lines[2]);
        }

        [Test]
        public void Escape_QuotesSpecialCharacters()
        {
            Assert.AreEqual("abc", CpuCsv.Escape("abc"));
            Assert.AreEqual("\"a,b\"", CpuCsv.Escape("a,b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", CpuCsv.Escape("say \"hi\""));
            Assert.AreEqual(string.Empty, CpuCsv.Escape(null));
        }

        [Test]
        public void Number_IsInvariant()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.AreEqual("1.5", CpuCsv.Number(1.5f));
                Assert.AreEqual("2", CpuCsv.Number(2.0001f));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void Accumulate_SumsButKeepsMaxForMaxKeys()
        {
            var total = new CpuCounters();
            var w1 = new CpuCounters();
            w1.Add("撃った秒", 2f);
            w1.Max("熱の最大%", 80f);
            var w2 = new CpuCounters();
            w2.Add("撃った秒", 3f);
            w2.Max("熱の最大%", 60f);
            CpuCsv.Accumulate(total, w1);
            CpuCsv.Accumulate(total, w2);
            Assert.AreEqual(5f, total.Get("撃った秒"));
            Assert.AreEqual(80f, total.Get("熱の最大%"));
        }
    }

    public sealed class CpuAutoplayOptionsTests
    {
        [Test]
        public void AutoplayTimescaleAndCsv()
        {
            var o = CpuLaunchOptions.Parse(new[] { "-autoplay", "10", "-timescale", "2.5", "-cpu-csv" });
            Assert.AreEqual(10, o.AutoplayMatches);
            Assert.AreEqual(2.5f, o.TimeScale);
            Assert.IsTrue(o.Csv);
            Assert.IsTrue(o.HasAny);
        }

        [Test]
        public void AutoplayWithoutCount_IsOneMatch()
        {
            Assert.AreEqual(1, CpuLaunchOptions.Parse(new[] { "-autoplay", "-cpu-level", "hard" }).AutoplayMatches);
        }

        [Test]
        public void BadTimescale_IsIgnored()
        {
            Assert.IsFalse(CpuLaunchOptions.Parse(new[] { "-timescale", "fast" }).TimeScale.HasValue);
            Assert.IsFalse(CpuLaunchOptions.Parse(new[] { "-timescale", "0" }).TimeScale.HasValue);
        }
    }
}
