using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>群衆をClientへどう伝えるか(2026-10-05)。</summary>
    public enum SwarmReplicationMode
    {
        /// <summary>全員の位置を同じ瞬間の写真として1秒に何十回も送り、Clientは先読みして置く(既定。震え・隙間が出ない)。</summary>
        Snapshot = 0,

        /// <summary>Clientも動きを計算し、Hostの位置で補正する(段階5まで。比較用)。</summary>
        Corrections = 1
    }

    /// <summary>
    /// 群衆の同期の「写真方式」(2026-10-05、ユーザー決定)。SwarmNetworkHub の続き。
    ///
    /// 補正方式では、Clientが自分で計算した位置とHostの位置を敵ごとにばらばらに混ぜていたため、密集した群衆では
    /// 移した敵と隣の敵が重なって押し合いで弾け、全体が震えて隙間だらけになっていた(ズレの平均0.4 ≒ 敵1体分の間隔)。
    ///
    /// 写真方式:
    /// ・Host: snapshotRate 回/秒、全員の位置を同じ瞬間に撮り(SwarmSnapshotEncodeJob)、全Clientへ同じ内容を確実な届け方で送る。
    ///   位置は前の2枚から予想した位置との差だけを、指数ゴロム符号で数ビットに詰める(SwarmSnapshotMath)。出現・消滅も同じ写真に入れる。
    ///   途中参加・ずれの立て直しには、全員分の写真(キーフレーム)をそのClientへ送ってから、以後の写真を送る。
    /// ・Client: 写真を復元し(SwarmSnapshotDecodeJob)、SwarmSystemは押し合いを計算せず、最新の写真を届くまでの遅れの分だけ
    ///   先読みした位置に置く(SwarmSnapshotFollowJob)。全員が同じ瞬間から同じ流れで進むので重ならず、遅れは通信の時間だけになる。
    ///   確かめ用の値が合わない・番号が飛んだときは、全員分の写真を頼んで立て直す。
    /// </summary>
    public sealed partial class SwarmNetworkHub
    {
        // Clientの時計とHostの時計の差の基準を、ゆっくり忘れていく速さ(秒/秒)。届く遅れが増えたときに合わせ直すため。
        private const double ClockCreepPerSecond = 0.002;

        private static readonly int SnapshotEventSize = UnsafeUtility.SizeOf<SwarmNetEvent>();
        private static readonly int KeyframeEntrySize = UnsafeUtility.SizeOf<SwarmKeyframeEntry>();

        // Host側
        private SnapshotEncoder _encoder;
        private readonly List<ulong> _snapshotClients = new();
        private float _snapshotAccumulator;
        private bool _encoderIsReset = true;

        // Client側
        private SnapshotDecoder _decoder;
        private bool _hasClock;
        private double _clockOffset;
        private double _lastClockSample;

        private bool UsesSnapshots => replicationMode == SwarmReplicationMode.Snapshot;

        // Host: 写真の状態(前回の写真に写っていた敵と、直前2枚の位置)。全Clientで共通。
        private sealed class SnapshotEncoder : IDisposable
        {
            public NativeArray<byte> AlivePrev = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<byte> AliveNow = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<byte> RemovedSince = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<byte> RemovedReason = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int2> H1 = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int2> H2 = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int2> Current = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int> IndexOfId = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int> TypeToNet = new(SwarmLimits.MaxTypes, Allocator.Persistent);
            public NativeArray<int> Header = new(4, Allocator.Persistent);
            public NativeList<SwarmNetEvent> Despawns = new(1024, Allocator.Persistent);
            public NativeList<SwarmNetEvent> Spawns = new(1024, Allocator.Persistent);
            public NativeList<byte> Bits = new(65536, Allocator.Persistent);
            public NativeList<SwarmKeyframeEntry> Keyframe = new(1024, Allocator.Persistent);
            public double T1 = -1d;
            public double T2 = -1d;
            public uint Serial;

            // 写真の流れを最初からやり直す(つながっているClientがいなくなったとき)。次の写真では全員が「新しく出た」扱いになる。
            public void Reset()
            {
                for (var i = 0; i < AlivePrev.Length; i++)
                {
                    AlivePrev[i] = 0;
                    RemovedSince[i] = 0;
                }

                T1 = T2 = -1d;
            }

            public void Dispose()
            {
                AlivePrev.Dispose();
                AliveNow.Dispose();
                RemovedSince.Dispose();
                RemovedReason.Dispose();
                H1.Dispose();
                H2.Dispose();
                Current.Dispose();
                IndexOfId.Dispose();
                TypeToNet.Dispose();
                Header.Dispose();
                Despawns.Dispose();
                Spawns.Dispose();
                Bits.Dispose();
                Keyframe.Dispose();
            }
        }

        // Client: 復元した写真の状態と、表示用に出した位置・速度(番号ごと)。
        private sealed class SnapshotDecoder : IDisposable
        {
            public NativeArray<byte> Alive = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int2> H1 = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int2> H2 = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<float2> SnapPos = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<float2> SnapVel = new(SwarmSystem.MaxNetIds, Allocator.Persistent);
            public NativeArray<int> Result = new(1, Allocator.Persistent);
            public uint Serial;
            public bool Synced;

            public void Dispose()
            {
                Alive.Dispose();
                H1.Dispose();
                H2.Dispose();
                SnapPos.Dispose();
                SnapVel.Dispose();
                Result.Dispose();
            }
        }

        private void DisposeSnapshotState()
        {
            _encoder?.Dispose();
            _encoder = null;
            _decoder?.Dispose();
            _decoder = null;
            _snapshotClients.Clear();
        }

        // ------------------------------------------------------------------ Host

        private void BeginSnapshotHost()
        {
            _encoder ??= new SnapshotEncoder();
            _encoder.Reset();
            _encoderIsReset = true;
            _snapshotClients.Clear();
            _snapshotAccumulator = 0f;
        }

        private void RecordRemovalsForSnapshot(IReadOnlyList<SwarmSystem.RemovalRecord> records)
        {
            if (_encoder == null)
            {
                return;
            }

            for (var i = 0; i < records.Count; i++)
            {
                var id = records[i].Id;
                if (_encoder.RemovedSince[id] == 0)
                {
                    _encoder.RemovedSince[id] = 1;
                    _encoder.RemovedReason[id] = (byte)records[i].Reason;
                }
            }
        }

        // SwarmSystemの計算が止まっている瞬間に呼ぶ。snapshotRate 回/秒、写真を撮って送る。
        private void SnapshotHostTick()
        {
            RefreshRemoteClients();
            for (var i = _snapshotClients.Count - 1; i >= 0; i--)
            {
                if (!_remoteClients.Contains(_snapshotClients[i]))
                {
                    _snapshotClients.RemoveAt(i);
                }
            }

            NetTrafficStats.SwarmFullStateTransfers = _fullStateRequests.Count;
            if (_remoteClients.Count == 0)
            {
                if (!_encoderIsReset)
                {
                    _encoder.Reset();
                    _encoderIsReset = true;
                }

                _snapshotAccumulator = 0f;
                return;
            }

            _snapshotAccumulator = Mathf.Min(_snapshotAccumulator + Time.unscaledDeltaTime * snapshotRate, 2f);
            if (_snapshotAccumulator < 1f)
            {
                return;
            }

            _snapshotAccumulator -= 1f;
            _encoderIsReset = false;

            var now = Time.realtimeSinceStartupAsDouble;
            var ratioQ = _encoder.T1 >= 0d && _encoder.T2 >= 0d ? SwarmSnapshotMath.RatioQ(now, _encoder.T1, _encoder.T2) : 0;
            FillTypeToNet();

            var storage = _swarm.Storage;
            new SwarmSnapshotEncodeJob
            {
                netId = storage.netId,
                pos = storage.pos,
                typeIdx = storage.typeIdx,
                speedScale = storage.speedScale,
                animTime = storage.animTime,
                facing = storage.facing,
                typeToNet = _encoder.TypeToNet,
                count = storage.Count,
                ratioQ = ratioQ,
                alivePrev = _encoder.AlivePrev,
                aliveNow = _encoder.AliveNow,
                removedSince = _encoder.RemovedSince,
                removedReason = _encoder.RemovedReason,
                h1 = _encoder.H1,
                h2 = _encoder.H2,
                current = _encoder.Current,
                indexOfId = _encoder.IndexOfId,
                despawns = _encoder.Despawns,
                spawns = _encoder.Spawns,
                bits = _encoder.Bits,
                header = _encoder.Header
            }.Schedule().Complete();

            _encoder.T2 = _encoder.T1;
            _encoder.T1 = now;
            _encoder.Serial++;
            var hostDelta = _encoder.T2 >= 0d ? (float)(_encoder.T1 - _encoder.T2) : 0f;
            var checksum = (uint)_encoder.Header[3];

            if (_snapshotClients.Count > 0)
            {
                SendSnapshot(ratioQ, hostDelta, checksum);
            }

            if (_fullStateRequests.Count > 0)
            {
                SendKeyframes(hostDelta, checksum);
            }
        }

        private void FillTypeToNet()
        {
            for (var t = 0; t < _encoder.TypeToNet.Length; t++)
            {
                var definition = _swarm.GetRegisteredType(t);
                var netIndex = -1;
                if (definition != null && !_types.TryGetIndex(definition, out netIndex))
                {
                    LogUnsupportedOnce();
                    netIndex = -1;
                }

                _encoder.TypeToNet[t] = netIndex;
            }
        }

        private void SendSnapshot(int ratioQ, float hostDelta, uint checksum)
        {
            var despawns = _encoder.Despawns.AsArray();
            var spawns = _encoder.Spawns.AsArray();
            var bits = _encoder.Bits.AsArray();
            var size = 64 + (despawns.Length + spawns.Length) * SnapshotEventSize + bits.Length;
            using var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmSnapshot, size);
            writer.WriteValueSafe(_encoder.Serial);
            writer.WriteValueSafe(_encoder.T1);
            writer.WriteValueSafe(hostDelta);
            writer.WriteValueSafe(ratioQ);
            writer.WriteByteSafe((byte)_encoder.Header[0]);
            writer.WriteByteSafe((byte)_encoder.Header[1]);
            writer.WriteValueSafe(_encoder.Header[2]);
            writer.WriteValueSafe(checksum);
            writer.WriteValueSafe(despawns, default(FastBufferWriter.ForGeneric));
            writer.WriteValueSafe(spawns, default(FastBufferWriter.ForGeneric));
            writer.WriteValueSafe(bits, default(FastBufferWriter.ForGeneric));

            for (var i = 0; i < _snapshotClients.Count; i++)
            {
                FortressNetMessages.Send(NetworkManager, _snapshotClients[i], writer, ReliableDelivery);
            }

            var bytes = (long)writer.Length * _snapshotClients.Count;
            NetTrafficStats.Sent(bytes);
            NetTrafficStats.SwarmEventsSent += despawns.Length + spawns.Length;
            NetTrafficStats.SwarmSnapshotsSent++;
            NetTrafficStats.SwarmSnapshotBytes = writer.Length;
            var continuing = _encoder.Header[2];
            NetTrafficStats.SwarmSnapshotBitsPerAgent = continuing > 0 ? bits.Length * 8f / continuing : 0f;
        }

        // 写真を頼んできたClientへ、今の全員分の写真を送り、以後の写真の送り先に加える(同じ確実な届け方なので、この後の写真は必ず後に届く)。
        private void SendKeyframes(float hostDelta, uint checksum)
        {
            var storage = _swarm.Storage;
            new SwarmSnapshotKeyframeJob
            {
                typeIdx = storage.typeIdx,
                speedScale = storage.speedScale,
                animTime = storage.animTime,
                facing = storage.facing,
                typeToNet = _encoder.TypeToNet,
                alive = _encoder.AlivePrev,
                h1 = _encoder.H1,
                h2 = _encoder.H2,
                indexOfId = _encoder.IndexOfId,
                output = _encoder.Keyframe
            }.Schedule().Complete();

            var entries = _encoder.Keyframe.AsArray();
            using var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmKeyframe, 64 + entries.Length * KeyframeEntrySize);
            writer.WriteValueSafe(_encoder.Serial);
            writer.WriteValueSafe(_encoder.T1);
            writer.WriteValueSafe(hostDelta);
            writer.WriteValueSafe(_types.LayoutHash);
            writer.WriteValueSafe(checksum);
            writer.WriteValueSafe(entries, default(FastBufferWriter.ForGeneric));

            foreach (var clientId in _fullStateRequests)
            {
                if (!_remoteClients.Contains(clientId))
                {
                    continue;
                }

                FortressNetMessages.Send(NetworkManager, clientId, writer, ReliableDelivery);
                NetTrafficStats.Sent(writer.Length);
                NetTrafficStats.SwarmEventsSent += entries.Length;
                if (!_snapshotClients.Contains(clientId))
                {
                    _snapshotClients.Add(clientId);
                }
            }

            _fullStateRequests.Clear();
            NetTrafficStats.SwarmFullStateTransfers = 0;
        }

        // ------------------------------------------------------------------ Client

        private void BeginSnapshotClient()
        {
            _decoder ??= new SnapshotDecoder();
            _decoder.Synced = false;
            _decoder.Serial = 0;
            for (var i = 0; i < _decoder.Alive.Length; i++)
            {
                _decoder.Alive[i] = 0;
            }

            _hasClock = false;
            _swarm.SetReplicaSource(SwarmReplicaSource.Snapshots);
            _swarm.ConfigureReplicaSnapshots(snapshotSharpness, snapshotSnapDistance, maxExtrapolationSeconds);
        }

        private void OnKeyframeMessage(ulong sender, FastBufferReader reader)
        {
            if (_swarm == null || _decoder == null || _layoutMismatch || IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint serial);
            reader.ReadValueSafe(out double hostTime);
            reader.ReadValueSafe(out float hostDelta);
            reader.ReadValueSafe(out uint layoutHash);
            reader.ReadValueSafe(out uint checksum);
            if (layoutHash != _types.LayoutHash)
            {
                // 敵の種類の並びが違う=違うビルド。違う種類で表示してしまうので同期をやめる。
                _layoutMismatch = true;
                Debug.LogError("[Net] SwarmNetworkHub: 敵の種類の並びがHostと一致しません。HostとClientが同じビルドか確認してください。群衆は同期しません。");
                _swarm.EndReplica();
                return;
            }

            reader.ReadValueSafe(out NativeArray<SwarmKeyframeEntry> entries, Allocator.Temp, default(FastBufferWriter.ForGeneric));
            NetTrafficStats.Received(entries.Length * KeyframeEntrySize);
            NetTrafficStats.SwarmEventsReceived += entries.Length;

            // 手元の敵を、写真に写っている顔ぶれに合わせる(いない敵は消し、知らない敵は出す)。
            var present = new NativeArray<byte>(SwarmSystem.MaxNetIds, Allocator.Temp);
            for (var i = 0; i < entries.Length; i++)
            {
                present[entries[i].Spawn.Id] = 1;
            }

            for (var id = 0; id < _alive.Length; id++)
            {
                if (_alive[id] && present[id] == 0)
                {
                    _alive[id] = false;
                    _swarm.RemoveReplica(id, EnemyRemovalReason.Removed);
                }
            }

            present.Dispose();
            for (var i = 0; i < entries.Length; i++)
            {
                if (!_alive[entries[i].Spawn.Id])
                {
                    Apply(entries[i].Spawn);
                }
            }

            new SwarmSnapshotKeyframeApplyJob
            {
                entries = entries,
                hostDeltaSeconds = hostDelta,
                expectedChecksum = checksum,
                alive = _decoder.Alive,
                h1 = _decoder.H1,
                h2 = _decoder.H2,
                snapPos = _decoder.SnapPos,
                snapVel = _decoder.SnapVel,
                result = _decoder.Result
            }.Run();
            entries.Dispose();

            if (_decoder.Result[0] == 0)
            {
                Debug.LogWarning("[Net] SwarmNetworkHub: 全員分の写真の確かめ用の値が合いません。もう一度頼みます。");
                _decoder.Synced = false;
                NetTrafficStats.SwarmSnapshotResyncs++;
                RequestFullStateRpc();
                return;
            }

            _decoder.Serial = serial;
            _decoder.Synced = true;
            _hasFullState = true;
            NetTrafficStats.SwarmFullStateReceived = true;
            PublishSnapshot(hostTime);
        }

        private void OnSnapshotMessage(ulong sender, FastBufferReader reader)
        {
            if (_swarm == null || _decoder == null || !_decoder.Synced || _layoutMismatch || IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint serial);
            if (serial != _decoder.Serial + 1)
            {
                // 確実な届け方なので普通は起きない(取りこぼし・順番の入れ替わり)。手元の状態が使えないので立て直す。
                RequestSnapshotResync();
                return;
            }

            reader.ReadValueSafe(out double hostTime);
            reader.ReadValueSafe(out float hostDelta);
            reader.ReadValueSafe(out int ratioQ);
            reader.ReadByteSafe(out byte kx);
            reader.ReadByteSafe(out byte ky);
            reader.ReadValueSafe(out int continuing);
            reader.ReadValueSafe(out uint checksum);
            reader.ReadValueSafe(out NativeArray<SwarmNetEvent> despawns, Allocator.Temp, default(FastBufferWriter.ForGeneric));
            reader.ReadValueSafe(out NativeArray<SwarmNetEvent> spawns, Allocator.Temp, default(FastBufferWriter.ForGeneric));
            reader.ReadValueSafe(out NativeArray<byte> bits, Allocator.Temp, default(FastBufferWriter.ForGeneric));
            NetTrafficStats.Received(reader.Length);
            NetTrafficStats.SwarmEventsReceived += despawns.Length + spawns.Length;
            NetTrafficStats.SwarmSnapshotsReceived++;

            new SwarmSnapshotDecodeJob
            {
                bits = bits,
                despawns = despawns,
                spawns = spawns,
                kx = kx,
                ky = ky,
                ratioQ = ratioQ,
                expectedContinuing = continuing,
                expectedChecksum = checksum,
                hostDeltaSeconds = hostDelta,
                alive = _decoder.Alive,
                h1 = _decoder.H1,
                h2 = _decoder.H2,
                snapPos = _decoder.SnapPos,
                snapVel = _decoder.SnapVel,
                result = _decoder.Result
            }.Run();

            if (_decoder.Result[0] == 0)
            {
                despawns.Dispose();
                spawns.Dispose();
                bits.Dispose();
                Debug.LogWarning($"[Net] SwarmNetworkHub: 写真{serial}の復元結果がHostと一致しません。全員分の写真で立て直します。");
                RequestSnapshotResync();
                return;
            }

            _decoder.Serial = serial;

            // 同じ写真の中で消えて同じ番号で出直した敵は、消さずにそのまま使う(消す処理と出す処理が同じフレームに重なると両方消えるため)。
            for (var i = 0; i < despawns.Length; i++)
            {
                var id = despawns[i].Id;
                if (!_alive[id] || ContainsId(spawns, id))
                {
                    continue;
                }

                _alive[id] = false;
                _swarm.RemoveReplica(id, (EnemyRemovalReason)despawns[i].TypeOrReason);
            }

            for (var i = 0; i < spawns.Length; i++)
            {
                if (!_alive[spawns[i].Id])
                {
                    Apply(spawns[i]);
                }
            }

            despawns.Dispose();
            spawns.Dispose();
            bits.Dispose();
            PublishSnapshot(hostTime);
        }

        private static bool ContainsId(NativeArray<SwarmNetEvent> events, ushort id)
        {
            for (var i = 0; i < events.Length; i++)
            {
                if (events[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        // 復元した写真をSwarmSystemへ渡し、時計の差を更新する。
        private void PublishSnapshot(double hostTime)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var sample = now - hostTime;
            if (!_hasClock || sample < _clockOffset)
            {
                _clockOffset = sample;
                _hasClock = true;
            }
            else
            {
                // 一番速く届いた時の差を基準にし(届く遅れのばらつきで表示が揺れないように)、少しずつ忘れて合わせ直す。
                _clockOffset = Math.Min(sample, _clockOffset + (now - _lastClockSample) * ClockCreepPerSecond);
            }

            _lastClockSample = now;
            _swarm.SetReplicaClock(_clockOffset, snapshotLeadSeconds);
            _swarm.SubmitReplicaSnapshot(_decoder.SnapPos, _decoder.SnapVel, hostTime);
        }

        private void RequestSnapshotResync()
        {
            if (!_decoder.Synced)
            {
                return;
            }

            _decoder.Synced = false;
            NetTrafficStats.SwarmSnapshotResyncs++;
            RequestFullStateRpc();
        }
    }
}
