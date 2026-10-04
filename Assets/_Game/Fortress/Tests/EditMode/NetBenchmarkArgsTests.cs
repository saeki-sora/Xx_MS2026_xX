using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class NetBenchmarkArgsTests
    {
        [Test]
        public void NoArgs_NoBenchmarkAndDefaults()
        {
            var args = NetBenchmarkArgs.Parse(new[] { "MS2026.exe", "-fortress-start", "host" });

            Assert.IsFalse(args.AnyBenchmark);
            Assert.AreEqual(6f, args.BurstRadius);
            Assert.AreEqual(3f, args.BurstDelaySeconds);
            Assert.IsNull(args.SpawnsPerFrame);
            Assert.IsNull(args.PacketQueueSize);
            Assert.IsNull(args.SnapshotRate);
        }

        [Test]
        public void FullBenchmark_IsParsed()
        {
            var args = NetBenchmarkArgs.Parse(new[]
            {
                "-fortress-bench", "-fortress-bench-burst", "30000", "-fortress-bench-radius", "8.5",
                "-fortress-bench-delay", "2", "-fortress-bench-clients", "3", "-fortress-bench-quit", "45",
                "-fortress-bench-no-wave", "-fortress-swarm-spawns-per-frame", "0", "-fortress-net-packet-queue", "128"
            });

            Assert.IsTrue(args.AnyBenchmark);
            Assert.IsTrue(args.LogEnabled);
            Assert.AreEqual(30000, args.BurstCount);
            Assert.AreEqual(8.5f, args.BurstRadius);
            Assert.AreEqual(2f, args.BurstDelaySeconds);
            Assert.AreEqual(3, args.ExpectedClients);
            Assert.AreEqual(45f, args.QuitAfterSeconds);
            Assert.IsTrue(args.StopWaves);
            Assert.AreEqual(0, args.SpawnsPerFrame);
            Assert.AreEqual(128, args.PacketQueueSize);
        }

        [Test]
        public void OverridesAlone_DoNotStartBenchmark()
        {
            // 改善策の切り替えだけなら、計測(ログ・自動終了)は始めない。
            var args = NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-spawns-per-frame", "500", "-fortress-swarm-snapshot-rate", "20" });

            Assert.IsFalse(args.AnyBenchmark);
            Assert.AreEqual(500, args.SpawnsPerFrame);
            Assert.AreEqual(20f, args.SnapshotRate);
        }

        [Test]
        public void SnapshotOptions_AreParsed()
        {
            var args = NetBenchmarkArgs.Parse(new[]
            {
                "-fortress-swarm-snapshot-rate", "20", "-fortress-swarm-snapshot-lead", "0.02",
                "-fortress-swarm-snapshot-adaptive", "0", "-fortress-swarm-snapshot-shift", "1"
            });

            Assert.AreEqual(20f, args.SnapshotRate);
            Assert.AreEqual(0.02f, args.SnapshotLeadSeconds);
            Assert.AreEqual(false, args.AdaptiveSnapshotQuality);
            Assert.AreEqual(1, args.SnapshotPrecisionShift);
            Assert.IsNull(NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-snapshot-adaptive", "x" }).AdaptiveSnapshotQuality);
        }

        [Test]
        public void BisectOptions_AreParsed()
        {
            var args = NetBenchmarkArgs.Parse(new[]
            {
                "-fortress-bench-no-ongui", "-fortress-bench-hide-ddrive-overlay", "-fortress-bench-disable", "LocalTurretGaugeHud,,FortressCameraRig"
            });

            Assert.IsTrue(args.NoOnGui);
            Assert.IsTrue(args.HideDDriveOverlay);
            CollectionAssert.AreEqual(new[] { "LocalTurretGaugeHud", "FortressCameraRig" }, args.DisableTypes);
            CollectionAssert.AreEqual(new[] { "A", "B" }, NetBenchmarkArgs.Parse(new[] { "-fortress-bench-disable", "A+B" }).DisableTypes);
        }

        [Test]
        public void InvalidOrMissingValues_AreIgnored()
        {
            var args = NetBenchmarkArgs.Parse(new[] { "-fortress-bench-radius", "abc", "-fortress-bench-burst" });

            Assert.AreEqual(6f, args.BurstRadius);
            Assert.AreEqual(0, args.BurstCount);
        }
    }
}
