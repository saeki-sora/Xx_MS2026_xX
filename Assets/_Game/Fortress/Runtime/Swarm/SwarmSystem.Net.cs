using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆システムのネット対戦まわり(通信用の番号・Clientモード)。同期そのもの(送受信・写真の圧縮)は SwarmNetworkHub が行い、
    /// ここは「番号を振る・消えたことを知らせる・Hostの写真どおりに追加/削除/配置する」入口だけを持つ。
    ///
    /// Host(とオフライン): 追加した敵に番号(netId)を振り、消えた敵を AgentsRemoved で知らせる。
    /// Client(<see cref="IsReplica"/>、写真方式): 自分では湧かせず、ダメージ・コア到達でも消さず、押し合いも計算しない。
    ///   Hostの写真から届いた追加(SpawnReplica)・削除(RemoveReplica)・位置と速度(SubmitReplicaSnapshot)に従い、
    ///   最新の写真を届くまでの遅れの分だけ先読みした位置に置く(SwarmSnapshotFollowJob)。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        /// <summary>通信用の番号の上限(2バイトで送るため)。</summary>
        public const int MaxNetIds = 65536;

        public struct RemovalRecord
        {
            public int Id;
            public EnemyRemovalReason Reason;
        }

        /// <summary>Host(とオフライン)で敵が消えた(倒された・コア到達・全消去)。</summary>
        public event Action<IReadOnlyList<RemovalRecord>> AgentsRemoved;

        /// <summary>
        /// ジョブが止まっていて <see cref="Storage"/> を安全に読める瞬間(毎フレーム1回、次の計算を始める直前)。
        /// それ以外のタイミングで Storage を読むと、計算中の配列に触れてエラーになる。
        /// </summary>
        public event Action StorageReadable;

        /// <summary>ネット対戦のClientとして、Hostの結果に従っている。</summary>
        public bool IsReplica { get; private set; }

        /// <summary>1フレームに追加する上限の上書き(計測で改善前後を比べる用)。負なら群衆の設定(maxSpawnsPerFrame)どおり。0は無制限。</summary>
        public int SpawnsPerFrameOverride { get; set; } = -1;

        /// <summary>Client: 今の先読みの秒数(写真の時刻から、表示している時刻まで。計測用)。</summary>
        public float ReplicaExtrapolationSeconds { get; private set; }

        private int SpawnsPerFrameLimit => SpawnsPerFrameOverride >= 0 ? SpawnsPerFrameOverride : _settings.maxSpawnsPerFrame;

        private readonly List<RemovalRecord> _removalRecords = new List<RemovalRecord>();
        private readonly List<int> _pendingRemovals = new List<int>();

        private SwarmIdPool _idPool;
        private NativeHashSet<int> _removeIds;
        private NativeArray<int> _removedIds;
        private NativeArray<byte> _removedReasons;
        private NativeArray<float> _replicaErrors;
        private NativeArray<float> _replicaStats;
        private bool _replicaStatsScheduled;
        private bool _resetIdsOnClear;

        // 届いた写真(番号ごとの位置と速度)。受信側の配列を、ジョブが動いていない間にここへ写してから使う
        // (受信は計算中にも起きるので、ジョブが読む配列を直接書き換えない)。
        private NativeArray<float2> _snapPos;
        private NativeArray<float2> _snapVel;
        private NativeArray<float2> _submittedSnapPos;
        private NativeArray<float2> _submittedSnapVel;
        private bool _snapshotSubmitted;
        private bool _hasSnapshot;
        private bool _newSnapshot;
        private double _submittedSnapshotHostTime;
        private double _snapshotHostTime;
        private double _replicaClockOffset;
        private float _snapshotLeadBias;
        private float _snapshotSharpness = 15f;
        private float _snapshotSnapDistance = 2f;
        private float _maxExtrapolationSeconds = 0.2f;
        private double _lastFollowTime = -1d;

        /// <summary>
        /// Client: sharpness=先読みの外れを見た目で直す速さ、snapDistance=これ以上外れたら滑らせずに合わせる距離、
        /// maxExtrapolation=写真が途切れたときに先へ進める上限(秒)。
        /// </summary>
        public void ConfigureReplicaSnapshots(float sharpness, float snapDistance, float maxExtrapolationSeconds)
        {
            _snapshotSharpness = Mathf.Max(0.1f, sharpness);
            _snapshotSnapDistance = Mathf.Max(0.1f, snapDistance);
            _maxExtrapolationSeconds = Mathf.Max(0f, maxExtrapolationSeconds);
        }

        /// <summary>
        /// Client: 復元した最新の写真を渡す(番号ごとの位置と速度、MaxNetIds個)。配列は次に呼ぶまで書き換えないこと
        /// (ジョブが動いていない間に、こちらの配列へ写す)。hostTime はHostの時計での写真の時刻。
        /// </summary>
        public void SubmitReplicaSnapshot(NativeArray<float2> positions, NativeArray<float2> velocities, double hostTime)
        {
            _submittedSnapPos = positions;
            _submittedSnapVel = velocities;
            _submittedSnapshotHostTime = hostTime;
            _snapshotSubmitted = true;
        }

        /// <summary>
        /// Client: Hostの時計との差(自分の時刻 − Hostの時刻、届くまでの一番短い遅れを含む)と、さらに先へ進める秒数。
        /// 表示する時刻(Hostの時計) = 自分の時刻 − offset + leadBias。
        /// </summary>
        public void SetReplicaClock(double localMinusHostSeconds, float leadBiasSeconds)
        {
            _replicaClockOffset = localMinusHostSeconds;
            _snapshotLeadBias = leadBiasSeconds;
        }

        /// <summary>Clientになる。今いる敵は全て消え、以後はHostの写真から届く敵だけになる。</summary>
        public void BeginReplica()
        {
            Initialize();
            IsReplica = true;
            _resetIdsOnClear = true;
            _pendingRemovals.Clear();
            ResetSnapshotState();
            ClearAll();
        }

        /// <summary>Clientをやめる(切断時など)。Hostの敵は今後更新されないので全て消す。</summary>
        public void EndReplica()
        {
            if (!IsReplica)
            {
                return;
            }

            IsReplica = false;
            _resetIdsOnClear = true;
            _pendingRemovals.Clear();
            ResetSnapshotState();
            ClearAll();
        }

        private void ResetSnapshotState()
        {
            _snapshotSubmitted = false;
            _hasSnapshot = false;
            _newSnapshot = false;
            _lastFollowTime = -1d;
            ReplicaExtrapolationSeconds = 0f;
        }

        /// <summary>Client専用。Hostから届いた敵を、届いた番号のまま追加する。</summary>
        public bool SpawnReplica(int id, EnemyTypeDefinition definition, Vector2 position, float speedScale, float animStart, Vector2 facing)
        {
            if (!IsReplica || definition == null)
            {
                return false;
            }

            var type = RegisterType(definition);
            if (type < 0 || Storage.Count + _pending.Count >= Storage.Capacity)
            {
                _stats.rejectedSpawns++;
                return false;
            }

            _pending.Add(new PendingSpawn
            {
                position = position,
                type = type,
                hp = definition.maxHealth,
                speed = speedScale,
                animStart = animStart,
                face = facing,
                id = id
            });

            _stats.totalSpawned++;
            return true;
        }

        /// <summary>Client専用。Hostで消えた敵を消す(次の計算で消える)。</summary>
        public void RemoveReplica(int id, EnemyRemovalReason reason)
        {
            if (!IsReplica)
            {
                return;
            }

            _pendingRemovals.Add(id);
            if (reason == EnemyRemovalReason.Died)
            {
                _stats.totalKilled++;
            }
            else if (reason == EnemyRemovalReason.ReachedCore)
            {
                _stats.totalArrived++;
            }
        }

        /// <summary>このシステム内の種類番号(Storage.typeIdx)から、敵の種類を引く。</summary>
        public EnemyTypeDefinition GetRegisteredType(int typeIndex)
        {
            return typeIndex >= 0 && typeIndex < _types.Count ? _types[typeIndex] : null;
        }

        private void InitializeNet(int capacity)
        {
            if (capacity > MaxNetIds)
            {
                Debug.LogWarning($"[Swarm] 敵の上限({capacity})が通信用の番号の上限({MaxNetIds})を超えています。ネット対戦では{MaxNetIds}体までしか同期できません。", this);
            }

            _idPool = new SwarmIdPool(Mathf.Min(capacity, MaxNetIds));
            _removeIds = new NativeHashSet<int>(1024, Allocator.Persistent);
            _removedIds = new NativeArray<int>(capacity, Allocator.Persistent);
            _removedReasons = new NativeArray<byte>(capacity, Allocator.Persistent);
            _replicaErrors = new NativeArray<float>(capacity, Allocator.Persistent);
            _replicaStats = new NativeArray<float>(SwarmReplicaStatsJob.StatCount, Allocator.Persistent);
            _snapPos = new NativeArray<float2>(MaxNetIds, Allocator.Persistent);
            _snapVel = new NativeArray<float2>(MaxNetIds, Allocator.Persistent);
        }

        private void DisposeNet()
        {
            if (_removeIds.IsCreated)
            {
                _removeIds.Dispose();
            }

            DisposeArray(ref _removedIds);
            DisposeArray(ref _removedReasons);
            DisposeArray(ref _replicaErrors);
            DisposeArray(ref _replicaStats);
            DisposeArray(ref _snapPos);
            DisposeArray(ref _snapVel);
        }

        private int AllocateNetId()
        {
            return _idPool.TryAllocate(out var id) ? id : -1;
        }

        private void ReleaseNetId(int id)
        {
            _idPool.Release(id);
        }

        // 全消去の直前(配列がまだ読める)に呼ぶ。Hostでは消える全員を「削除」として知らせ、番号を返す。
        private void HandleStorageCleared()
        {
            if (_resetIdsOnClear)
            {
                // Clientになった/やめた直後。番号の持ち主が変わるので、数え直す。
                _resetIdsOnClear = false;
                _idPool.Reset();
                return;
            }

            if (IsReplica)
            {
                return;
            }

            for (var i = 0; i < Storage.Count; i++)
            {
                var id = Storage.netId[i];
                _idPool.Release(id);
                _removalRecords.Add(new RemovalRecord { Id = id, Reason = EnemyRemovalReason.Removed });
            }

            RaiseRemovalRecords();
        }

        // ジョブ完了後に呼ぶ。Hostでは消えた敵の番号を返し、削除として知らせる。Clientでは先読みの外れを集計する。
        private void CollectRemovals()
        {
            if (IsReplica)
            {
                CollectReplicaStats();
                return;
            }

            var removed = _counters[3];
            for (var i = 0; i < removed; i++)
            {
                var id = _removedIds[i];
                _idPool.Release(id);
                _removalRecords.Add(new RemovalRecord { Id = id, Reason = (EnemyRemovalReason)_removedReasons[i] });
            }

            RaiseRemovalRecords();
        }

        private void CollectReplicaStats()
        {
            if (!_replicaStatsScheduled)
            {
                return;
            }

            _replicaStatsScheduled = false;
            _stats.replicaCorrections = (int)_replicaStats[0];
            _stats.replicaErrorSum = _replicaStats[1];
            _stats.replicaErrorMax = _replicaStats[2];
            _stats.replicaSnaps = (int)_replicaStats[3];
        }

        private void RaiseRemovalRecords()
        {
            if (_removalRecords.Count == 0)
            {
                return;
            }

            AgentsRemoved?.Invoke(_removalRecords);
            _removalRecords.Clear();
        }

        // ジョブに渡す「消す番号」と「最新の写真」を用意する(ジョブが動いていない間に呼ぶ)。
        private void PrepareNetJobInputs()
        {
            _removeIds.Clear();
            foreach (var id in _pendingRemovals)
            {
                _removeIds.Add(id);
            }

            _pendingRemovals.Clear();

            // 新しい写真が届いていれば、ジョブ用の配列へ写す(受信側はジョブと関係なく次の写真を書けるように)。
            _newSnapshot = false;
            if (_snapshotSubmitted && _submittedSnapPos.IsCreated && _submittedSnapVel.IsCreated)
            {
                _snapshotSubmitted = false;
                NativeArray<float2>.Copy(_submittedSnapPos, _snapPos);
                NativeArray<float2>.Copy(_submittedSnapVel, _snapVel);
                _snapshotHostTime = _submittedSnapshotHostTime;
                _hasSnapshot = true;
                _newSnapshot = true;
            }
        }

        private void DiscardNetJobInputs()
        {
            _pendingRemovals.Clear();
        }

        // Client: 最新の写真を、届くまでの遅れの分だけ先へ進めた位置に置く(押し合いは計算しない)。
        private JobHandle ScheduleReplicaFollow(JobHandle dependsOn, int count)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var advance = _lastFollowTime < 0d ? 0f : (float)(now - _lastFollowTime);
            _lastFollowTime = now;
            if (!_hasSnapshot)
            {
                _replicaStatsScheduled = false;
                return dependsOn;
            }

            var displayHostTime = now - _replicaClockOffset + _snapshotLeadBias;
            var lead = Mathf.Clamp((float)(displayHostTime - _snapshotHostTime), 0f, _maxExtrapolationSeconds);
            ReplicaExtrapolationSeconds = lead;

            var followed = new SwarmSnapshotFollowJob
            {
                netId = Storage.netId,
                snapPos = _snapPos,
                snapVel = _snapVel,
                typeIdx = Storage.typeIdx,
                types = _typeParams,
                pos = Storage.pos,
                vel = Storage.vel,
                correction = Storage.correction,
                errorOut = _replicaErrors,
                lead = lead,
                advance = advance,
                blend = 1f - Mathf.Exp(-_snapshotSharpness * advance),
                snapDistanceSq = _snapshotSnapDistance * _snapshotSnapDistance,
                newSnapshot = _newSnapshot ? 1 : 0
            }.Schedule(count, 256, dependsOn);

            _replicaStatsScheduled = true;

            // 集計は軽いので、以降の計算はこれの完了も待つ(全体の完了=_handleに含めるため)。
            return new SwarmReplicaStatsJob
            {
                errors = _replicaErrors,
                stats = _replicaStats,
                count = count,
                snapDistance = _snapshotSnapDistance
            }.Schedule(followed);
        }
    }
}
