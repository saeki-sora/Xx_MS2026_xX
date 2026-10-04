using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 群衆の「写真」(スナップショット)の圧縮に使う計算(Unity API非依存・EditModeテスト対象)。
    ///
    /// 位置は1/500単位の整数。前の2枚の写真から「同じ速さで進んだら」の位置を予想し(Predict)、実際との差だけを送る。
    /// 群衆は滑らかに流れるので差はほとんど0〜数単位になり、指数ゴロム符号(ExpGolomb)で1軸あたり数ビットに縮む。
    /// 予想は整数だけで計算する(HostとClientで1単位でも食い違うと、以後ずっとずれるため。小数の計算はCPUで結果が変わり得る)。
    ///
    /// 差は 2^shift 単位に丸めて送る(位置の細かさ。shift=2 で0.008ワールド単位≒画面の1/3ピクセル)。Hostは丸めた後の位置
    /// (Reconstruct)を次の予想に使うので、丸めの誤差は溜まらない(常に±半単位以内)。1段粗くするごとに1軸1ビット減る。
    /// </summary>
    public static class SwarmSnapshotMath
    {
        /// <summary>写真の間隔の比(今回/前回)を1024倍した整数の上限(4倍まで)。</summary>
        public const int MaxRatioQ = 4096;

        /// <summary>予想に使う指数ゴロム符号の次数の上限(0〜この値から、1枚ごとに一番短くなる物を選ぶ)。</summary>
        public const int MaxGolombOrder = 7;

        public static int Quantize(float value)
        {
            return SwarmNetQuantize.ToShort(value);
        }

        /// <summary>写真の間隔の比(今回/前回)を1024倍した整数。前回の間隔が分からなければ0(=止まっていると予想)。</summary>
        public static int RatioQ(double now, double previous, double beforePrevious)
        {
            var last = previous - beforePrevious;
            if (last <= 1e-6)
            {
                return 0;
            }

            var ratio = math.round((now - previous) / last * 1024.0);
            return (int)math.clamp(ratio, 0.0, MaxRatioQ);
        }

        /// <summary>前の2枚(h1=直前、h2=その前)から、今回の位置を予想する(整数のみ)。</summary>
        public static int2 Predict(int2 h1, int2 h2, int ratioQ)
        {
            var delta = h1 - h2;
            return h1 + ((delta * ratioQ + 512) >> 10);
        }

        /// <summary>差を 2^shift 単位に丸める(四捨五入。負の数も整数のシフトだけで計算し、HostとClientで必ず一致させる)。</summary>
        public static int QuantizeResidual(int residual, int shift)
        {
            return (residual + ((1 << shift) >> 1)) >> shift;
        }

        public static int2 QuantizeResidual(int2 residual, int shift)
        {
            return new int2(QuantizeResidual(residual.x, shift), QuantizeResidual(residual.y, shift));
        }

        /// <summary>予想に丸めた差を足して位置を戻す(16bitの範囲に収める)。</summary>
        public static int2 Reconstruct(int2 predicted, int2 quantizedResidual, int shift)
        {
            return math.clamp(predicted + (quantizedResidual << shift), short.MinValue, short.MaxValue);
        }

        public static uint ZigZag(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        public static int UnZigZag(uint value)
        {
            return (int)(value >> 1) ^ -(int)(value & 1u);
        }

        /// <summary>k次の指数ゴロム符号で value を書いたときのビット数。</summary>
        public static int ExpGolombBits(uint value, int k)
        {
            var w = (ulong)value + (1UL << k);
            var n = 63 - math.lzcnt(w);
            return 2 * n - k + 1;
        }

        /// <summary>1体分の確かめ用の値。全員分を足し合わせて(順番によらない)、HostとClientの写真が一致しているか確かめる。</summary>
        public static uint Checksum(int id, int2 position)
        {
            return ((uint)id * 0x9E3779B1u) ^ ((uint)position.x * 0x85EBCA77u) ^ ((uint)position.y * 0xC2B2AE3Du);
        }

        public static byte ToAngleByte(float2 direction)
        {
            if (math.lengthsq(direction) < 1e-8f)
            {
                return 0;
            }

            var turns = math.atan2(direction.y, direction.x) / (2f * math.PI);
            return (byte)((int)math.round(turns * 256f) & 0xFF);
        }
    }

    /// <summary>ビット単位で書く(下位ビットから詰める)。</summary>
    public struct SwarmBitWriter
    {
        public NativeList<byte> Bytes;
        private ulong _acc;
        private int _count;

        public SwarmBitWriter(NativeList<byte> bytes)
        {
            Bytes = bytes;
            _acc = 0;
            _count = 0;
        }

        /// <summary>value の下位 count ビット(0〜32)を書く。</summary>
        public void Write(uint value, int count)
        {
            if (count <= 0)
            {
                return;
            }

            var mask = count >= 32 ? 0xFFFFFFFFUL : (1UL << count) - 1UL;
            _acc |= (value & mask) << _count;
            _count += count;
            while (_count >= 8)
            {
                Bytes.Add((byte)_acc);
                _acc >>= 8;
                _count -= 8;
            }
        }

        /// <summary>k次の指数ゴロム符号: (n-k)個の0、1、続いて w=value+2^k の下位nビット(nはwの最上位ビットの位置)。</summary>
        public void WriteExpGolomb(uint value, int k)
        {
            var w = (ulong)value + (1UL << k);
            var n = 63 - math.lzcnt(w);
            Write(0u, n - k);
            Write(1u, 1);
            Write((uint)(w & ((1UL << n) - 1UL)), n);
        }

        public void Flush()
        {
            if (_count > 0)
            {
                Bytes.Add((byte)_acc);
                _acc = 0;
                _count = 0;
            }
        }
    }

    /// <summary>ビット単位で読む(SwarmBitWriterの逆)。データが足りなければ Overrun が立つ。</summary>
    public struct SwarmBitReader
    {
        private NativeArray<byte> _bytes;
        private int _position;
        private ulong _acc;
        private int _count;

        public bool Overrun;

        public SwarmBitReader(NativeArray<byte> bytes)
        {
            _bytes = bytes;
            _position = 0;
            _acc = 0;
            _count = 0;
            Overrun = false;
        }

        private void Refill()
        {
            while (_count <= 56 && _position < _bytes.Length)
            {
                _acc |= (ulong)_bytes[_position++] << _count;
                _count += 8;
            }
        }

        public uint Read(int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (_count < count)
            {
                Refill();
                if (_count < count)
                {
                    Overrun = true;
                    return 0;
                }
            }

            var mask = count >= 32 ? 0xFFFFFFFFUL : (1UL << count) - 1UL;
            var value = (uint)(_acc & mask);
            _acc >>= count;
            _count -= count;
            return value;
        }

        public uint ReadExpGolomb(int k)
        {
            var zeros = 0;
            while (Read(1) == 0u)
            {
                if (Overrun || ++zeros > 40)
                {
                    Overrun = true;
                    return 0;
                }
            }

            var n = zeros + k;
            if (n > 32)
            {
                Overrun = true;
                return 0;
            }

            var low = n == 32 ? (ulong)Read(32) : Read(n);
            var w = (1UL << n) | low;
            return (uint)(w - (1UL << k));
        }
    }

    /// <summary>途中参加・ずれの立て直し用の「全員分の写真」1体分(出現の情報＋直前2枚の位置)。</summary>
    public struct SwarmKeyframeEntry : Unity.Netcode.INetworkSerializeByMemcpy
    {
        /// <summary>種類・直前の位置(X,Y)・速さのばらつき・アニメの位置・向き。Kind は Spawn。</summary>
        public SwarmNetEvent Spawn;

        /// <summary>その1枚前の位置。</summary>
        public short X2;

        public short Y2;
    }

    /// <summary>
    /// Host: 1枚の写真を作る。Storage(今の全員)と、前回の写真の状態(alivePrev/h1/h2)を比べて、
    /// 消えた敵・新しく出た敵・続いている敵の位置の差(ビット列)を出し、状態を今回の写真に進める。
    /// 続いている敵は番号の小さい順に並べる(Clientも同じ順で読む)。
    /// </summary>
    [BurstCompile]
    public struct SwarmSnapshotEncodeJob : IJob
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<float> animTime;
        [ReadOnly] public NativeArray<float2> facing;

        /// <summary>SwarmSystemの種類番号 → 通信用の種類番号(EnemyTypeNetIndex)。-1なら同期しない種類。</summary>
        [ReadOnly] public NativeArray<int> typeToNet;

        public int count;
        public int ratioQ;

        /// <summary>差を丸める単位(2^shift)。</summary>
        public int shift;

        public NativeArray<byte> alivePrev;
        public NativeArray<byte> aliveNow;
        public NativeArray<byte> removedSince;
        public NativeArray<byte> removedReason;
        public NativeArray<int2> h1;
        public NativeArray<int2> h2;
        public NativeArray<int2> current;
        public NativeArray<int> indexOfId;

        public NativeList<SwarmNetEvent> despawns;
        public NativeList<SwarmNetEvent> spawns;
        public NativeList<byte> bits;

        /// <summary>[0]=x軸の次数 [1]=y軸の次数 [2]=続いている敵の数 [3]=確かめ用の値(uintをintとして)。</summary>
        public NativeArray<int> header;

        public void Execute()
        {
            despawns.Clear();
            spawns.Clear();
            bits.Clear();
            var ids = aliveNow.Length;

            for (var id = 0; id < ids; id++)
            {
                aliveNow[id] = 0;
            }

            for (var i = 0; i < count; i++)
            {
                if (typeToNet[typeIdx[i]] < 0)
                {
                    continue;
                }

                var id = netId[i];
                aliveNow[id] = 1;
                indexOfId[id] = i;
                var p = pos[i];
                current[id] = new int2(SwarmSnapshotMath.Quantize(p.x), SwarmSnapshotMath.Quantize(p.y));
            }

            // 消えた敵(一度消えて同じ番号で出直した敵も、古い方は消えたとして扱う)。
            for (var id = 0; id < ids; id++)
            {
                if (alivePrev[id] != 0 && (aliveNow[id] == 0 || removedSince[id] != 0))
                {
                    despawns.Add(new SwarmNetEvent
                    {
                        Id = (ushort)id,
                        Kind = SwarmNetEventKind.Despawn,
                        TypeOrReason = removedSince[id] != 0 ? removedReason[id] : (byte)EnemyRemovalReason.Removed
                    });
                }
            }

            // 続いている敵の差の大きさから、一番短くなる符号の次数を選ぶ。
            var costX = new FixedList64Bytes<int>();
            var costY = new FixedList64Bytes<int>();
            for (var k = 0; k <= SwarmSnapshotMath.MaxGolombOrder; k++)
            {
                costX.Add(0);
                costY.Add(0);
            }

            var continuing = 0;
            for (var id = 0; id < ids; id++)
            {
                if (!Continuing(id))
                {
                    continue;
                }

                continuing++;
                var residual = SwarmSnapshotMath.QuantizeResidual(current[id] - SwarmSnapshotMath.Predict(h1[id], h2[id], ratioQ), shift);
                var zx = SwarmSnapshotMath.ZigZag(residual.x);
                var zy = SwarmSnapshotMath.ZigZag(residual.y);
                for (var k = 0; k <= SwarmSnapshotMath.MaxGolombOrder; k++)
                {
                    costX[k] += SwarmSnapshotMath.ExpGolombBits(zx, k);
                    costY[k] += SwarmSnapshotMath.ExpGolombBits(zy, k);
                }
            }

            var kx = 0;
            var ky = 0;
            for (var k = 1; k <= SwarmSnapshotMath.MaxGolombOrder; k++)
            {
                if (costX[k] < costX[kx])
                {
                    kx = k;
                }

                if (costY[k] < costY[ky])
                {
                    ky = k;
                }
            }

            var writer = new SwarmBitWriter(bits);
            for (var id = 0; id < ids; id++)
            {
                if (!Continuing(id))
                {
                    continue;
                }

                var predicted = SwarmSnapshotMath.Predict(h1[id], h2[id], ratioQ);
                var residual = SwarmSnapshotMath.QuantizeResidual(current[id] - predicted, shift);
                writer.WriteExpGolomb(SwarmSnapshotMath.ZigZag(residual.x), kx);
                writer.WriteExpGolomb(SwarmSnapshotMath.ZigZag(residual.y), ky);

                // Clientが復元する位置(丸めた後)を、以後の予想の元にする(丸めの誤差を溜めない)。
                current[id] = SwarmSnapshotMath.Reconstruct(predicted, residual, shift);
            }

            writer.Flush();

            // 新しく出た敵(Clientはこの写真の位置に出す)。続いている敵の位置の履歴を進める。
            var checksum = 0u;
            for (var id = 0; id < ids; id++)
            {
                if (aliveNow[id] == 0)
                {
                    alivePrev[id] = 0;
                    removedSince[id] = 0;
                    continue;
                }

                var p = current[id];
                if (alivePrev[id] == 0 || removedSince[id] != 0)
                {
                    var index = indexOfId[id];
                    spawns.Add(new SwarmNetEvent
                    {
                        Id = (ushort)id,
                        Kind = SwarmNetEventKind.Spawn,
                        TypeOrReason = (byte)typeToNet[typeIdx[index]],
                        X = (short)p.x,
                        Y = (short)p.y,
                        SpeedScale = SwarmNetQuantize.ToHalf(speedScale[index]),
                        AnimStart = SwarmNetQuantize.ToHalf(animTime[index]),
                        Facing = SwarmSnapshotMath.ToAngleByte(facing[index])
                    });
                    h2[id] = p;
                }
                else
                {
                    h2[id] = h1[id];
                }

                h1[id] = p;
                alivePrev[id] = 1;
                removedSince[id] = 0;
                checksum += SwarmSnapshotMath.Checksum(id, p);
            }

            header[0] = kx;
            header[1] = ky;
            header[2] = continuing;
            header[3] = (int)checksum;
        }

        private bool Continuing(int id)
        {
            return alivePrev[id] != 0 && aliveNow[id] != 0 && removedSince[id] == 0;
        }
    }

    /// <summary>Host: 今の写真の状態(全員分の出現の情報＋直前2枚の位置)を、途中参加・立て直し用にまとめる。Encodeの直後に使う。</summary>
    [BurstCompile]
    public struct SwarmSnapshotKeyframeJob : IJob
    {
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<float> animTime;
        [ReadOnly] public NativeArray<float2> facing;
        [ReadOnly] public NativeArray<int> typeToNet;
        [ReadOnly] public NativeArray<byte> alive;
        [ReadOnly] public NativeArray<int2> h1;
        [ReadOnly] public NativeArray<int2> h2;
        [ReadOnly] public NativeArray<int> indexOfId;
        public NativeList<SwarmKeyframeEntry> output;

        public void Execute()
        {
            output.Clear();
            for (var id = 0; id < alive.Length; id++)
            {
                if (alive[id] == 0)
                {
                    continue;
                }

                var index = indexOfId[id];
                var p1 = h1[id];
                var p2 = h2[id];
                output.Add(new SwarmKeyframeEntry
                {
                    Spawn = new SwarmNetEvent
                    {
                        Id = (ushort)id,
                        Kind = SwarmNetEventKind.Spawn,
                        TypeOrReason = (byte)typeToNet[typeIdx[index]],
                        X = (short)p1.x,
                        Y = (short)p1.y,
                        SpeedScale = SwarmNetQuantize.ToHalf(speedScale[index]),
                        AnimStart = SwarmNetQuantize.ToHalf(animTime[index]),
                        Facing = SwarmSnapshotMath.ToAngleByte(facing[index])
                    },
                    X2 = (short)p2.x,
                    Y2 = (short)p2.y
                });
            }
        }
    }

    /// <summary>
    /// Client: 届いた写真(差のビット列)を、手元の前回の状態に当てはめて今回の位置を復元し、表示用の位置と速度を出す。
    /// 消えた敵 → 続いている敵(番号の小さい順にビット列を読む) → 新しく出た敵、の順に処理する(Hostと同じ順)。
    /// 数や確かめ用の値がHostと合わなければ result[0]=0 (状態が壊れたので、全員分の写真で立て直す)。
    /// </summary>
    [BurstCompile]
    public struct SwarmSnapshotDecodeJob : IJob
    {
        [ReadOnly] public NativeArray<byte> bits;
        [ReadOnly] public NativeArray<SwarmNetEvent> despawns;
        [ReadOnly] public NativeArray<SwarmNetEvent> spawns;
        public int kx;
        public int ky;
        public int ratioQ;
        public int shift;
        public int expectedContinuing;
        public uint expectedChecksum;

        /// <summary>前回の写真から今回までの、Hostでの経過秒(速度を出すのに使う)。</summary>
        public float hostDeltaSeconds;

        public NativeArray<byte> alive;
        public NativeArray<int2> h1;
        public NativeArray<int2> h2;
        public NativeArray<float2> snapPos;
        public NativeArray<float2> snapVel;

        /// <summary>[0]=成功なら1。</summary>
        public NativeArray<int> result;

        public void Execute()
        {
            result[0] = 0;
            for (var i = 0; i < despawns.Length; i++)
            {
                alive[despawns[i].Id] = 0;
            }

            var reader = new SwarmBitReader(bits);
            var continuing = 0;
            for (var id = 0; id < alive.Length; id++)
            {
                if (alive[id] == 0)
                {
                    continue;
                }

                continuing++;
                var rx = SwarmSnapshotMath.UnZigZag(reader.ReadExpGolomb(kx));
                var ry = SwarmSnapshotMath.UnZigZag(reader.ReadExpGolomb(ky));
                if (reader.Overrun)
                {
                    return;
                }

                var p = SwarmSnapshotMath.Reconstruct(SwarmSnapshotMath.Predict(h1[id], h2[id], ratioQ), new int2(rx, ry), shift);
                h2[id] = h1[id];
                h1[id] = p;
            }

            if (continuing != expectedContinuing)
            {
                return;
            }

            for (var i = 0; i < spawns.Length; i++)
            {
                var e = spawns[i];
                var p = new int2(e.X, e.Y);
                alive[e.Id] = 1;
                h1[e.Id] = p;
                h2[e.Id] = p;
            }

            result[0] = SwarmSnapshotState.Publish(alive, h1, h2, snapPos, snapVel, hostDeltaSeconds) == expectedChecksum ? 1 : 0;
        }
    }

    /// <summary>Client: 全員分の写真で、手元の状態を丸ごと置き換える。</summary>
    [BurstCompile]
    public struct SwarmSnapshotKeyframeApplyJob : IJob
    {
        [ReadOnly] public NativeArray<SwarmKeyframeEntry> entries;
        public float hostDeltaSeconds;
        public uint expectedChecksum;
        public NativeArray<byte> alive;
        public NativeArray<int2> h1;
        public NativeArray<int2> h2;
        public NativeArray<float2> snapPos;
        public NativeArray<float2> snapVel;
        public NativeArray<int> result;

        public void Execute()
        {
            for (var id = 0; id < alive.Length; id++)
            {
                alive[id] = 0;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                alive[e.Spawn.Id] = 1;
                h1[e.Spawn.Id] = new int2(e.Spawn.X, e.Spawn.Y);
                h2[e.Spawn.Id] = new int2(e.X2, e.Y2);
            }

            result[0] = SwarmSnapshotState.Publish(alive, h1, h2, snapPos, snapVel, hostDeltaSeconds) == expectedChecksum ? 1 : 0;
        }
    }

    public static class SwarmSnapshotState
    {
        /// <summary>復元した状態から表示用の位置と速度を出し、確かめ用の値を返す。</summary>
        public static uint Publish(NativeArray<byte> alive, NativeArray<int2> h1, NativeArray<int2> h2,
            NativeArray<float2> snapPos, NativeArray<float2> snapVel, float hostDeltaSeconds)
        {
            var checksum = 0u;
            var inverseScale = 1f / SwarmNetQuantize.PositionScale;
            var velocityScale = hostDeltaSeconds > 1e-4f ? inverseScale / hostDeltaSeconds : 0f;
            for (var id = 0; id < alive.Length; id++)
            {
                if (alive[id] == 0)
                {
                    continue;
                }

                var p1 = h1[id];
                snapPos[id] = new float2(p1.x, p1.y) * inverseScale;
                snapVel[id] = new float2(p1.x - h2[id].x, p1.y - h2[id].y) * velocityScale;
                checksum += SwarmSnapshotMath.Checksum(id, p1);
            }

            return checksum;
        }
    }
}
