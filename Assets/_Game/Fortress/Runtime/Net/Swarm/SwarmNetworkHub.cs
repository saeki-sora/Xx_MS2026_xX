using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>位置の補正の選び方。</summary>
    public enum SwarmCorrectionMode
    {
        /// <summary>Clientごとに、大事な敵・ズレていそうな敵から優先して、決めた通信量の範囲で送る(段階5。既定)。</summary>
        Priority = 0,

        /// <summary>番号で10組に分け、1組ずつ順番に全員へ送る(段階4まで。比較用)。</summary>
        RoundRobin = 1
    }

    /// <summary>
    /// 群衆(SwarmSystem、最大数万体)をネット対戦で同期する。他の同期部品と同じ「[Fortress] NetSync」に付ける。
    ///
    /// 方式(2026-10-04 ユーザー決定「各PCで計算＋Hostで補正」):
    /// ・Host: 今まで通り全てを計算する(湧かせる・倒す・コア到達・コアへのダメージ)。
    ///   出現/消滅は起きた順に確実に送る(1秒あたりの上限つき)。位置は番号でグループ分けし、毎回1グループずつ
    ///   「補正」として送る(既定: 普段は全員を0.25秒に1回、Clientのズレが大きい間は最短0.1秒に1回)。
    /// ・Client: SwarmSystemをClientモードにし、動き(経路・押し合い)は自分で計算しつつ、届いた位置へ合わせる
    ///   (計算上の位置はすぐ合わせ、見た目だけ滑らかに追いつく)。自分では湧かせず、倒さず、コアにダメージを与えない。
    /// ・途中参加: 接続時点の全員を分割して受け取り、受け取り終わるまでに届いた変化は貯めておいて、後から順に反映する。
    ///
    /// 送受信(2026-10-04 段階3): 量の多いデータ(出現/消滅・全体の状態・補正)はRPCではなく FortressNetMessages(名前なしメッセージ)で送り、
    /// 補正の収集・目標位置の表づくりはBurstのジョブで行う。送受信でGCを出さない(RPCの配列引数は毎回配列を作っていた)。
    ///
    /// 優先度つきの補正(2026-10-05 段階5、correctionMode=Priority):
    /// ・Clientごとに、敵1体ずつ「補正の必要度」を毎フレーム貯める(Priority Accumulator)。貯まる速さ = 大事さ × ズレの見込み。
    ///   大事さ = そのClientの画面(＋余白)に映っている・レーザーの近く・コアの近く。ズレの見込み = 前回送った位置と速度からの予想と今の位置の差。
    /// ・毎フレーム、通信量の枠(SwarmCorrectionBudgetController: ズレが大きいと増やし、往復時間が伸びたら減らす)に収まる数だけ、
    ///   貯まった順に送り、送った敵は0に戻す。長く送っていない敵(maxCorrectionAge)は必ず先に送る。
    /// ・Clientは、ズレの報告と一緒に自分の画面の範囲を送る。届くまでの遅れ(往復時間の半分)も先読みに足す。
    /// ・sendVelocity=ONなら速度も送り、ClientはHostの速度で先読みしてその速度を引き継ぐ(1体6→10バイト)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SwarmNetworkHub : NetworkBehaviour
    {
        [Header("同期方式(2026-10-05)")]
        [Tooltip("Snapshot=全員の位置を同じ瞬間の写真として送り、Clientは先読みして置く(既定。震え・隙間が出ない)。" +
                 "Corrections=Clientも動きを計算してHostの位置で補正する(段階5まで。比較用)。")]
        public SwarmReplicationMode replicationMode = SwarmReplicationMode.Snapshot;

        [Header("写真方式")]
        [Tooltip("1秒に撮って送る写真の枚数。多いほど先読みの外れが小さいが、通信が増える(3万体・30枚で1人あたり約6〜9Mbps)。")]
        [Min(5f)]
        public float snapshotRate = 30f;

        [Tooltip("Clientで、新しい写真が届いたときの先読みの外れを見た目で直す速さ。大きいほど早く合うが、滑るように見えやすい。")]
        [Min(0.1f)]
        public float snapshotSharpness = 15f;

        [Tooltip("先読みがこれ以上外れていたら、滑らせずにその場で合わせる距離(ワールド単位)。")]
        [Min(0.1f)]
        public float snapshotSnapDistance = 2f;

        [Tooltip("届くまでの一番短い遅れ(時計の差に含まれて測れない分)の見込み(秒)。この分さらに先読みする。")]
        [Min(0f)]
        public float snapshotLeadSeconds = 0.01f;

        [Tooltip("写真が途切れたときに先へ進める上限(秒)。")]
        [Min(0f)]
        public float maxExtrapolationSeconds = 0.2f;

        [Header("出現・消滅")]
        [Tooltip("出現・消滅をまとめて送る回数(回/秒)。")]
        [Min(1f)]
        public float eventSendRate = 20f;

        [Tooltip("1秒あたりに送る出現・消滅の上限(件)。一斉に大量に湧いたときは、この速さで少しずつ届く。1件14バイト。")]
        [Min(100)]
        public int maxEventsPerSecond = 40000;

        [Header("位置の補正")]
        [Tooltip("普段、全員の位置を1回ずつ補正するのにかける時間(秒)。短いほど正確だが通信が増える。" +
                 "既定0.25秒は2026-10-04の計測で決めた有線LAN向けの値(3万体で1人あたり普段約5Mbps)。Wi-Fiなら0.5秒程度に。")]
        [Min(0.05f)]
        public float correctionCycleSeconds = 0.25f;

        [Tooltip("ONなら、Clientが測ったHostとのズレに応じて補正の間隔を自動で縮める(ズレが大きい間だけ通信を増やす)。")]
        public bool adaptiveCorrection = true;

        [Tooltip("自動調整で縮める間隔の下限(秒)。一斉投入の直後など、ズレが大きい間だけここまで縮める(3万体・0.1秒で1人あたり最大約12Mbps、有線LAN向け)。")]
        [Min(0.05f)]
        public float minCorrectionCycleSeconds = 0.1f;

        [Tooltip("Clientのズレ(平均、ワールド単位)がこれ以上なら、間隔を下限まで縮める。")]
        [Min(0.01f)]
        public float highErrorThreshold = 1f;

        [Tooltip("Clientのズレ(平均)がこれ以下なら、間隔を普段の値へゆっくり戻す。")]
        [Min(0f)]
        public float lowErrorThreshold = 0.3f;

        [Tooltip("Clientでの寄せ方。RenderOffset=計算上の位置はすぐHostに合わせ、見た目だけ滑らかに追いつく(既定)。BlendSimulation=計算上の位置ごと少しずつ寄せる(従来)。")]
        public SwarmReplicaSmoothing smoothing = SwarmReplicaSmoothing.RenderOffset;

        [Tooltip("Client側で、Hostの位置とのズレを詰める速さ。大きいほど早く合うが、敵が滑るように動いて見えやすい。")]
        [Min(0.1f)]
        public float correctionSharpness = 8f;

        [Tooltip("Hostの位置とこれ以上ズレていたら、滑らせずにその場で合わせる距離(ワールド単位)。")]
        [Min(0.1f)]
        public float snapDistance = 4f;

        [Tooltip("届くまでの遅れの見込み(秒)。Hostの位置をその敵の速さ×この秒数だけ先へ進めた所を目標にする。")]
        [Min(0f)]
        public float correctionLeadSeconds = 0.05f;

        [Header("位置の補正 — 優先度つき(段階5)")]
        [Tooltip("Priority=大事な敵・ズレていそうな敵から、決めた通信量の範囲で送る(既定)。RoundRobin=番号順に全員を同じ間隔で送る(段階4まで。比較用)。")]
        public SwarmCorrectionMode correctionMode = SwarmCorrectionMode.Priority;

        [Tooltip("普段の補正の通信量(Client1人あたり、KB/秒)。既定720は3万体を0.25秒に1回ずつ送るのと同じ量(約6Mbps)。")]
        [Min(16f)]
        public float normalBudgetKBps = 720f;

        [Tooltip("ズレが大きい間に増やす上限(Client1人あたり、KB/秒)。既定1800は3万体を0.1秒に1回ずつと同じ量(約15Mbps、有線LAN向け)。")]
        [Min(16f)]
        public float maxBudgetKBps = 1800f;

        [Tooltip("回線が混んでいるときに下げる下限(Client1人あたり、KB/秒)。")]
        [Min(8f)]
        public float minBudgetKBps = 120f;

        [Tooltip("往復時間(RTT、0.5秒でならした値)がそのClientの一番良かった値よりこれ以上(ミリ秒)伸びたら、回線が混んできたとみなして通信量を下げる。" +
                 "NGOのRTTにはフレーム時間も含まれる(FPSが下がるだけで10〜25ms伸びる)ので、小さくしすぎないこと。")]
        [Min(1f)]
        public float congestionRttMs = 40f;

        [Tooltip("ONなら位置と一緒に速度も送る(1体6→10バイト)。Clientは自分で計算した速度ではなくHostの速度で先読みする。")]
        public bool sendVelocity;

        [Tooltip("そのClientの画面に映っている敵の大事さに足す値(1が基本)。")]
        [Min(0f)]
        public float viewWeight = 3f;

        [Tooltip("レーザーの近くの敵の大事さに足す値。当たっているのに倒れない等の違和感を減らす。")]
        [Min(0f)]
        public float beamWeight = 4f;

        [Tooltip("コアの近くの敵の大事さに足す値。")]
        [Min(0f)]
        public float coreWeight = 2f;

        [Tooltip("画面の外側に足す余白(ワールド単位)。画面に入ってくる直前の敵も優先する。")]
        [Min(0f)]
        public float viewMargin = 3f;

        [Tooltip("レーザーの太さに足す「近く」の幅(ワールド単位)。")]
        [Min(0f)]
        public float beamMargin = 1.5f;

        [Tooltip("コアからこの距離以内を「近く」とする(ワールド単位)。")]
        [Min(0f)]
        public float coreRadius = 4f;

        [Tooltip("ズレの見込みが0の敵でも貯まる分(ワールド単位)。大きいほど、ズレていない敵にも満遍なく配る。")]
        [Min(0.01f)]
        public float baseError = 0.25f;

        [Tooltip("同じ敵を続けて送らない最短の間隔(秒)。敵が少ないときに同じ敵ばかり送らないように。")]
        [Min(0f)]
        public float minCorrectionInterval = 0.05f;

        [Tooltip("これ以上送っていない敵は、優先度に関係なく先に送る(秒)。画面外の敵もこの間隔以内には必ず補正される。")]
        [Min(0.1f)]
        public float maxCorrectionAge = 1f;

        [Header("Clientの計算の軽さ")]
        [Tooltip("Clientでの押し合い計算の反復回数。Clientの位置はHostが補正するので、群衆の設定より少なくして計算を軽くできる。-1なら群衆の設定どおり。")]
        [Min(-1)]
        public int replicaSeparationIterations = 1;

        // 番号で分けるグループの数。1回の補正の送信で1グループ分を送り、全グループで1周する(=全員に1回ずつ届く)。
        private const int CorrectionGroups = 10;

        // ホストが1フレームに送る補正の回数の上限(フレームが重いときにまとめて送りすぎないため)。
        private const int MaxCorrectionSendsPerFrame = 4;

        // Clientがズレを報告する間隔(秒)と、Hostがその報告を有効とみなす時間(秒)。
        private const float ErrorReportInterval = 0.5f;
        private const float ErrorReportLifetime = 2f;

        // 出現/消滅・全体の状態は確実に届ける送信(分割されるので大きくてよい)。順番を保つため両方同じ届け方にする。
        private const NetworkDelivery ReliableDelivery = NetworkDelivery.ReliableFragmentedSequenced;

        // 補正(取りこぼしてよい送信)は分割されず1回1296バイトが上限なので、NetMessageLimitsで決める(6バイト×200件)。
        private static readonly int CorrectionsPerMessage = NetMessageLimits.UnreliableItemsPerMessage<SwarmNetCorrection>();

        private static readonly int CorrectionsWithVelocityPerMessage = NetMessageLimits.UnreliableItemsPerMessage<SwarmNetCorrectionV>();

        private static readonly int EventSize = UnsafeUtility.SizeOf<SwarmNetEvent>();
        private static readonly int CorrectionSize = UnsafeUtility.SizeOf<SwarmNetCorrection>();
        private static readonly int CorrectionWithVelocitySize = UnsafeUtility.SizeOf<SwarmNetCorrectionV>();

        // 補正のメッセージの見出し(連番 + 種類)。1=速度つき、2=抜き取り検査(優先度と関係なく選んだ物。Clientがズレを偏りなく測るため)。
        private const byte CorrectionFlagVelocity = 1;
        private const byte CorrectionFlagAudit = 2;

        // 優先度つきの補正と並行して、番号で64組に分けた1組を毎秒4回「抜き取り検査」として送る(3万体で毎秒約1900体・約11KB/秒)。
        private const int AuditGroups = 64;
        private const float AuditGroupsPerSecond = 4f;

        // 「最後に送った時刻」の印: まだその敵の出現(または全体の状態)をそのClientへ送っていない。
        // 経過時間が負になるので補正の対象にならない(届く前の補正はClientで捨てられ無駄になるため)。
        private const float NotYetSpawnedTime = 1e9f;

        private readonly EnemyTypeNetIndex _types = new();
        private SwarmSystem _swarm;

        // Host側
        private readonly SwarmNetEventQueue _queue = new();
        private readonly List<SwarmNetEvent> _takeBuffer = new();
        private readonly List<ulong> _fullStateRequests = new();
        private readonly List<FullStateTransfer> _transfers = new();
        private readonly List<ulong> _remoteClients = new();
        private NativeList<SwarmNetCorrection> _gathered;
        private NativeList<int> _gatheredIndices;
        private float _auditAccumulator;
        private int _auditPhase;
        private float _eventCooldown;
        private float _correctionAccumulator;
        private int _correctionPhase;
        private readonly SwarmCorrectionRateController _rateController = new();
        private readonly Dictionary<ulong, ClientCorrection> _clientStates = new();
        private readonly List<ClientCorrection> _clientList = new();
        private uint _correctionSequence;
        private bool _unsupportedLogged;

        // Client側
        private readonly bool[] _alive = new bool[SwarmSystem.MaxNetIds];
        private readonly List<SwarmNetEvent> _bufferedEvents = new();
        private bool _hasFullState;
        private bool _receivingFullState;
        private bool _layoutMismatch;
        private uint _lastCorrectionSequence;
        private float _errorReportTimer;
        private int _errorCount;
        private float _errorSum;
        private int _auditErrorCount;
        private float _auditErrorSum;

        private sealed class FullStateTransfer
        {
            public ulong ClientId;
            public SwarmNetEvent[] Events;
            public int Sent;
        }

        // Host: 1人のClientへの補正の状態(段階5)。優先度・最後に送った値は番号(netId)で引く。
        private sealed class ClientCorrection : IDisposable
        {
            public ulong ClientId;
            public NativeArray<float> Priority;
            public NativeArray<SwarmSentState> Sent;
            public NativeArray<float> Scores;
            public NativeArray<float> Scratch;
            public NativeList<SwarmNetCorrection> Output;
            public NativeList<SwarmNetCorrectionV> OutputWithVelocity;
            public readonly SwarmCorrectionBudgetController Budget = new();
            public float ItemCarry;
            public float4 View;
            public bool HasView;
            public float Error = -1f;
            public float ErrorTime = float.MinValue;
            public float RttMs = -1f;

            public ClientCorrection(ulong clientId, int capacity)
            {
                ClientId = clientId;
                Priority = new NativeArray<float>(SwarmSystem.MaxNetIds, Allocator.Persistent);
                Sent = new NativeArray<SwarmSentState>(SwarmSystem.MaxNetIds, Allocator.Persistent);
                Scores = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Scratch = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                Output = new NativeList<SwarmNetCorrection>(4096, Allocator.Persistent);
                OutputWithVelocity = new NativeList<SwarmNetCorrectionV>(4096, Allocator.Persistent);

                // 今いる敵は、全体の状態として送った時点から補正の対象にする。
                for (var i = 0; i < Sent.Length; i++)
                {
                    Sent[i] = new SwarmSentState { time = NotYetSpawnedTime };
                }
            }

            // 出現(または全体の状態)をこのClientへ送った。届いた位置を「最後に送った値」として、補正の優先度を0から貯め直す。
            public void MarkSpawnSent(in SwarmNetEvent e, float now)
            {
                Sent[e.Id] = new SwarmSentState
                {
                    pos = new float2(SwarmNetQuantize.FromShort(e.X), SwarmNetQuantize.FromShort(e.Y)),
                    vel = float2.zero,
                    time = now
                };
                Priority[e.Id] = 0f;
            }

            public void Dispose()
            {
                Priority.Dispose();
                Sent.Dispose();
                Scores.Dispose();
                Scratch.Dispose();
                Output.Dispose();
                OutputWithVelocity.Dispose();
            }
        }

        public override void OnNetworkSpawn()
        {
            _swarm = SwarmSystem.Current;
            if (_swarm == null)
            {
                Debug.LogWarning("[Net] SwarmNetworkHub: シーンにSwarmSystemが無いため、群衆は同期しません。");
                return;
            }

            _types.Build();

            if (IsServer)
            {
                _queue.Clear();
                _transfers.Clear();
                _fullStateRequests.Clear();
                if (!_gathered.IsCreated)
                {
                    _gathered = new NativeList<SwarmNetCorrection>(4096, Allocator.Persistent);
                    _gatheredIndices = new NativeList<int>(4096, Allocator.Persistent);
                }

                if (UsesSnapshots)
                {
                    BeginSnapshotHost();
                }

                _swarm.AgentsSpawned += OnAgentsSpawned;
                _swarm.AgentsRemoved += OnAgentsRemoved;
                _swarm.StorageReadable += OnStorageReadable;
                return;
            }

            Array.Clear(_alive, 0, _alive.Length);
            _bufferedEvents.Clear();
            _hasFullState = false;
            _receivingFullState = false;
            _layoutMismatch = false;
            _lastCorrectionSequence = 0;
            _swarm.ConfigureReplicaCorrection(correctionSharpness, snapDistance, correctionLeadSeconds, smoothing);
            _errorReportTimer = 0f;
            _errorCount = 0;
            _errorSum = 0f;
            _auditErrorCount = 0;
            _auditErrorSum = 0f;
            _swarm.SetReplicaSeparationIterations(replicaSeparationIterations);
            if (UsesSnapshots)
            {
                BeginSnapshotClient();
            }
            else
            {
                _swarm.SetReplicaSource(SwarmReplicaSource.Corrections);
            }

            _swarm.BeginReplica();
            NetTrafficStats.SwarmFullStateReceived = false;

            FortressNetMessages.Register(NetworkManager, FortressNetChannel.SwarmEvents, OnEventsMessage);
            FortressNetMessages.Register(NetworkManager, FortressNetChannel.SwarmFullState, OnFullStateMessage);
            FortressNetMessages.Register(NetworkManager, FortressNetChannel.SwarmCorrections, OnCorrectionsMessage);
            FortressNetMessages.Register(NetworkManager, FortressNetChannel.SwarmSnapshot, OnSnapshotMessage);
            FortressNetMessages.Register(NetworkManager, FortressNetChannel.SwarmKeyframe, OnKeyframeMessage);
            RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            FortressNetMessages.Unregister(FortressNetChannel.SwarmEvents);
            FortressNetMessages.Unregister(FortressNetChannel.SwarmFullState);
            FortressNetMessages.Unregister(FortressNetChannel.SwarmCorrections);
            FortressNetMessages.Unregister(FortressNetChannel.SwarmSnapshot);
            FortressNetMessages.Unregister(FortressNetChannel.SwarmKeyframe);

            if (_swarm == null)
            {
                DisposeSnapshotState();
                return;
            }

            _swarm.AgentsSpawned -= OnAgentsSpawned;
            _swarm.AgentsRemoved -= OnAgentsRemoved;
            _swarm.StorageReadable -= OnStorageReadable;
            _queue.Clear();
            _transfers.Clear();
            DisposeClientStates();

            // 切断したClientでは、Hostの群衆はもう更新されないので消す。
            _swarm.EndReplica();
            Array.Clear(_alive, 0, _alive.Length);
            _bufferedEvents.Clear();
            DisposeSnapshotState();
        }

        public override void OnDestroy()
        {
            if (_gathered.IsCreated)
            {
                _gathered.Dispose();
                _gatheredIndices.Dispose();
            }

            DisposeClientStates();
            DisposeSnapshotState();
            base.OnDestroy();
        }

        private void DisposeClientStates()
        {
            foreach (var state in _clientList)
            {
                state.Dispose();
            }

            _clientList.Clear();
            _clientStates.Clear();
        }

        private void Update()
        {
            if (!IsSpawned || _swarm == null)
            {
                return;
            }

            if (!IsServer)
            {
                if (!UsesSnapshots)
                {
                    ReportErrorPeriodically();
                }

                return;
            }

            if (UsesSnapshots)
            {
                return; // 写真方式では出現・消滅も写真で送る(SnapshotHostTick)。
            }

            _eventCooldown -= Time.unscaledDeltaTime;
            if (_eventCooldown > 0f)
            {
                return;
            }

            _eventCooldown = 1f / eventSendRate;
            var budget = Mathf.Max(1, Mathf.RoundToInt(maxEventsPerSecond / eventSendRate));
            RefreshRemoteClients();
            SyncClientStates();
            FlushEvents(budget);
            FlushFullStateTransfers(budget);
            NetTrafficStats.SwarmEventBacklog = _queue.Count;
            NetTrafficStats.SwarmFullStateTransfers = _transfers.Count;
        }

        // ------------------------------------------------------------------ Host

        private void OnAgentsSpawned(IReadOnlyList<SwarmSystem.SpawnRecord> records)
        {
            if (UsesSnapshots || !HasRemoteClients())
            {
                return; // 後から来たClientには全体の状態で伝わる。
            }

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (!_types.TryGetIndex(record.Type, out var typeIndex))
                {
                    LogUnsupportedOnce();
                    continue;
                }

                // 出現を実際に送るまで(一斉投入では送信待ちが続く)は、補正の対象にしない(FlushEventsで送った時点から対象にする)。
                for (var c = 0; c < _clientList.Count; c++)
                {
                    _clientList[c].Sent[record.Id] = new SwarmSentState { pos = record.Position, time = NotYetSpawnedTime };
                    _clientList[c].Priority[record.Id] = 0f;
                }

                _queue.EnqueueSpawn(new SwarmNetEvent
                {
                    Id = (ushort)record.Id,
                    TypeOrReason = (byte)typeIndex,
                    X = SwarmNetQuantize.ToShort(record.Position.x),
                    Y = SwarmNetQuantize.ToShort(record.Position.y),
                    SpeedScale = SwarmNetQuantize.ToHalf(record.SpeedScale),
                    AnimStart = SwarmNetQuantize.ToHalf(record.AnimStart),
                    Facing = SwarmNetQuantize.ToAngleByte(record.Facing)
                });
            }
        }

        private void OnAgentsRemoved(IReadOnlyList<SwarmSystem.RemovalRecord> records)
        {
            if (UsesSnapshots)
            {
                RecordRemovalsForSnapshot(records);
                return;
            }

            if (!HasRemoteClients())
            {
                return;
            }

            for (var i = 0; i < records.Count; i++)
            {
                _queue.EnqueueDespawn((ushort)records[i].Id, (byte)records[i].Reason);
            }
        }

        // SwarmSystemの計算が止まっている瞬間。途中参加者への全体の状態と、位置の補正はここで読む。
        private void OnStorageReadable()
        {
            if (UsesSnapshots)
            {
                SnapshotHostTick();
                return;
            }

            if (_fullStateRequests.Count > 0)
            {
                var snapshot = CaptureAll();
                foreach (var clientId in _fullStateRequests)
                {
                    _transfers.Add(new FullStateTransfer { ClientId = clientId, Events = snapshot });
                }

                _fullStateRequests.Clear();
            }

            if (!HasRemoteClients())
            {
                _correctionAccumulator = 0f;
                return;
            }

            RefreshRemoteClients();
            SyncClientStates();
            if (correctionMode == SwarmCorrectionMode.Priority)
            {
                SendPriorityCorrections(Time.unscaledDeltaTime);
                SendAuditPeriodically(Time.unscaledDeltaTime);
                return;
            }

            // 1周(=全グループ)を cycle 秒で回すので、1秒あたりの送信回数は CorrectionGroups / cycle。
            var cycle = CurrentCorrectionCycle(Time.unscaledDeltaTime);
            NetTrafficStats.SwarmCorrectionCycle = cycle;
            _correctionAccumulator = Mathf.Min(_correctionAccumulator + Time.unscaledDeltaTime * CorrectionGroups / cycle, MaxCorrectionSendsPerFrame);
            if (_correctionAccumulator < 1f)
            {
                return;
            }

            while (_correctionAccumulator >= 1f)
            {
                _correctionAccumulator -= 1f;
                SendCorrectionGroup();
            }
        }

        private float CurrentCorrectionCycle(float deltaTime)
        {
            if (!adaptiveCorrection)
            {
                return correctionCycleSeconds;
            }

            _rateController.MaxCycle = correctionCycleSeconds;
            _rateController.MinCycle = minCorrectionCycleSeconds;
            _rateController.HighError = highErrorThreshold;
            _rateController.LowError = lowErrorThreshold;

            var worst = -1f;
            for (var i = 0; i < _clientList.Count; i++)
            {
                var error = FreshError(_clientList[i]);
                if (error > worst)
                {
                    worst = error;
                }
            }

            NetTrafficStats.SwarmReportedError = worst;
            return _rateController.Update(deltaTime, worst);
        }

        // 途中参加は稀なので、ここだけは配列を作ってよい(送り終えるまで保持する)。
        private SwarmNetEvent[] CaptureAll()
        {
            var storage = _swarm.Storage;
            var result = new List<SwarmNetEvent>(storage.Count);
            for (var i = 0; i < storage.Count; i++)
            {
                if (!_types.TryGetIndex(_swarm.GetRegisteredType(storage.typeIdx[i]), out var typeIndex))
                {
                    LogUnsupportedOnce();
                    continue;
                }

                var position = storage.pos[i];
                var facing = storage.facing[i];
                result.Add(new SwarmNetEvent
                {
                    Id = (ushort)storage.netId[i],
                    Kind = SwarmNetEventKind.Spawn,
                    TypeOrReason = (byte)typeIndex,
                    X = SwarmNetQuantize.ToShort(position.x),
                    Y = SwarmNetQuantize.ToShort(position.y),
                    SpeedScale = SwarmNetQuantize.ToHalf(storage.speedScale[i]),
                    AnimStart = SwarmNetQuantize.ToHalf(storage.animTime[i]),
                    Facing = SwarmNetQuantize.ToAngleByte(new Vector2(facing.x, facing.y))
                });
            }

            return result.ToArray();
        }

        private float FreshError(ClientCorrection state)
        {
            return Time.unscaledTime - state.ErrorTime <= ErrorReportLifetime ? state.Error : -1f;
        }

        // つながっているClientの補正の状態を作る/抜けたClientの分を捨てる(_remoteClients を更新した後に呼ぶ)。
        private void SyncClientStates()
        {
            for (var i = 0; i < _remoteClients.Count; i++)
            {
                GetOrCreateClientState(_remoteClients[i]);
            }

            for (var i = _clientList.Count - 1; i >= 0; i--)
            {
                var state = _clientList[i];
                if (_remoteClients.Contains(state.ClientId))
                {
                    continue;
                }

                state.Dispose();
                _clientList.RemoveAt(i);
                _clientStates.Remove(state.ClientId);
            }
        }

        private ClientCorrection GetOrCreateClientState(ulong clientId)
        {
            if (_clientStates.TryGetValue(clientId, out var state))
            {
                return state;
            }

            state = new ClientCorrection(clientId, _swarm.Storage.Capacity);
            _clientStates.Add(clientId, state);
            _clientList.Add(state);
            return state;
        }

        private SwarmPriorityWeights PriorityWeights => new()
        {
            view = viewWeight,
            beam = beamWeight,
            core = coreWeight,
            baseError = baseError,
            beamMargin = beamMargin,
            coreRadius = coreRadius,
            minInterval = minCorrectionInterval,
            maxAge = maxCorrectionAge,
            maxPredictSeconds = 0.5f
        };

        // 段階5: Clientごとに、貯めた優先度の高い順に、通信量の枠に収まる数だけ送る。全員分のジョブをまとめて動かしてから送る。
        private void SendPriorityCorrections(float deltaTime)
        {
            var storage = _swarm.Storage;
            var count = storage.Count;
            if (count <= 0 || _clientList.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            var itemSize = sendVelocity ? CorrectionWithVelocitySize : CorrectionSize;
            var weights = PriorityWeights;
            var beams = _swarm.RecentBeams;
            var cores = _swarm.CorePositions;
            var transport = NetworkManager.NetworkConfig.NetworkTransport;
            var handle = default(JobHandle);
            var worstBudget = 0f;
            var worstRtt = -1f;
            var worstError = -1f;
            var backoffs = 0;

            for (var c = 0; c < _clientList.Count; c++)
            {
                var state = _clientList[c];
                state.RttMs = transport != null ? transport.GetCurrentRtt(state.ClientId) : -1f;
                var bytesPerSecond = UpdateBudget(state, deltaTime);
                worstBudget = Mathf.Max(worstBudget, bytesPerSecond);
                worstRtt = Mathf.Max(worstRtt, state.RttMs);
                worstError = Mathf.Max(worstError, FreshError(state));
                backoffs += state.Budget.Backoffs;

                // 枠の端数は次のフレームへ持ち越す(使い切れなかった枠は貯めない)。
                state.ItemCarry += bytesPerSecond * deltaTime / itemSize;
                var budget = (int)state.ItemCarry;
                state.ItemCarry -= budget;

                var view = state.View;
                var margin = new float4(-viewMargin, -viewMargin, viewMargin, viewMargin);
                var priorityHandle = new SwarmCorrectionPriorityJob
                {
                    netId = storage.netId,
                    pos = storage.pos,
                    beams = beams,
                    cores = cores,
                    sent = state.Sent,
                    priority = state.Priority,
                    scores = state.Scores,
                    weights = weights,
                    view = view + margin,
                    hasView = state.HasView ? 1 : 0,
                    now = now,
                    dt = deltaTime
                }.Schedule(count, 512);

                var selectHandle = new SwarmCorrectionSelectJob
                {
                    netId = storage.netId,
                    pos = storage.pos,
                    vel = storage.vel,
                    scores = state.Scores,
                    scratch = state.Scratch,
                    sent = state.Sent,
                    priority = state.Priority,
                    output = state.Output,
                    outputWithVelocity = state.OutputWithVelocity,
                    count = count,
                    budget = budget,
                    withVelocity = sendVelocity ? 1 : 0,
                    now = now,
                    positionScale = SwarmNetQuantize.PositionScale
                }.Schedule(priorityHandle);

                handle = JobHandle.CombineDependencies(handle, selectHandle);
            }

            handle.Complete();

            NetTrafficStats.SwarmCorrectionBudgetKBs = worstBudget / 1024f;
            NetTrafficStats.SwarmCorrectionCycle = worstBudget > 0f ? count * itemSize / worstBudget : 0f;
            NetTrafficStats.SwarmRttMs = worstRtt;
            NetTrafficStats.SwarmReportedError = worstError;
            NetTrafficStats.SwarmCorrectionBackoffs = backoffs;

            for (var c = 0; c < _clientList.Count; c++)
            {
                var state = _clientList[c];
                if (sendVelocity)
                {
                    SendCorrections(state.ClientId, state.OutputWithVelocity.AsArray(), CorrectionsWithVelocityPerMessage, CorrectionWithVelocitySize, CorrectionFlagVelocity);
                }
                else
                {
                    SendCorrections(state.ClientId, state.Output.AsArray(), CorrectionsPerMessage, CorrectionSize, 0);
                }
            }
        }

        // 抜き取り検査: 優先度と関係なく番号で選んだ1組を、全Clientへ送る(普通の補正としても使われる)。
        // 優先度つきでは「ズレていそうな敵」を選んで送るので、届いた補正のズレは大きめに出る。これで全員のズレを偏りなく測れる。
        private void SendAuditPeriodically(float deltaTime)
        {
            _auditAccumulator = Mathf.Min(_auditAccumulator + deltaTime * AuditGroupsPerSecond, 1f);
            if (_auditAccumulator < 1f)
            {
                return;
            }

            _auditAccumulator -= 1f;
            var storage = _swarm.Storage;
            new SwarmCorrectionGatherJob
            {
                netId = storage.netId,
                pos = storage.pos,
                count = storage.Count,
                groups = AuditGroups,
                phase = _auditPhase++ % AuditGroups,
                output = _gathered,
                indices = _gatheredIndices
            }.Schedule().Complete();

            if (_gathered.Length == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var c = 0; c < _clientList.Count; c++)
            {
                var state = _clientList[c];
                for (var k = 0; k < _gatheredIndices.Length; k++)
                {
                    var index = _gatheredIndices[k];
                    var id = storage.netId[index];

                    // まだ出現を送っていない敵は、届いてもClientで捨てられるので記録しない。
                    if (state.Sent[id].time >= NotYetSpawnedTime)
                    {
                        continue;
                    }

                    state.Sent[id] = new SwarmSentState { pos = storage.pos[index], vel = storage.vel[index], time = now };
                    state.Priority[id] = 0f;
                }

                SendCorrections(state.ClientId, _gathered.AsArray(), CorrectionsPerMessage, CorrectionSize, CorrectionFlagAudit);
            }
        }

        private float UpdateBudget(ClientCorrection state, float deltaTime)
        {
            var budget = state.Budget;
            budget.NormalBytesPerSecond = normalBudgetKBps * 1024f;
            budget.MaxBytesPerSecond = Mathf.Max(normalBudgetKBps, maxBudgetKBps) * 1024f;
            budget.MinBytesPerSecond = Mathf.Min(minBudgetKBps, normalBudgetKBps) * 1024f;
            budget.HighError = highErrorThreshold;
            budget.LowError = lowErrorThreshold;
            budget.CongestionRttMs = congestionRttMs;

            // 自動調整を切ったときは、ズレも混み具合も見ずに普段の量で送る。
            return adaptiveCorrection
                ? budget.Update(deltaTime, FreshError(state), state.RttMs)
                : budget.Update(deltaTime, -1f, -1f);
        }

        private void SendCorrections<T>(ulong clientId, NativeArray<T> all, int perMessage, int itemSize, byte flags) where T : unmanaged
        {
            if (all.Length == 0)
            {
                return;
            }

            _correctionSequence++;
            for (var start = 0; start < all.Length; start += perMessage)
            {
                var chunk = all.GetSubArray(start, Mathf.Min(perMessage, all.Length - start));
                using var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmCorrections, 9 + chunk.Length * itemSize);
                writer.WriteValueSafe(_correctionSequence);
                writer.WriteByteSafe(flags);
                writer.WriteValueSafe(chunk, default(FastBufferWriter.ForGeneric));
                FortressNetMessages.Send(NetworkManager, clientId, writer, NetworkDelivery.Unreliable);
                NetTrafficStats.SwarmCorrectionsSent += chunk.Length;
                NetTrafficStats.Sent((long)chunk.Length * itemSize);
            }
        }

        // 番号を CorrectionGroups 個のグループに分け、今回のグループの敵の位置を送る(全グループで全員に1回ずつ届く)。
        private void SendCorrectionGroup()
        {
            var storage = _swarm.Storage;
            new SwarmCorrectionGatherJob
            {
                netId = storage.netId,
                pos = storage.pos,
                count = storage.Count,
                groups = CorrectionGroups,
                phase = _correctionPhase++ % CorrectionGroups,
                output = _gathered,
                indices = _gatheredIndices
            }.Schedule().Complete();

            if (_gathered.Length == 0)
            {
                return;
            }

            _correctionSequence++;
            var all = _gathered.AsArray();
            for (var start = 0; start < all.Length; start += CorrectionsPerMessage)
            {
                var chunk = all.GetSubArray(start, Mathf.Min(CorrectionsPerMessage, all.Length - start));
                using var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmCorrections, 9 + chunk.Length * CorrectionSize);
                writer.WriteValueSafe(_correctionSequence);

                // 番号順に全員を送る方式は、どの補正も優先度と関係なく選ばれている(=全てが抜き取り検査と同じ扱い)。
                writer.WriteByteSafe(CorrectionFlagAudit);
                writer.WriteValueSafe(chunk, default(FastBufferWriter.ForGeneric));
                SendToRemoteClients(writer, NetworkDelivery.Unreliable);
                NetTrafficStats.SwarmCorrectionsSent += chunk.Length;
                NetTrafficStats.Sent((long)chunk.Length * CorrectionSize);
            }
        }

        private void FlushEvents(int budget)
        {
            if (!HasRemoteClients())
            {
                _queue.Clear();
                return;
            }

            _takeBuffer.Clear();
            if (_queue.TakeInto(_takeBuffer, budget) == 0)
            {
                return;
            }

            using var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmEvents, 4 + _takeBuffer.Count * EventSize);
            WriteEvents(writer, _takeBuffer, 0, _takeBuffer.Count);
            SendToRemoteClients(writer, ReliableDelivery);
            NetTrafficStats.SwarmEventsSent += _takeBuffer.Count;
            NetTrafficStats.Sent((long)_takeBuffer.Count * EventSize);

            var now = Time.unscaledTime;
            for (var i = 0; i < _takeBuffer.Count; i++)
            {
                var e = _takeBuffer[i];
                if (e.Kind != SwarmNetEventKind.Spawn)
                {
                    continue;
                }

                for (var c = 0; c < _clientList.Count; c++)
                {
                    _clientList[c].MarkSpawnSent(e, now);
                }
            }
        }

        private void FlushFullStateTransfers(int budget)
        {
            for (var t = _transfers.Count - 1; t >= 0; t--)
            {
                var transfer = _transfers[t];
                if (!NetworkManager.ConnectedClients.ContainsKey(transfer.ClientId))
                {
                    _transfers.RemoveAt(t);
                    continue;
                }

                // 0体でも「最後」を1回送って、受け取り終わったことを伝える。
                var count = Mathf.Min(budget, transfer.Events.Length - transfer.Sent);
                var isLast = transfer.Sent + count >= transfer.Events.Length;
                using (var writer = FortressNetMessages.Begin(FortressNetChannel.SwarmFullState, 9 + count * EventSize))
                {
                    writer.WriteValueSafe(_types.LayoutHash);
                    writer.WriteValueSafe(isLast);
                    WriteEvents(writer, transfer.Events, transfer.Sent, count);
                    FortressNetMessages.Send(NetworkManager, transfer.ClientId, writer, ReliableDelivery);
                }

                if (_clientStates.TryGetValue(transfer.ClientId, out var state))
                {
                    var now = Time.unscaledTime;
                    for (var i = 0; i < count; i++)
                    {
                        state.MarkSpawnSent(transfer.Events[transfer.Sent + i], now);
                    }
                }

                transfer.Sent += count;
                NetTrafficStats.SwarmEventsSent += count;
                NetTrafficStats.Sent((long)count * EventSize);

                if (isLast)
                {
                    _transfers.RemoveAt(t);
                }
            }
        }

        private static void WriteEvents(FastBufferWriter writer, IReadOnlyList<SwarmNetEvent> events, int start, int count)
        {
            writer.WriteValueSafe(count);
            for (var i = 0; i < count; i++)
            {
                var e = events[start + i];
                writer.WriteValueSafe(in e);
            }
        }

        private void RefreshRemoteClients()
        {
            _remoteClients.Clear();
            var ids = NetworkManager.ConnectedClientsIds;
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] != NetworkManager.ServerClientId)
                {
                    _remoteClients.Add(ids[i]);
                }
            }
        }

        private void SendToRemoteClients(FastBufferWriter writer, NetworkDelivery delivery)
        {
            for (var i = 0; i < _remoteClients.Count; i++)
            {
                FortressNetMessages.Send(NetworkManager, _remoteClients[i], writer, delivery);
            }
        }

        private bool HasRemoteClients()
        {
            return NetworkManager != null && NetworkManager.ConnectedClientsIds.Count > 1;
        }

        private void LogUnsupportedOnce()
        {
            if (_unsupportedLogged)
            {
                return;
            }

            _unsupportedLogged = true;
            Debug.LogWarning("[Net] SwarmNetworkHub: ウェーブ設定に入っていない種類の群衆がいるため、その敵はClientに表示されません。");
        }

        // view: Clientの画面に映る範囲(xMin, yMin, xMax, yMax)。hasView=false なら分からない(画面内の優先をしない)。
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, RequireOwnership = false)]
        private void ReportCorrectionErrorRpc(float averageError, Vector4 view, bool hasView, RpcParams rpcParams = default)
        {
            if (_swarm == null)
            {
                return;
            }

            var senderId = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.ContainsKey(senderId))
            {
                return;
            }

            var state = GetOrCreateClientState(senderId);
            state.ErrorTime = Time.unscaledTime;
            state.Error = averageError;
            state.View = new float4(view.x, view.y, view.z, view.w);
            state.HasView = hasView;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            if (_swarm != null)
            {
                _fullStateRequests.Add(rpcParams.Receive.SenderClientId);
            }
        }

        // ------------------------------------------------------------------ Client

        private void OnFullStateMessage(ulong sender, FastBufferReader reader)
        {
            if (_swarm == null || _layoutMismatch || IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint layoutHash);
            reader.ReadValueSafe(out bool isLast);
            reader.ReadValueSafe(out int count);
            NetTrafficStats.SwarmEventsReceived += count;
            NetTrafficStats.Received((long)count * EventSize);

            if (!_receivingFullState)
            {
                _receivingFullState = true;
                if (layoutHash != _types.LayoutHash)
                {
                    // 敵の種類の並びが違う=違うビルド。違う種類で表示してしまうので同期をやめる。
                    _layoutMismatch = true;
                    Debug.LogError("[Net] SwarmNetworkHub: 敵の種類の並びがHostと一致しません。HostとClientが同じビルドか確認してください。群衆は同期しません。");
                    _swarm.EndReplica();
                    return;
                }
            }

            for (var i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out SwarmNetEvent e);
                Apply(e);
            }

            if (!isLast)
            {
                return;
            }

            _hasFullState = true;
            foreach (var buffered in _bufferedEvents)
            {
                Apply(buffered);
            }

            _bufferedEvents.Clear();
            NetTrafficStats.SwarmBufferedEvents = 0;
            NetTrafficStats.SwarmFullStateReceived = true;
        }

        private void OnEventsMessage(ulong sender, FastBufferReader reader)
        {
            if (_swarm == null || _layoutMismatch || IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int count);
            NetTrafficStats.SwarmEventsReceived += count;
            NetTrafficStats.Received((long)count * EventSize);

            for (var i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out SwarmNetEvent e);

                // 全体の状態を受け取り終わるまでは貯めておき、受け取り終わってから順に反映する。
                if (_hasFullState)
                {
                    Apply(e);
                }
                else
                {
                    _bufferedEvents.Add(e);
                }
            }

            if (!_hasFullState)
            {
                NetTrafficStats.SwarmBufferedEvents = _bufferedEvents.Count;
            }
        }

        // 取りこぼしても次の周回で届くのでUnreliable。入れ替わって届いた古い回は捨てる(同じ回の分割分は同じ連番)。
        // 届いた位置は1件ずつ処理せず、そのままSwarmSystemへまとめて渡す(目標位置の表はジョブで作られる)。
        private void OnCorrectionsMessage(ulong sender, FastBufferReader reader)
        {
            if (_swarm == null || !_hasFullState || _layoutMismatch || IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint sequence);
            if ((int)(sequence - _lastCorrectionSequence) < 0)
            {
                return;
            }

            _lastCorrectionSequence = sequence;
            reader.ReadByteSafe(out var flags);
            if ((flags & CorrectionFlagVelocity) != 0)
            {
                reader.ReadValueSafe(out NativeArray<SwarmNetCorrectionV> withVelocity, Allocator.Temp, default(FastBufferWriter.ForGeneric));
                NetTrafficStats.SwarmCorrectionsReceived += withVelocity.Length;
                NetTrafficStats.Received((long)withVelocity.Length * CorrectionWithVelocitySize);
                _swarm.QueueCorrections(withVelocity);
                withVelocity.Dispose();
                return;
            }

            reader.ReadValueSafe(out NativeArray<SwarmNetCorrection> corrections, Allocator.Temp, default(FastBufferWriter.ForGeneric));
            NetTrafficStats.SwarmCorrectionsReceived += corrections.Length;
            NetTrafficStats.Received((long)corrections.Length * CorrectionSize);
            _swarm.QueueCorrections(corrections, (flags & CorrectionFlagAudit) != 0);
            corrections.Dispose();
        }

        // Client: Hostの位置が届いたときに測ったズレの平均と自分の画面の範囲を、一定間隔でHostへ伝える
        // (通信量の自動調整と、画面に映る敵の優先に使う)。届くまでの遅れ(往復時間の半分)もここで測り直す。
        private void ReportErrorPeriodically()
        {
            var stats = _swarm.Stats;
            _errorCount += stats.replicaCorrections;
            _errorSum += stats.replicaErrorSum;
            _auditErrorCount += stats.replicaAuditCorrections;
            _auditErrorSum += stats.replicaAuditErrorSum;

            _errorReportTimer += Time.unscaledDeltaTime;
            if (_errorReportTimer < ErrorReportInterval)
            {
                return;
            }

            _errorReportTimer = 0f;
            var hasView = TryGetLocalView(out var view);
            _swarm.SetReplicaViewRect(view, hasView);

            var transport = NetworkManager.NetworkConfig.NetworkTransport;
            var rttMs = transport != null ? transport.GetCurrentRtt(NetworkManager.ServerClientId) : 0f;
            NetTrafficStats.SwarmRttMs = rttMs;
            _swarm.SetReplicaMeasuredLead(rttMs * 0.0005f);

            if (_hasFullState)
            {
                // ズレがまだ測れていなくても、画面の範囲は伝える(負=ズレの報告なし)。
                // 抜き取り検査のズレがあればそれを使う(優先度つきでは、届いた補正全体のズレは「ズレていそうな敵」に偏って大きく出るため)。
                var error = _auditErrorCount > 0 ? _auditErrorSum / _auditErrorCount
                    : _errorCount > 0 ? _errorSum / _errorCount : -1f;
                ReportCorrectionErrorRpc(error, new Vector4(view.xMin, view.yMin, view.xMax, view.yMax), hasView);
            }

            _errorCount = 0;
            _errorSum = 0f;
            _auditErrorCount = 0;
            _auditErrorSum = 0f;
        }

        private static readonly Vector2[] ViewportCorners = { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) };

        // 自分の画面に映るマップ(XY平面、z=0)の範囲。画面の四隅から出る線とマップの交点を囲む矩形(回転・傾きにも対応)。
        // 視点の部品(FortressCameraRig)があればそのカメラ、無ければメインカメラ(Gameシーンは2026-10-05時点でメインカメラのみ)。
        private static bool TryGetLocalView(out Rect rect)
        {
            rect = default;
            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            if (camera == null)
            {
                return false;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var hits = 0;
            foreach (var corner in ViewportCorners)
            {
                var ray = camera.ViewportPointToRay(new Vector3(corner.x, corner.y, 0f));
                if (Mathf.Abs(ray.direction.z) < 1e-5f)
                {
                    continue;
                }

                // マップと交わらない(地平線より上を向いている)隅は、描画される一番遠い所までとする。
                var distance = -ray.origin.z / ray.direction.z;
                if (distance < 0f || distance > camera.farClipPlane)
                {
                    distance = camera.farClipPlane;
                }

                var point = (Vector2)ray.GetPoint(distance);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                hits++;
            }

            if (hits == 0)
            {
                return false;
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void Apply(SwarmNetEvent e)
        {
            if (e.Kind == SwarmNetEventKind.Spawn)
            {
                // 全体の状態と、その前後の出現が重なって届くことがあるので、知っている番号は無視する。
                if (_alive[e.Id])
                {
                    return;
                }

                var type = _types.Get(e.TypeOrReason);
                _alive[e.Id] = type != null && _swarm.SpawnReplica(
                    e.Id,
                    type,
                    new Vector2(SwarmNetQuantize.FromShort(e.X), SwarmNetQuantize.FromShort(e.Y)),
                    SwarmNetQuantize.FromHalf(e.SpeedScale),
                    SwarmNetQuantize.FromHalf(e.AnimStart),
                    SwarmNetQuantize.FromAngleByte(e.Facing));
            }
            else if (e.Kind == SwarmNetEventKind.Despawn && _alive[e.Id])
            {
                _alive[e.Id] = false;
                _swarm.RemoveReplica(e.Id, (EnemyRemovalReason)e.TypeOrReason);
            }
        }
    }
}
