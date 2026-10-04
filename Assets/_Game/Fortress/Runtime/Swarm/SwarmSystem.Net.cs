using System;
using System.Collections.Generic;
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
    ///   Hostから届いた追加(SpawnReplica)・削除(RemoveReplica)・位置(QueueCorrection)に従う。
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

        private struct PendingCorrection
        {
            public int Id;
            public float2 Position;
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
        private readonly List<PendingCorrection> _pendingCorrections = new List<PendingCorrection>();

        private SwarmIdPool _idPool;
        private NativeHashSet<int> _removeIds;
        private NativeHashMap<int, float2> _correctionTargets;
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

        private bool UsesRenderOffset => IsReplica && _smoothing == SwarmReplicaSmoothing.RenderOffset;

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
            _pendingCorrections.Clear();
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

        /// <summary>Client専用。Hostでのその敵の位置を伝える(数フレームかけて寄せる)。</summary>
        public void QueueCorrection(int id, Vector2 position)
        {
            if (IsReplica)
            {
                _pendingCorrections.Add(new PendingCorrection { Id = id, Position = position });
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
            _correctionTargets = new NativeHashMap<int, float2>(4096, Allocator.Persistent);
            _removedIds = new NativeArray<int>(capacity, Allocator.Persistent);
            _removedReasons = new NativeArray<byte>(capacity, Allocator.Persistent);
            _correctionErrors = new NativeArray<float>(capacity, Allocator.Persistent);
            _correctionStats = new NativeArray<float>(4, Allocator.Persistent);
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
            DisposeArray(ref _correctionStats);
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

            _correctionTargets.Clear();
            foreach (var correction in _pendingCorrections)
            {
                _correctionTargets[correction.Id] = correction.Position;
            }

            _pendingCorrections.Clear();
        }

        private void DiscardNetJobInputs()
        {
            _pendingRemovals.Clear();
            _pendingCorrections.Clear();
        }

        private JobHandle ScheduleReplicaCorrection(JobHandle dependsOn, int count, float dt)
        {
            if (!IsReplica)
            {
                return dependsOn;
            }

            var corrected = new SwarmCorrectionJob
            {
                netId = Storage.netId,
                vel = Storage.vel,
                targets = _correctionTargets,
                pos = Storage.pos,
                correction = Storage.correction,
                errorOut = _correctionErrors,
                blend = 1f - Mathf.Exp(-_correctionSharpness * dt),
                snapDistanceSq = _correctionSnapDistance * _correctionSnapDistance,
                leadSeconds = _correctionLeadSeconds,
                renderOnly = UsesRenderOffset ? 1 : 0
            }.Schedule(count, 256, dependsOn);

            _correctionStatsScheduled = true;

            // 集計は軽いので、以降の計算はこれの完了も待つ(全体の完了=_handleに含めるため)。
            return new SwarmCorrectionStatsJob
            {
                errors = _correctionErrors,
                stats = _correctionStats,
                count = count,
                snapDistance = _correctionSnapDistance
            }.Schedule(corrected);
        }
    }
}
