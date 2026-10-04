using MS2026.Fortress.Net;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmSnapshotMathTests
    {
        [Test]
        public void ZigZag_RoundTrips()
        {
            foreach (var value in new[] { 0, 1, -1, 2, -2, 1000, -1000, 70000, -70000, int.MaxValue, int.MinValue })
            {
                Assert.AreEqual(value, SwarmSnapshotMath.UnZigZag(SwarmSnapshotMath.ZigZag(value)));
            }

            // 小さい値ほど小さい符号になる。
            Assert.AreEqual(0u, SwarmSnapshotMath.ZigZag(0));
            Assert.AreEqual(1u, SwarmSnapshotMath.ZigZag(-1));
            Assert.AreEqual(2u, SwarmSnapshotMath.ZigZag(1));
        }

        [Test]
        public void ExpGolombBits_MatchesDefinition()
        {
            Assert.AreEqual(1, SwarmSnapshotMath.ExpGolombBits(0u, 0));
            Assert.AreEqual(3, SwarmSnapshotMath.ExpGolombBits(1u, 0));
            Assert.AreEqual(3, SwarmSnapshotMath.ExpGolombBits(2u, 0));
            Assert.AreEqual(5, SwarmSnapshotMath.ExpGolombBits(3u, 0));
            Assert.AreEqual(3, SwarmSnapshotMath.ExpGolombBits(0u, 2));
            Assert.AreEqual(3, SwarmSnapshotMath.ExpGolombBits(3u, 2));
            Assert.AreEqual(5, SwarmSnapshotMath.ExpGolombBits(4u, 2));
        }

        [Test]
        public void Predict_ContinuesMotionWithIntervalRatio()
        {
            var h2 = new int2(100, 200);
            var h1 = new int2(110, 190);

            // 同じ間隔なら同じだけ進む。
            Assert.AreEqual(new int2(120, 180), SwarmSnapshotMath.Predict(h1, h2, 1024));

            // 間隔が半分なら半分だけ。
            Assert.AreEqual(new int2(115, 185), SwarmSnapshotMath.Predict(h1, h2, 512));

            // 比が0(前回の間隔が分からない)なら止まっていると予想。
            Assert.AreEqual(h1, SwarmSnapshotMath.Predict(h1, h2, 0));
        }

        [Test]
        public void RatioQ_IsClampedAndHandlesUnknownInterval()
        {
            Assert.AreEqual(1024, SwarmSnapshotMath.RatioQ(3.0, 2.0, 1.0));
            Assert.AreEqual(2048, SwarmSnapshotMath.RatioQ(4.0, 2.0, 1.0));
            Assert.AreEqual(SwarmSnapshotMath.MaxRatioQ, SwarmSnapshotMath.RatioQ(100.0, 2.0, 1.0));
            Assert.AreEqual(0, SwarmSnapshotMath.RatioQ(3.0, 2.0, 2.0));
        }

        [Test]
        public void Checksum_SumIsOrderIndependentAndSensitive()
        {
            var a = SwarmSnapshotMath.Checksum(1, new int2(5, 6)) + SwarmSnapshotMath.Checksum(2, new int2(7, 8));
            var b = SwarmSnapshotMath.Checksum(2, new int2(7, 8)) + SwarmSnapshotMath.Checksum(1, new int2(5, 6));
            var c = SwarmSnapshotMath.Checksum(2, new int2(7, 9)) + SwarmSnapshotMath.Checksum(1, new int2(5, 6));
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }
    }

    /// <summary>NativeArrayを使うので、Unityのテストランナーでのみ動く。</summary>
    public sealed class SwarmSnapshotCodecTests
    {
        [Test]
        public void BitWriterAndReader_RoundTripExpGolomb()
        {
            using var bytes = new NativeList<byte>(64, Allocator.Temp);
            var writer = new SwarmBitWriter(bytes);
            var values = new uint[] { 0, 1, 2, 3, 7, 100, 65535, 1u << 20 };
            foreach (var v in values)
            {
                for (var k = 0; k <= SwarmSnapshotMath.MaxGolombOrder; k++)
                {
                    writer.WriteExpGolomb(v, k);
                }
            }

            writer.Write(5u, 3);
            writer.Flush();

            var reader = new SwarmBitReader(bytes.AsArray());
            foreach (var v in values)
            {
                for (var k = 0; k <= SwarmSnapshotMath.MaxGolombOrder; k++)
                {
                    Assert.AreEqual(v, reader.ReadExpGolomb(k));
                }
            }

            Assert.AreEqual(5u, reader.Read(3));
            Assert.IsFalse(reader.Overrun);
        }

        // Host(Encode)とClient(Decode)で、出現・移動・消滅・同じ番号での出直しを3枚通して、位置が完全に一致するか。
        [Test]
        public void EncodeDecode_ThreeSnapshots_ReproduceHostPositions()
        {
            const int ids = SwarmSystem.MaxNetIds;
            using var alivePrev = new NativeArray<byte>(ids, Allocator.TempJob);
            using var aliveNow = new NativeArray<byte>(ids, Allocator.TempJob);
            // 書き換えるので using 宣言にはしない(最後に破棄)。
            var removedSince = new NativeArray<byte>(ids, Allocator.TempJob);
            var removedReason = new NativeArray<byte>(ids, Allocator.TempJob);
            using var h1 = new NativeArray<int2>(ids, Allocator.TempJob);
            using var h2 = new NativeArray<int2>(ids, Allocator.TempJob);
            using var current = new NativeArray<int2>(ids, Allocator.TempJob);
            using var indexOfId = new NativeArray<int>(ids, Allocator.TempJob);
            using var typeToNet = new NativeArray<int>(new[] { 0 }, Allocator.TempJob);
            using var header = new NativeArray<int>(4, Allocator.TempJob);
            using var despawns = new NativeList<SwarmNetEvent>(16, Allocator.TempJob);
            using var spawns = new NativeList<SwarmNetEvent>(16, Allocator.TempJob);
            using var bits = new NativeList<byte>(256, Allocator.TempJob);

            using var cAlive = new NativeArray<byte>(ids, Allocator.TempJob);
            using var cH1 = new NativeArray<int2>(ids, Allocator.TempJob);
            using var cH2 = new NativeArray<int2>(ids, Allocator.TempJob);
            using var snapPos = new NativeArray<float2>(ids, Allocator.TempJob);
            using var snapVel = new NativeArray<float2>(ids, Allocator.TempJob);
            using var result = new NativeArray<int>(1, Allocator.TempJob);

            var frames = new[]
            {
                (ids: new[] { 3, 7, 9 }, positions: new[] { new float2(1f, 1f), new float2(-2f, 0.5f), new float2(10f, -3f) }, removed: new int[0]),
                (ids: new[] { 3, 7, 9 }, positions: new[] { new float2(1.1f, 1.05f), new float2(-1.9f, 0.4f), new float2(10.2f, -3.1f) }, removed: new int[0]),
                // 7 が消えて同じ番号で別の場所に出直し、9 は消え、12 が新しく出る。
                (ids: new[] { 3, 7, 12 }, positions: new[] { new float2(1.23f, 1.1f), new float2(5f, 5f), new float2(0f, 0f) }, removed: new[] { 7, 9 })
            };

            var ratios = new[] { 0, 0, 1024 };
            try
            {
                RunFrames(frames, ratios, removedSince, removedReason, alivePrev, aliveNow, h1, h2, current, indexOfId, typeToNet, header,
                    despawns, spawns, bits, cAlive, cH1, cH2, snapPos, snapVel, result);
            }
            finally
            {
                removedSince.Dispose();
                removedReason.Dispose();
            }
        }

        private static void RunFrames((int[] ids, float2[] positions, int[] removed)[] frames, int[] ratios,
            NativeArray<byte> removedSince, NativeArray<byte> removedReason, NativeArray<byte> alivePrev, NativeArray<byte> aliveNow,
            NativeArray<int2> h1, NativeArray<int2> h2, NativeArray<int2> current, NativeArray<int> indexOfId, NativeArray<int> typeToNet,
            NativeArray<int> header, NativeList<SwarmNetEvent> despawns, NativeList<SwarmNetEvent> spawns, NativeList<byte> bits,
            NativeArray<byte> cAlive, NativeArray<int2> cH1, NativeArray<int2> cH2, NativeArray<float2> snapPos, NativeArray<float2> snapVel,
            NativeArray<int> result)
        {
            for (var f = 0; f < frames.Length; f++)
            {
                var frame = frames[f];
                foreach (var removed in frame.removed)
                {
                    removedSince[removed] = 1;
                    removedReason[removed] = (byte)EnemyRemovalReason.Died;
                }

                using var netId = new NativeArray<int>(frame.ids, Allocator.TempJob);
                using var pos = new NativeArray<float2>(frame.positions, Allocator.TempJob);
                using var typeIdx = new NativeArray<int>(frame.ids.Length, Allocator.TempJob);
                using var speedScale = new NativeArray<float>(frame.ids.Length, Allocator.TempJob);
                using var animTime = new NativeArray<float>(frame.ids.Length, Allocator.TempJob);
                using var facing = new NativeArray<float2>(frame.ids.Length, Allocator.TempJob);

                new SwarmSnapshotEncodeJob
                {
                    netId = netId, pos = pos, typeIdx = typeIdx, speedScale = speedScale, animTime = animTime, facing = facing,
                    typeToNet = typeToNet, count = frame.ids.Length, ratioQ = ratios[f],
                    alivePrev = alivePrev, aliveNow = aliveNow, removedSince = removedSince, removedReason = removedReason,
                    h1 = h1, h2 = h2, current = current, indexOfId = indexOfId,
                    despawns = despawns, spawns = spawns, bits = bits, header = header
                }.Run();

                new SwarmSnapshotDecodeJob
                {
                    bits = bits.AsArray(), despawns = despawns.AsArray(), spawns = spawns.AsArray(),
                    kx = header[0], ky = header[1], ratioQ = ratios[f], expectedContinuing = header[2], expectedChecksum = (uint)header[3],
                    hostDeltaSeconds = 1f / 30f,
                    alive = cAlive, h1 = cH1, h2 = cH2, snapPos = snapPos, snapVel = snapVel, result = result
                }.Run();

                Assert.AreEqual(1, result[0], $"frame {f}");
                for (var i = 0; i < frame.ids.Length; i++)
                {
                    var id = frame.ids[i];
                    Assert.AreEqual(1, cAlive[id]);
                    Assert.AreEqual(frame.positions[i].x, snapPos[id].x, 0.002f);
                    Assert.AreEqual(frame.positions[i].y, snapPos[id].y, 0.002f);
                }

                if (f == 2)
                {
                    Assert.AreEqual(0, cAlive[9]);
                    Assert.AreEqual(2, despawns.Length); // 7(出直し前の方) と 9
                    Assert.AreEqual(2, spawns.Length);   // 7(出直し後) と 12
                    Assert.AreEqual(1, header[2]);       // 続いているのは 3 だけ
                }
            }
        }
    }
}
