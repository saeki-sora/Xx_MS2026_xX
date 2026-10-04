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
            Assert.IsNull(args.ReplicaSeparationIterations);
            Assert.IsNull(args.PacketQueueSize);
        }

        [Test]
        public void FullBenchmark_IsParsed()
        {
            var args = NetBenchmarkArgs.Parse(new[]
            {
                "-fortress-bench", "-fortress-bench-burst", "30000", "-fortress-bench-radius", "8.5",
                "-fortress-bench-delay", "2", "-fortress-bench-clients", "3", "-fortress-bench-quit", "45",
                "-fortress-bench-no-wave", "-fortress-swarm-spawns-per-frame", "0",
                "-fortress-replica-separation", "-1", "-fortress-net-packet-queue", "128"
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
            Assert.AreEqual(-1, args.ReplicaSeparationIterations);
            Assert.AreEqual(128, args.PacketQueueSize);
        }

        [Test]
        public void OverridesAlone_DoNotStartBenchmark()
        {
            // 改善策の切り替えだけなら、計測(ログ・自動終了)は始めない。
            var args = NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-spawns-per-frame", "500" });

            Assert.IsFalse(args.AnyBenchmark);
            Assert.AreEqual(500, args.SpawnsPerFrame);
        }

        [Test]
        public void CorrectionTuning_IsParsed()
        {
            var args = NetBenchmarkArgs.Parse(new[]
            {
                "-fortress-swarm-smoothing", "blend", "-fortress-swarm-correction-cycle", "0.25",
                "-fortress-swarm-correction-min-cycle", "0.1", "-fortress-swarm-adaptive", "0"
            });

            Assert.AreEqual(SwarmReplicaSmoothing.BlendSimulation, args.Smoothing);
            Assert.AreEqual(0.25f, args.CorrectionCycleSeconds);
            Assert.AreEqual(0.1f, args.MinCorrectionCycleSeconds);
            Assert.AreEqual(false, args.AdaptiveCorrection);
            Assert.AreEqual(SwarmReplicaSmoothing.RenderOffset, NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-smoothing", "RENDER" }).Smoothing);
            Assert.IsNull(NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-smoothing", "other" }).Smoothing);
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
