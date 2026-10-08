using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 群衆(SwarmSystem、最大数万体)をネット対戦で同期する。他の同期部品と同じ「[Fortress] NetSync」に付ける。
    ///
    /// 方式(2026-10-04 ユーザー決定「各PCで計算＋Hostで補正」):
    /// ・Host: 今まで通り全てを計算する(湧かせる・倒す・コア到達・コアへのダメージ)。
    ///   出現/消滅は起きた順に確実に送る(1秒あたりの上限つき)。位置は番号でグループ分けし、毎回1グループずつ
    ///   「補正」として送る(既定: 全員を0.5秒に1回)。
    /// ・Client: SwarmSystemをClientモードにし、動き(経路・押し合い)は自分で計算しつつ、届いた位置へ少しずつ寄せる。
    ///   自分では湧かせず、倒さず、コアにダメージを与えない。レーザーの被弾フラッシュ(見た目)だけは出る。
    /// ・途中参加: 接続時点の全員を分割して受け取り、受け取り終わるまでに届いた変化は貯めておいて、後から順に反映する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwarmNetworkHub : NetworkBehaviour
    {
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

        [Header("Clientの計算の軽さ")]
        [Tooltip("Clientでの押し合い計算の反復回数。Clientの位置はHostが補正するので、群衆の設定より少なくして計算を軽くできる。-1なら群衆の設定どおり。")]
        [Min(-1)]
        public int replicaSeparationIterations = 1;

        // 1回の送信に詰める数。出現/消滅(確実に届ける送信)は分割されるので大きめでよい。
        // 補正(取りこぼしてよい送信)は分割されず1回1296バイトが上限なので、NetMessageLimitsで決める(6バイト×200件)。
        private const int EventsPerMessage = 256;

        // 番号で分けるグループの数。1回の補正の送信で1グループ分を送り、全グループで1周する(=全員に1回ずつ届く)。
        private const int CorrectionGroups = 10;

        // ホストが1フレームに送る補正の回数の上限(フレームが重いときにまとめて送りすぎないため)。
        private const int MaxCorrectionSendsPerFrame = 4;

        // Clientがズレを報告する間隔(秒)と、Hostがその報告を有効とみなす時間(秒)。
        private const float ErrorReportInterval = 0.5f;
        private const float ErrorReportLifetime = 2f;
        private static readonly int CorrectionsPerMessage = NetMessageLimits.UnreliableItemsPerMessage<SwarmNetCorrection>();

        private static readonly int EventSize = UnsafeUtility.SizeOf<SwarmNetEvent>();
        private static readonly int CorrectionSize = UnsafeUtility.SizeOf<SwarmNetCorrection>();

        private readonly EnemyTypeNetIndex _types = new();
        private SwarmSystem _swarm;

        // Host側
        private readonly SwarmNetEventQueue _queue = new();
        private readonly List<SwarmNetCorrection> _correctionBuffer = new();
        private readonly List<ulong> _fullStateRequests = new();
        private readonly List<FullStateTransfer> _transfers = new();
        private float _eventCooldown;
        private float _correctionAccumulator;
        private int _correctionPhase;
        private readonly SwarmCorrectionRateController _rateController = new();
        private readonly Dictionary<ulong, (float error, float time)> _reportedErrors = new();
        private uint _correctionSequence;
        private bool _unsupportedLogged;

        // Client側
        private readonly HashSet<ushort> _aliveIds = new();
        private readonly List<SwarmNetEvent> _bufferedEvents = new();
        private bool _hasFullState;
        private bool _receivingFullState;
        private bool _layoutMismatch;
        private uint _lastCorrectionSequence;
        private float _errorReportTimer;
        private int _errorCount;
        private float _errorSum;

        private sealed class FullStateTransfer
        {
            public ulong ClientId;
            public SwarmNetEvent[] Events;
            public int Sent;
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
                _swarm.AgentsSpawned += OnAgentsSpawned;
                _swarm.AgentsRemoved += OnAgentsRemoved;
                _swarm.StorageReadable += OnStorageReadable;
                return;
            }

            _aliveIds.Clear();
            _bufferedEvents.Clear();
            _hasFullState = false;
            _receivingFullState = false;
            _layoutMismatch = false;
            _lastCorrectionSequence = 0;
            _swarm.ConfigureReplicaCorrection(correctionSharpness, snapDistance, correctionLeadSeconds, smoothing);
            _errorReportTimer = 0f;
            _errorCount = 0;
            _errorSum = 0f;
            _swarm.SetReplicaSeparationIterations(replicaSeparationIterations);
            _swarm.BeginReplica();
            NetTrafficStats.SwarmFullStateReceived = false;
            RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (_swarm == null)
            {
                return;
            }

            _swarm.AgentsSpawned -= OnAgentsSpawned;
            _swarm.AgentsRemoved -= OnAgentsRemoved;
            _swarm.StorageReadable -= OnStorageReadable;
            _queue.Clear();
            _transfers.Clear();

            // 切断したClientでは、Hostの群衆はもう更新されないので消す。
            _swarm.EndReplica();
            _aliveIds.Clear();
            _bufferedEvents.Clear();
        }

        private void Update()
        {
            if (!IsSpawned || _swarm == null)
            {
                return;
            }

            if (!IsServer)
            {
                ReportErrorPeriodically();
                return;
            }

            _eventCooldown -= Time.unscaledDeltaTime;
            if (_eventCooldown > 0f)
            {
                return;
            }

            _eventCooldown = 1f / eventSendRate;
            var budget = Mathf.Max(1, Mathf.RoundToInt(maxEventsPerSecond / eventSendRate));
            FlushEvents(budget);
            FlushFullStateTransfers(budget);
            NetTrafficStats.SwarmEventBacklog = _queue.Count;
            NetTrafficStats.SwarmFullStateTransfers = _transfers.Count;
        }

        private void SendEvents(SwarmNetEvent[] events)
        {
            EventsRpc(events);
            NetTrafficStats.SwarmEventsSent += events.Length;
            NetTrafficStats.Sent((long)events.Length * EventSize);
        }

        private void SendCorrections(uint sequence, SwarmNetCorrection[] corrections)
        {
            CorrectionsRpc(sequence, corrections);
            NetTrafficStats.SwarmCorrectionsSent += corrections.Length;
            NetTrafficStats.Sent((long)corrections.Length * CorrectionSize);
        }

        // ------------------------------------------------------------------ Host

        private void OnAgentsSpawned(IReadOnlyList<SwarmSystem.SpawnRecord> records)
        {
            if (!HasRemoteClients())
            {
                return; // 後から来たClientには全体の状態で伝わる。
            }

            foreach (var record in records)
            {
                if (!_types.TryGetIndex(record.Type, out var typeIndex))
                {
                    LogUnsupportedOnce();
                    continue;
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
            if (!HasRemoteClients())
            {
                return;
            }

            foreach (var record in records)
            {
                _queue.EnqueueDespawn((ushort)record.Id, (byte)record.Reason);
            }
        }

        // SwarmSystemの計算が止まっている瞬間。途中参加者への全体の状態と、位置の補正はここで読む。
        private void OnStorageReadable()
        {
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

            // 1周(=全グループ)を cycle 秒で回すので、1秒あたりの送信回数は CorrectionGroups / cycle。
            var cycle = CurrentCorrectionCycle(Time.unscaledDeltaTime);
            NetTrafficStats.SwarmCorrectionCycle = cycle;
            _correctionAccumulator = Mathf.Min(_correctionAccumulator + Time.unscaledDeltaTime * CorrectionGroups / cycle, MaxCorrectionSendsPerFrame);
            while (_correctionAccumulator >= 1f)
            {
                _correctionAccumulator -= 1f;
                SendCorrections();
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
            var now = Time.unscaledTime;
            foreach (var report in _reportedErrors.Values)
            {
                if (now - report.time <= ErrorReportLifetime && report.error > worst)
                {
                    worst = report.error;
                }
            }

            NetTrafficStats.SwarmReportedError = worst;
            return _rateController.Update(deltaTime, worst);
        }

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

        // 番号を groups 個のグループに分け、今回のグループの敵の位置を送る(groups回で全員に1回ずつ届く)。
        private void SendCorrections()
        {
            var groups = CorrectionGroups;
            var phase = _correctionPhase++ % groups;
            var storage = _swarm.Storage;

            _correctionSequence++;
            _correctionBuffer.Clear();
            for (var i = 0; i < storage.Count; i++)
            {
                var id = storage.netId[i];
                if (id % groups != phase)
                {
                    continue;
                }

                var position = storage.pos[i];
                _correctionBuffer.Add(new SwarmNetCorrection
                {
                    Id = (ushort)id,
                    X = SwarmNetQuantize.ToShort(position.x),
                    Y = SwarmNetQuantize.ToShort(position.y)
                });

                if (_correctionBuffer.Count >= CorrectionsPerMessage)
                {
                    SendCorrections(_correctionSequence, _correctionBuffer.ToArray());
                    _correctionBuffer.Clear();
                }
            }

            if (_correctionBuffer.Count > 0)
            {
                SendCorrections(_correctionSequence, _correctionBuffer.ToArray());
            }
        }

        private void FlushEvents(int budget)
        {
            if (!HasRemoteClients())
            {
                _queue.Clear();
                return;
            }

            var events = _queue.Take(budget);
            if (events == null)
            {
                return;
            }

            for (var start = 0; start < events.Length; start += EventsPerMessage)
            {
                SendEvents(Slice(events, start, EventsPerMessage));
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

                var end = Mathf.Min(transfer.Events.Length, transfer.Sent + budget);
                var target = RpcTarget.Single(transfer.ClientId, RpcTargetUse.Temp);

                // 0体でも「最後」を1回送って、受け取り終わったことを伝える。
                do
                {
                    var count = Mathf.Min(EventsPerMessage, end - transfer.Sent);
                    var chunk = Slice(transfer.Events, transfer.Sent, count);
                    transfer.Sent += count;
                    FullStateRpc(_types.LayoutHash, chunk, transfer.Sent >= transfer.Events.Length, target);
                    NetTrafficStats.SwarmEventsSent += chunk.Length;
                    NetTrafficStats.Sent((long)chunk.Length * EventSize);
                }
                while (transfer.Sent < end);

                if (transfer.Sent >= transfer.Events.Length)
                {
                    _transfers.RemoveAt(t);
                }
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

        private static SwarmNetEvent[] Slice(SwarmNetEvent[] source, int start, int count)
        {
            count = Mathf.Max(0, Mathf.Min(count, source.Length - start));
            var result = new SwarmNetEvent[count];
            System.Array.Copy(source, start, result, 0, count);
            return result;
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReportCorrectionErrorRpc(float averageError, RpcParams rpcParams = default)
        {
            _reportedErrors[rpcParams.Receive.SenderClientId] = (averageError, Time.unscaledTime);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            if (_swarm != null)
            {
                _fullStateRequests.Add(rpcParams.Receive.SenderClientId);
            }
        }

        // ------------------------------------------------------------------ Client

        [Rpc(SendTo.SpecifiedInParams)]
        private void FullStateRpc(uint layoutHash, SwarmNetEvent[] spawns, bool isLast, RpcParams rpcParams = default)
        {
            if (_swarm == null || _layoutMismatch)
            {
                return;
            }

            NetTrafficStats.SwarmEventsReceived += spawns.Length;
            NetTrafficStats.Received((long)spawns.Length * EventSize);

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

            foreach (var spawn in spawns)
            {
                Apply(spawn);
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

        [Rpc(SendTo.NotServer)]
        private void EventsRpc(SwarmNetEvent[] events)
        {
            if (_swarm == null || _layoutMismatch)
            {
                return;
            }

            NetTrafficStats.SwarmEventsReceived += events.Length;
            NetTrafficStats.Received((long)events.Length * EventSize);

            // 全体の状態を受け取り終わるまでは貯めておき、受け取り終わってから順に反映する。
            if (!_hasFullState)
            {
                _bufferedEvents.AddRange(events);
                NetTrafficStats.SwarmBufferedEvents = _bufferedEvents.Count;
                return;
            }

            foreach (var e in events)
            {
                Apply(e);
            }
        }

        // 取りこぼしても次の周回で届くのでUnreliable。入れ替わって届いた古い回は捨てる(同じ回の分割分は同じ連番)。
        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void CorrectionsRpc(uint sequence, SwarmNetCorrection[] corrections)
        {
            if (_swarm == null || !_hasFullState || _layoutMismatch || (int)(sequence - _lastCorrectionSequence) < 0)
            {
                return;
            }

            _lastCorrectionSequence = sequence;
            NetTrafficStats.SwarmCorrectionsReceived += corrections.Length;
            NetTrafficStats.Received((long)corrections.Length * CorrectionSize);
            foreach (var c in corrections)
            {
                if (_aliveIds.Contains(c.Id))
                {
                    _swarm.QueueCorrection(c.Id, new Vector2(SwarmNetQuantize.FromShort(c.X), SwarmNetQuantize.FromShort(c.Y)));
                }
            }
        }

        // Client: Hostの位置が届いたときに測ったズレの平均を、一定間隔でHostへ伝える(補正の間隔の自動調整に使う)。
        private void ReportErrorPeriodically()
        {
            var stats = _swarm.Stats;
            _errorCount += stats.replicaCorrections;
            _errorSum += stats.replicaErrorSum;

            _errorReportTimer += Time.unscaledDeltaTime;
            if (_errorReportTimer < ErrorReportInterval)
            {
                return;
            }

            _errorReportTimer = 0f;
            if (_errorCount > 0 && _hasFullState)
            {
                ReportCorrectionErrorRpc(_errorSum / _errorCount);
            }

            _errorCount = 0;
            _errorSum = 0f;
        }

        private void Apply(SwarmNetEvent e)
        {
            if (e.Kind == SwarmNetEventKind.Spawn)
            {
                // 全体の状態と、その前後の出現が重なって届くことがあるので、知っている番号は無視する。
                if (!_aliveIds.Add(e.Id))
                {
                    return;
                }

                var type = _types.Get(e.TypeOrReason);
                var spawned = type != null && _swarm.SpawnReplica(
                    e.Id,
                    type,
                    new Vector2(SwarmNetQuantize.FromShort(e.X), SwarmNetQuantize.FromShort(e.Y)),
                    SwarmNetQuantize.FromHalf(e.SpeedScale),
                    SwarmNetQuantize.FromHalf(e.AnimStart),
                    SwarmNetQuantize.FromAngleByte(e.Facing));
                if (!spawned)
                {
                    _aliveIds.Remove(e.Id);
                }
            }
            else if (e.Kind == SwarmNetEventKind.Despawn && _aliveIds.Remove(e.Id))
            {
                _swarm.RemoveReplica(e.Id, (EnemyRemovalReason)e.TypeOrReason);
            }
        }
    }
}
