using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Actorモードの敵(1体ごとにGameObjectを持つ敵。ボスなど)をネット対戦で同期する。
    /// 他の同期部品と同じ「[Fortress] NetSync」に付ける(メニュー「Tools/要塞/ネットワーク/同期オブジェクトをシーンに配置」)。
    ///
    /// ・Host: 敵は今まで通りHostだけが湧かせて動かす。出現/消滅を起きた順に、位置と速度を毎秒<see cref="positionSendRate"/>回配る。
    /// ・Client: 同じ種類・同じPrefabから「表示だけの敵(レプリカ)」を作り、届いた位置へ滑らかに動かす。
    ///   倒されたときはOnDeathも発火するので、倒れる演出などは全員の画面で出る。
    /// NetworkObjectは使わない(敵のPrefabごとに登録する手間を無くすため)。種類は<see cref="EnemyTypeNetIndex"/>の番号で伝える。
    /// 群衆(Swarm)の敵はここでは扱わない(Phase 4)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyNetworkHub : NetworkBehaviour
    {
        [Tooltip("出現・消滅をまとめて送る回数(回/秒)。")]
        [Min(1f)]
        public float eventSendRate = 20f;

        [Tooltip("敵の位置を送る回数(回/秒)。")]
        [Min(1f)]
        public float positionSendRate = 20f;

        // 1回の送信に詰める敵の数。取りこぼしてよい送信は分割されず1回1296バイトが上限(NetMessageLimits、20バイト×60体)。
        private static readonly int PositionsPerMessage = NetMessageLimits.UnreliableItemsPerMessage<EnemyNetPosition>();

        private readonly EnemyTypeNetIndex _types = new();
        private readonly EnemyNetTracker<EnemyController> _tracker = new();
        private readonly List<EnemyNetPosition> _positionBuffer = new();
        private EnemyNetTracker<EnemyController>.Describe _describe;
        private float _eventCooldown;
        private float _positionCooldown;
        private uint _positionSequence;
        private bool _unsupportedLogged;

        // Client側
        private readonly Dictionary<uint, EnemyController> _replicas = new();
        private GameObject _fallbackPrefab;
        private bool _hasFullState;
        private bool _layoutMismatch;
        private uint _lastPositionSequence;

        private void Awake()
        {
            _describe = DescribeEnemy;
        }

        public override void OnNetworkSpawn()
        {
            _types.Build();
            var director = FindFirstObjectByType<EnemySpawnDirector>();
            _fallbackPrefab = director != null ? director.fallbackEnemyPrefab : null;

            if (IsServer)
            {
                _tracker.Clear();
                EnemyController.Registered += OnEnemyRegistered;
                EnemyController.Unregistered += OnEnemyUnregistered;

                // 通信を始める前から居た敵も、Clientに伝える。
                foreach (var enemy in EnemyController.Active)
                {
                    _tracker.OnAdded(enemy);
                }

                return;
            }

            // Clientになる前に自分で湧かせていた敵はHostの敵と二重になるので消す(以後はHostから届く敵だけを表示する)。
            foreach (var enemy in new List<EnemyController>(EnemyController.Active))
            {
                if (!enemy.IsReplica)
                {
                    Destroy(enemy.gameObject);
                }
            }

            _hasFullState = false;
            _layoutMismatch = false;
            _lastPositionSequence = 0;
            RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            EnemyController.Registered -= OnEnemyRegistered;
            EnemyController.Unregistered -= OnEnemyUnregistered;
            _tracker.Clear();

            // 切断したClientでは、Hostの敵の表示はもう更新されないので消す。
            foreach (var replica in _replicas.Values)
            {
                if (replica != null)
                {
                    Destroy(replica.gameObject);
                }
            }

            _replicas.Clear();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            _eventCooldown -= Time.unscaledDeltaTime;
            if (_eventCooldown <= 0f)
            {
                _eventCooldown = 1f / eventSendRate;
                FlushEvents();
            }

            _positionCooldown -= Time.unscaledDeltaTime;
            if (_positionCooldown <= 0f)
            {
                _positionCooldown = 1f / positionSendRate;
                SendPositions();
            }
        }

        // ------------------------------------------------------------------ Host

        private void OnEnemyRegistered(EnemyController enemy)
        {
            if (!enemy.IsReplica)
            {
                _tracker.OnAdded(enemy);
            }
        }

        private void OnEnemyUnregistered(EnemyController enemy)
        {
            if (!enemy.IsReplica)
            {
                _tracker.OnRemoved(enemy, (byte)enemy.RemovalReason);
            }
        }

        private bool DescribeEnemy(EnemyController enemy, out ushort typeIndex, out float x, out float y)
        {
            typeIndex = 0;
            x = y = 0f;
            if (enemy == null || !_types.TryGetIndex(enemy.definition, out var index))
            {
                return false;
            }

            typeIndex = (ushort)index;
            var position = enemy.transform.position;
            x = position.x;
            y = position.y;
            return true;
        }

        private void FlushEvents()
        {
            var unsupported = _tracker.ResolvePending(_describe);
            if (unsupported > 0 && !_unsupportedLogged)
            {
                _unsupportedLogged = true;
                Debug.LogWarning("[Net] EnemyNetworkHub: ウェーブ設定に入っていない種類の敵がいるため、Clientには表示されません" +
                                 "(ウェーブに入っていない種類は、ビルドに含まれない場合があり番号を振れません)。");
            }

            if (!HasRemoteClients())
            {
                _tracker.DiscardEvents();
                return;
            }

            var events = _tracker.Drain();
            if (events != null)
            {
                ApplyEventsRpc(events);
            }
        }

        private void SendPositions()
        {
            if (!HasRemoteClients() || _tracker.Ids.Count == 0)
            {
                return;
            }

            _positionSequence++;
            _positionBuffer.Clear();
            foreach (var pair in _tracker.Ids)
            {
                var enemy = pair.Key;
                if (enemy == null)
                {
                    continue;
                }

                var position = enemy.transform.position;
                var velocity = enemy.Velocity;
                _positionBuffer.Add(new EnemyNetPosition { Id = pair.Value, X = position.x, Y = position.y, VelocityX = velocity.x, VelocityY = velocity.y });

                if (_positionBuffer.Count >= PositionsPerMessage)
                {
                    PositionsRpc(_positionSequence, _positionBuffer.ToArray());
                    _positionBuffer.Clear();
                }
            }

            if (_positionBuffer.Count > 0)
            {
                PositionsRpc(_positionSequence, _positionBuffer.ToArray());
            }
        }

        private bool HasRemoteClients()
        {
            return NetworkManager != null && NetworkManager.ConnectedClientsIds.Count > 1;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            // 保留中の敵にも番号を振ってから、今いる全員を「出現」として送る。
            _tracker.ResolvePending(_describe);

            var spawns = new List<EnemyNetEvent>(_tracker.Ids.Count);
            foreach (var pair in _tracker.Ids)
            {
                if (pair.Key != null && DescribeEnemy(pair.Key, out var typeIndex, out var x, out var y))
                {
                    spawns.Add(new EnemyNetEvent { Id = pair.Value, Kind = EnemyNetEventKind.Spawn, TypeIndex = typeIndex, X = x, Y = y });
                }
            }

            FullStateRpc(_types.LayoutHash, spawns.ToArray(), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        // ------------------------------------------------------------------ Client

        [Rpc(SendTo.SpecifiedInParams)]
        private void FullStateRpc(uint layoutHash, EnemyNetEvent[] spawns, RpcParams rpcParams = default)
        {
            if (layoutHash != _types.LayoutHash)
            {
                _layoutMismatch = true;
                Debug.LogError("[Net] EnemyNetworkHub: 敵の種類の並びがHostと一致しません。HostとClientが同じビルドか確認してください。Actorの敵は表示されません。");
                return;
            }

            foreach (var spawn in spawns)
            {
                CreateReplica(spawn);
            }

            _hasFullState = true;
        }

        [Rpc(SendTo.NotServer)]
        private void ApplyEventsRpc(EnemyNetEvent[] events)
        {
            // 全体の状態より前の変化は、全体の状態に含まれているので捨てる。
            if (!_hasFullState || _layoutMismatch)
            {
                return;
            }

            foreach (var e in events)
            {
                if (e.Kind == EnemyNetEventKind.Spawn)
                {
                    CreateReplica(e);
                }
                else if (_replicas.TryGetValue(e.Id, out var replica))
                {
                    _replicas.Remove(e.Id);
                    if (replica != null)
                    {
                        replica.ApplyReplicatedRemoval((EnemyRemovalReason)e.Reason);
                    }
                }
            }
        }

        // 位置は取りこぼしても次がすぐ届くのでUnreliable。入れ替わって届いた古い位置は連番で捨てる(同じ回の分割分は同じ連番)。
        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void PositionsRpc(uint sequence, EnemyNetPosition[] positions)
        {
            if (!_hasFullState || _layoutMismatch || (int)(sequence - _lastPositionSequence) < 0)
            {
                return;
            }

            _lastPositionSequence = sequence;
            foreach (var p in positions)
            {
                if (_replicas.TryGetValue(p.Id, out var replica) && replica != null)
                {
                    replica.ApplyReplicatedState(new Vector2(p.X, p.Y), new Vector2(p.VelocityX, p.VelocityY), snap: false);
                }
            }
        }

        private void CreateReplica(EnemyNetEvent spawn)
        {
            if (_replicas.ContainsKey(spawn.Id))
            {
                return;
            }

            var type = _types.Get(spawn.TypeIndex);
            if (type == null)
            {
                return;
            }

            var position = new Vector3(spawn.X, spawn.Y, 0f);
            var enemy = EnemyActorFactory.Create(type, position, _fallbackPrefab);
            enemy.BeginReplica();
            enemy.ApplyReplicatedState(position, Vector2.zero, snap: true);
            _replicas[spawn.Id] = enemy;
        }
    }
}
