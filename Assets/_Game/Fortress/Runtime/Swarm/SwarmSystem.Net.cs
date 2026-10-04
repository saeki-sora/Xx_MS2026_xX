using System;
using System.Collections.Generic;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆システムのネット対戦まわり(通信用の番号・Clientモード)。同期そのもの(送受信)は SwarmNetworkHub が行い、
    /// ここは「番号を振る・出入りを知らせる・Hostの指示どおりに追加/削除/補正する」入口だけを持つ。
    ///
    /// Host(とオフライン): 追加した敵に番号を振り、追加/削除を AgentsSpawned / AgentsRemoved で知らせる。
    /// Client(<see cref="IsReplica"/>): 自分では湧かせず、ダメージ・コア到達でも消さない。動き(経路・押し合い)だけ自分で計算し、
    ///   Hostから届いた追加(SpawnReplica)・削除(RemoveReplica)・位置(QueueCorrections)に従う。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        /// <summary>通信用の番号の上限(2バイトで送るため)。</summary>
        public const int MaxNetIds = 65536;

        /// <summary>Hostで追加された敵1体の情報(Clientで同じ敵を作るのに必要な物)。</summary>
        public struct SpawnRecord
        {
            public int Id;
            public EnemyTypeDefinition Type;
            public Vector2 Position;
            public float SpeedScale;
            public float AnimStart;
            public Vector2 Facing;
        }

        public struct RemovalRecord
        {
            public int Id;
            public EnemyRemovalReason Reason;
        }

        /// <summary>Host(とオフライン)で敵が追加された(毎フレーム、まとめて1回)。</summary>
        public event Action<IReadOnlyList<SpawnRecord>> AgentsSpawned;

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

        private int SpawnsPerFrameLimit => SpawnsPerFrameOverride >= 0 ? SpawnsPerFrameOverride : _settings.maxSpawnsPerFrame;

        private readonly List<SpawnRecord> _spawnRecords = new List<SpawnRecord>();
        private readonly List<RemovalRecord> _removalRecords = new List<RemovalRecord>();
        private readonly List<int> _pendingRemovals = new List<int>();

        // Hostから届いた補正。受信のたびにまとめてコピーし(1件ずつの処理やGCを出さないため)、
        // 計算の直前にジョブ用へ移して、目標位置の表(_correctionTargets)をジョブで作る。
        private NativeList<SwarmNetCorrection> _pendingCorrections;
        private NativeList<SwarmNetCorrection> _jobCorrections;
        private NativeList<SwarmNetCorrectionV> _pendingCorrectionsV;
        private NativeList<SwarmNetCorrectionV> _jobCorrectionsV;
        private NativeList<SwarmNetCorrection> _pendingAudit;
        private NativeList<SwarmNetCorrection> _jobAudit;
        private NativeArray<byte> _correctionAudit;

        private SwarmIdPool _idPool;
        private NativeHashSet<int> _removeIds;
        private NativeHashMap<int, float4> _correctionTargets;
        private NativeArray<int> _removedIds;
        private NativeArray<byte> _removedReasons;
        private NativeArray<float> _correctionErrors;
        private NativeArray<float> _correctionStats;
        private bool _correctionStatsScheduled;
        private bool _resetIdsOnClear;
        private int _replicaSeparationIterations = -1;

        private float _correctionSharpness = 8f;
        private float _correctionSnapDistance = 4f;
        private float _correctionLeadSeconds = 0.05f;
        private SwarmReplicaSmoothing _smoothing = SwarmReplicaSmoothing.RenderOffset;
        private float _measuredLeadSeconds;
        private SwarmReplicaSource _replicaSource = SwarmReplicaSource.Snapshots;

        // 写真方式: 届いた写真(番号ごとの位置と速度)。受信側の配列を、ジョブが動いていない間にここへ写してから使う。
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

        /// <summary>Client(写真方式): 今の先読みの秒数(計測用)。</summary>
        public float ReplicaExtrapolationSeconds { get; private set; }

        private float4 _replicaView;
        private bool _hasReplicaView;
        private int _lastBeamCount;

        /// <summary>
        /// Host: 直前の計算で使ったレーザー(補正の優先度づけ用)。<see cref="StorageReadable"/> の中でだけ読むこと。
        /// </summary>
        public NativeArray<SwarmBeam> RecentBeams => _beamsJob.GetSubArray(0, _lastBeamCount);

        /// <summary>Host: コアの位置(補正の優先度づけ用)。<see cref="StorageReadable"/> の中でだけ読むこと。</summary>
        public NativeArray<float2> CorePositions => _goals.GetSubArray(0, _goalCount);

        /// <summary>
        /// Client側の補正の効き方。sharpness=見た目が追いつく速さ、snapDistance=これ以上ズレたら即座に合わせる距離、
        /// leadSeconds=届くまでの遅れの見込み、smoothing=計算上の位置を即合わせて見た目だけ滑らかにするか(既定)、従来どおり計算上の位置ごと寄せるか。
        /// </summary>
        public void ConfigureReplicaCorrection(float sharpness, float snapDistance, float leadSeconds, SwarmReplicaSmoothing smoothing)
        {
            _correctionSharpness = Mathf.Max(0.1f, sharpness);
            _correctionSnapDistance = Mathf.Max(0.1f, snapDistance);
            _correctionLeadSeconds = Mathf.Max(0f, leadSeconds);
            _smoothing = smoothing;
        }

        private bool UsesRenderOffset => IsReplica && (UsesSnapshots || _smoothing == SwarmReplicaSmoothing.RenderOffset);

        private bool UsesSnapshots => IsReplica && _replicaSource == SwarmReplicaSource.Snapshots;

        /// <summary>Clientで、敵の動きをHostの写真で決めている(写真方式)。</summary>
        public bool ReplicaUsesSnapshots => UsesSnapshots;

        /// <summary>Client: 敵の動きを写真で決めるか、自分で計算して補正するか。BeginReplica の前に決める。</summary>
        public void SetReplicaSource(SwarmReplicaSource source)
        {
            _replicaSource = source;
        }

        /// <summary>
        /// Client(写真方式): sharpness=先読みの外れを見た目で直す速さ、snapDistance=これ以上外れたら滑らせずに合わせる距離、
        /// maxExtrapolation=写真が途切れたときに先へ進める上限(秒)。
        /// </summary>
        public void ConfigureReplicaSnapshots(float sharpness, float snapDistance, float maxExtrapolationSeconds)
        {
            _snapshotSharpness = Mathf.Max(0.1f, sharpness);
            _snapshotSnapDistance = Mathf.Max(0.1f, snapDistance);
            _maxExtrapolationSeconds = Mathf.Max(0f, maxExtrapolationSeconds);
        }

        /// <summary>
        /// Client(写真方式): 復元した最新の写真を渡す(番号ごとの位置と速度、MaxNetIds個)。配列は次に呼ぶまで書き換えないこと
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
        /// Client(写真方式): Hostの時計との差(自分の時刻 − Hostの時刻、届くまでの一番短い遅れを含む)と、さらに先へ進める秒数。
        /// 表示する時刻(Hostの時計) = 自分の時刻 − offset + leadBias。
        /// </summary>
        public void SetReplicaClock(double localMinusHostSeconds, float leadBiasSeconds)
        {
            _replicaClockOffset = localMinusHostSeconds;
            _snapshotLeadBias = leadBiasSeconds;
        }

        /// <summary>
        /// Client: 実測した届くまでの遅れ(秒。往復時間の半分)。ConfigureReplicaCorrection の leadSeconds に足して先読みする(段階5)。
        /// </summary>
        public void SetReplicaMeasuredLead(float seconds)
        {
            _measuredLeadSeconds = Mathf.Clamp(seconds, 0f, 0.5f);
        }

        /// <summary>Client: 自分の画面に映る範囲(計測用。画面に映る敵のズレを別に数える)。hasView=false で数えない。</summary>
        public void SetReplicaViewRect(Rect rect, bool hasView)
        {
            _replicaView = new float4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
            _hasReplicaView = hasView;
        }

        /// <summary>
        /// Clientの押し合い計算の反復回数(負なら群衆の設定どおり)。Clientの位置はHostが補正するので、
        /// 反復を減らして計算を軽くしても見た目への影響は小さい。
        /// </summary>
        public void SetReplicaSeparationIterations(int iterations)
        {
            _replicaSeparationIterations = iterations;
        }

        private int SeparationIterations => IsReplica && _replicaSeparationIterations >= 0
            ? _replicaSeparationIterations
            : _settings.separationIterations;

        /// <summary>Clientになる。今いる敵は全て消え、以後はHostから届く敵だけになる。</summary>
        public void BeginReplica()
        {
            Initialize();
            IsReplica = true;
            _resetIdsOnClear = true;
            _pendingRemovals.Clear();
            _pendingCorrections.Clear();
            _pendingCorrectionsV.Clear();
            _pendingAudit.Clear();
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
            _pendingCorrections.Clear();
            _pendingCorrectionsV.Clear();
            _pendingAudit.Clear();
            ResetSnapshotState();
            ClearAll();
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

        /// <summary>
        /// Client専用。Hostでの敵の位置(届いたまま、量子化された値)をまとめて伝える。中身はコピーするので、呼んだ後に配列を捨ててよい。
        /// 知らない番号(既に消えた敵など)が混ざっていても構わない(該当する敵がいなければ何もしない)。
        /// </summary>
        /// <param name="audit">抜き取り検査の補正(優先度と関係なく選ばれた物)。普通の補正と同じく合わせ、ズレを別に数える。</param>
        public void QueueCorrections(NativeArray<SwarmNetCorrection> corrections, bool audit = false)
        {
            if (IsReplica && corrections.IsCreated && corrections.Length > 0)
            {
                if (audit)
                {
                    _pendingAudit.AddRange(corrections);
                }
                else
                {
                    _pendingCorrections.AddRange(corrections);
                }
            }
        }

        /// <summary>Client専用。速度つきの補正(段階5)。使い方は位置だけの物と同じ。</summary>
        public void QueueCorrections(NativeArray<SwarmNetCorrectionV> corrections)
        {
            if (IsReplica && corrections.IsCreated && corrections.Length > 0)
            {
                _pendingCorrectionsV.AddRange(corrections);
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
            _correctionTargets = new NativeHashMap<int, float4>(4096, Allocator.Persistent);
            _removedIds = new NativeArray<int>(capacity, Allocator.Persistent);
            _removedReasons = new NativeArray<byte>(capacity, Allocator.Persistent);
            _correctionErrors = new NativeArray<float>(capacity, Allocator.Persistent);
            _correctionAudit = new NativeArray<byte>(capacity, Allocator.Persistent);
            _correctionStats = new NativeArray<float>(10, Allocator.Persistent);
            _pendingCorrections = new NativeList<SwarmNetCorrection>(4096, Allocator.Persistent);
            _jobCorrections = new NativeList<SwarmNetCorrection>(4096, Allocator.Persistent);
            _pendingCorrectionsV = new NativeList<SwarmNetCorrectionV>(4096, Allocator.Persistent);
            _jobCorrectionsV = new NativeList<SwarmNetCorrectionV>(4096, Allocator.Persistent);
            _pendingAudit = new NativeList<SwarmNetCorrection>(1024, Allocator.Persistent);
            _jobAudit = new NativeList<SwarmNetCorrection>(1024, Allocator.Persistent);
            _snapPos = new NativeArray<float2>(MaxNetIds, Allocator.Persistent);
            _snapVel = new NativeArray<float2>(MaxNetIds, Allocator.Persistent);
        }

        private void DisposeNet()
        {
            if (_removeIds.IsCreated)
            {
                _removeIds.Dispose();
            }

            if (_correctionTargets.IsCreated)
            {
                _correctionTargets.Dispose();
            }

            DisposeArray(ref _removedIds);
            DisposeArray(ref _removedReasons);
            DisposeArray(ref _correctionErrors);
            DisposeArray(ref _correctionAudit);
            DisposeArray(ref _snapPos);
            DisposeArray(ref _snapVel);
            DisposeArray(ref _correctionStats);

            if (_pendingCorrections.IsCreated)
            {
                _pendingCorrections.Dispose();
            }

            if (_jobCorrections.IsCreated)
            {
                _jobCorrections.Dispose();
            }

            if (_pendingCorrectionsV.IsCreated)
            {
                _pendingCorrectionsV.Dispose();
            }

            if (_jobCorrectionsV.IsCreated)
            {
                _jobCorrectionsV.Dispose();
            }

            if (_pendingAudit.IsCreated)
            {
                _pendingAudit.Dispose();
            }

            if (_jobAudit.IsCreated)
            {
                _jobAudit.Dispose();
            }
        }

        private int AllocateNetId()
        {
            return _idPool.TryAllocate(out var id) ? id : -1;
        }

        private void ReleaseNetId(int id)
        {
            _idPool.Release(id);
        }

        private void RecordSpawn(PendingSpawn spawn, int id)
        {
            if (IsReplica)
            {
                return;
            }

            _spawnRecords.Add(new SpawnRecord
            {
                Id = id,
                Type = _types[spawn.type],
                Position = spawn.position,
                SpeedScale = spawn.speed,
                AnimStart = spawn.animStart,
                Facing = spawn.face
            });
        }

        private void RaiseSpawnRecords()
        {
            if (_spawnRecords.Count == 0)
            {
                return;
            }

            AgentsSpawned?.Invoke(_spawnRecords);
            _spawnRecords.Clear();
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

        // ジョブ完了後に呼ぶ。Hostでは消えた敵の番号を返し、削除として知らせる。
        private void CollectRemovals()
        {
            if (IsReplica)
            {
                CollectCorrectionStats();
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

        private void CollectCorrectionStats()
        {
            if (!_correctionStatsScheduled)
            {
                return;
            }

            _correctionStatsScheduled = false;
            _stats.replicaCorrections = (int)_correctionStats[0];
            _stats.replicaErrorSum = _correctionStats[1];
            _stats.replicaErrorMax = _correctionStats[2];
            _stats.replicaSnaps = (int)_correctionStats[3];
            _stats.replicaViewCorrections = (int)_correctionStats[4];
            _stats.replicaViewErrorSum = _correctionStats[5];
            _stats.replicaAuditCorrections = (int)_correctionStats[6];
            _stats.replicaAuditErrorSum = _correctionStats[7];
            _stats.replicaAuditViewCorrections = (int)_correctionStats[8];
            _stats.replicaAuditViewErrorSum = _correctionStats[9];
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

        // ジョブに渡す「消す番号」「補正先」を、届いた分から作る(ジョブが動いていない間に呼ぶ)。
        private void PrepareNetJobInputs()
        {
            _removeIds.Clear();
            foreach (var id in _pendingRemovals)
            {
                _removeIds.Add(id);
            }

            _pendingRemovals.Clear();

            // 目標位置の表はジョブで作る(SwarmCorrectionTargetsJob)。ジョブが読む間に受信が来ても壊れないよう、ジョブ用へ移しておく。
            _jobCorrections.CopyFrom(_pendingCorrections);
            _pendingCorrections.Clear();
            _jobCorrectionsV.CopyFrom(_pendingCorrectionsV);
            _pendingCorrectionsV.Clear();
            _jobAudit.CopyFrom(_pendingAudit);
            _pendingAudit.Clear();

            // 写真方式: 新しい写真が届いていれば、ジョブ用の配列へ写す(受信側はジョブと関係なく次の写真を書けるように)。
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
            var targetCount = _jobCorrections.Length + _jobCorrectionsV.Length + _jobAudit.Length;
            if (_correctionTargets.Capacity < targetCount)
            {
                _correctionTargets.Capacity = targetCount;
            }
        }

        private void DiscardNetJobInputs()
        {
            _pendingRemovals.Clear();
            if (_pendingCorrections.IsCreated)
            {
                _pendingCorrections.Clear();
            }

            if (_pendingCorrectionsV.IsCreated)
            {
                _pendingCorrectionsV.Clear();
            }

            if (_pendingAudit.IsCreated)
            {
                _pendingAudit.Clear();
            }
        }

        // 写真方式: 最新の写真を、届くまでの遅れの分だけ先へ進めた位置に置く(押し合いは計算しない)。
        private JobHandle ScheduleReplicaFollow(JobHandle dependsOn, int count)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var advance = _lastFollowTime < 0d ? 0f : (float)(now - _lastFollowTime);
            _lastFollowTime = now;
            if (!_hasSnapshot)
            {
                _correctionStatsScheduled = false;
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
                errorOut = _correctionErrors,
                auditOut = _correctionAudit,
                lead = lead,
                advance = advance,
                blend = 1f - Mathf.Exp(-_snapshotSharpness * advance),
                snapDistanceSq = _snapshotSnapDistance * _snapshotSnapDistance,
                newSnapshot = _newSnapshot ? 1 : 0
            }.Schedule(count, 256, dependsOn);

            _correctionStatsScheduled = true;
            return new SwarmCorrectionStatsJob
            {
                errors = _correctionErrors,
                audit = _correctionAudit,
                pos = Storage.pos,
                stats = _correctionStats,
                count = count,
                snapDistance = _snapshotSnapDistance,
                view = _replicaView,
                hasView = _hasReplicaView ? 1 : 0
            }.Schedule(followed);
        }

        private JobHandle ScheduleReplicaCorrection(JobHandle dependsOn, int count, float dt)
        {
            if (!IsReplica)
            {
                return dependsOn;
            }

            if (UsesSnapshots)
            {
                return ScheduleReplicaFollow(dependsOn, count);
            }

            var targets = new SwarmCorrectionTargetsJob
            {
                corrections = _jobCorrections.AsDeferredJobArray(),
                correctionsWithVelocity = _jobCorrectionsV.AsDeferredJobArray(),
                auditCorrections = _jobAudit.AsDeferredJobArray(),
                targets = _correctionTargets,
                inverseScale = 1f / SwarmNetQuantize.PositionScale
            }.Schedule(dependsOn);

            var corrected = new SwarmCorrectionJob
            {
                netId = Storage.netId,
                vel = Storage.vel,
                targets = _correctionTargets,
                pos = Storage.pos,
                correction = Storage.correction,
                errorOut = _correctionErrors,
                auditOut = _correctionAudit,
                blend = 1f - Mathf.Exp(-_correctionSharpness * dt),
                snapDistanceSq = _correctionSnapDistance * _correctionSnapDistance,
                leadSeconds = _correctionLeadSeconds + _measuredLeadSeconds,
                renderOnly = UsesRenderOffset ? 1 : 0
            }.Schedule(count, 256, targets);

            _correctionStatsScheduled = true;

            // 集計は軽いので、以降の計算はこれの完了も待つ(全体の完了=_handleに含めるため)。
            return new SwarmCorrectionStatsJob
            {
                errors = _correctionErrors,
                audit = _correctionAudit,
                pos = Storage.pos,
                stats = _correctionStats,
                count = count,
                snapDistance = _correctionSnapDistance,
                view = _replicaView,
                hasView = _hasReplicaView ? 1 : 0
            }.Schedule(corrected);
        }
    }
}
