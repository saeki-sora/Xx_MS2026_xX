using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 破壊可能物(スマッシュボールを含む)をネット対戦で同期する。<see cref="TurretNetworkHub"/>と同じ
    /// 「[Fortress] NetSync」に付ける(メニュー「Tools/要塞/ネットワーク/同期オブジェクトをシーンに配置」)。
    ///
    /// ・Host: 今まで通り計算する。各物のイベント(ダメージ・耐久・破壊・再生・無敵・表示ON/OFF)を起きた順に貯め、
    ///   毎秒<see cref="eventSendRate"/>回まとめて全Clientへ送る。浮遊中のスマッシュボールの位置も配る。
    /// ・Client: 全ての物をレプリカ(自分では状態を変えない)にし、届いた変化を同じイベントとして発火する。
    ///   見た目・演出・ドロップ・スマッシュボールの記録は購読しているだけなので、そのまま全員の画面で動く。
    ///   接続直後に一度だけHostへ「今の状態」を問い合わせて合わせる(途中参加・接続前に壊れた物への対応)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleNetworkHub : NetworkBehaviour
    {
        [Tooltip("破壊・ダメージなどの変化をまとめて送る回数(回/秒)。多いほど反映が速いが通信が増える。")]
        [Min(1f)]
        public float eventSendRate = 20f;

        [Tooltip("浮遊中のスマッシュボールの位置を送る回数(回/秒)。")]
        [Min(1f)]
        public float positionSendRate = 15f;

        private const int PlayerCount = 4;

        private readonly DestructibleNetIndex _index = new();
        private readonly DestructibleEventQueue _queue = new();
        private readonly List<Action> _unsubscribers = new();
        private readonly List<DestructibleNetPosition> _positionBuffer = new();
        private readonly LaserTurret[] _turrets = new LaserTurret[PlayerCount];

        private SmashBallFloater[] _floaters = Array.Empty<SmashBallFloater>();
        private bool[] _lastActive = Array.Empty<bool>();
        private float _eventCooldown;
        private float _positionCooldown;
        private uint _positionSequence;

        // Client側
        private bool _hasFullState;
        private bool _layoutMismatch;
        private uint _lastPositionSequence;

        public override void OnNetworkSpawn()
        {
            _index.Build();
            CollectTurrets();

            _floaters = new SmashBallFloater[_index.Count];
            _lastActive = new bool[_index.Count];
            for (var i = 0; i < _index.Count; i++)
            {
                var obstacle = _index.Get(i);
                _floaters[i] = obstacle.GetComponent<SmashBallFloater>();
                _lastActive[i] = obstacle.gameObject.activeSelf;
            }

            if (IsServer)
            {
                SubscribeHostEvents();
                return;
            }

            _hasFullState = false;
            _layoutMismatch = false;
            _lastPositionSequence = 0;
            foreach (var obstacle in _index.Items)
            {
                obstacle.BeginReplica();
            }

            RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            foreach (var unsubscribe in _unsubscribers)
            {
                unsubscribe();
            }

            _unsubscribers.Clear();
            _queue.Clear();

            foreach (var obstacle in _index.Items)
            {
                if (obstacle != null && obstacle.IsReplica)
                {
                    obstacle.EndReplica();
                }
            }
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
                SendFloatingPositions();
            }
        }

        // ------------------------------------------------------------------ Host

        private void SubscribeHostEvents()
        {
            _unsubscribers.Clear();
            for (var i = 0; i < _index.Count; i++)
            {
                var index = i;
                var obstacle = _index.Get(i);

                Action<DestructibleObstacle, float, DamageSource> onDamaged = (o, amount, source) =>
                    _queue.OnDamaged(index, amount, (byte)source, ToNet(o.LastAttacker), o.CurrentHealth);
                Action<DestructibleObstacle> onHealthChanged = o => _queue.OnHealthChanged(index, o.CurrentHealth);
                Action<DestructibleObstacle> onDestroyed = o => _queue.OnDestroyed(index, (byte)o.LastDestroySource, ToNet(o.DestroyedBy));
                Action<DestructibleObstacle> onRegenerated = _ => _queue.OnRegenerated(index);
                Action<DestructibleObstacle> onInvulnerability = o => _queue.OnInvulnerabilityChanged(index, o.IsInvulnerable);

                obstacle.Damaged += onDamaged;
                obstacle.HealthChanged += onHealthChanged;
                obstacle.Destroyed += onDestroyed;
                obstacle.Regenerated += onRegenerated;
                obstacle.InvulnerabilityChanged += onInvulnerability;

                _unsubscribers.Add(() =>
                {
                    if (obstacle == null)
                    {
                        return;
                    }

                    obstacle.Damaged -= onDamaged;
                    obstacle.HealthChanged -= onHealthChanged;
                    obstacle.Destroyed -= onDestroyed;
                    obstacle.Regenerated -= onRegenerated;
                    obstacle.InvulnerabilityChanged -= onInvulnerability;
                });
            }
        }

        private void FlushEvents()
        {
            // 表示ON/OFFはイベントが無いので、送る直前に変化を見つける(スマッシュボールのローテーション等)。
            for (var i = 0; i < _index.Count; i++)
            {
                var obstacle = _index.Get(i);
                if (obstacle == null)
                {
                    continue;
                }

                var active = obstacle.gameObject.activeSelf;
                if (active != _lastActive[i])
                {
                    _lastActive[i] = active;
                    _queue.OnActiveChanged(i, active);
                }
            }

            if (!HasRemoteClients())
            {
                _queue.Clear();
                return;
            }

            var events = _queue.Drain();
            if (events != null)
            {
                ApplyEventsRpc(events);
            }
        }

        private void SendFloatingPositions()
        {
            if (!HasRemoteClients())
            {
                return;
            }

            _positionBuffer.Clear();
            for (var i = 0; i < _floaters.Length; i++)
            {
                var floater = _floaters[i];
                if (floater != null && floater.IsFloating)
                {
                    var position = floater.transform.position;
                    _positionBuffer.Add(new DestructibleNetPosition { Index = (ushort)i, X = position.x, Y = position.y });
                }
            }

            if (_positionBuffer.Count == 0)
            {
                return;
            }

            // 取りこぼしてよい送信は1回1296バイトが上限(NetMessageLimits)。同じ回の分割分は同じ連番にする。
            _positionSequence++;
            var perMessage = NetMessageLimits.UnreliableItemsPerMessage<DestructibleNetPosition>();
            for (var start = 0; start < _positionBuffer.Count; start += perMessage)
            {
                var count = Mathf.Min(perMessage, _positionBuffer.Count - start);
                FloatingPositionsRpc(_positionSequence, _positionBuffer.GetRange(start, count).ToArray());
            }
        }

        private bool HasRemoteClients()
        {
            return NetworkManager != null && NetworkManager.ConnectedClientsIds.Count > 1;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            var states = new DestructibleNetState[_index.Count];
            for (var i = 0; i < _index.Count; i++)
            {
                var obstacle = _index.Get(i);
                if (obstacle == null)
                {
                    continue;
                }

                states[i] = new DestructibleNetState
                {
                    Health = obstacle.CurrentHealth,
                    RegenProgress01 = obstacle.RegenProgress01,
                    DestroyCount = obstacle.DestroyCount,
                    DestroyedBy = ToNet(obstacle.DestroyedBy),
                    LastAttacker = ToNet(obstacle.LastAttacker),
                    IsDestroyed = obstacle.IsDestroyed,
                    IsInvulnerable = obstacle.IsInvulnerable,
                    IsActive = obstacle.gameObject.activeSelf
                };
            }

            FullStateRpc(_index.LayoutHash, states, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        // ------------------------------------------------------------------ Client

        [Rpc(SendTo.SpecifiedInParams)]
        private void FullStateRpc(uint layoutHash, DestructibleNetState[] states, RpcParams rpcParams = default)
        {
            if (layoutHash != _index.LayoutHash || states.Length != _index.Count)
            {
                // 番号の付け方がHostと違う(シーンの中身が違うビルド等)。違う物に反映してしまうので同期をやめ、自分で動かす。
                _layoutMismatch = true;
                Debug.LogError($"[Net] DestructibleNetworkHub: 破壊可能物の並びがHostと一致しません(Host {states.Length}個 / 自分 {_index.Count}個)。" +
                               "HostとClientが同じビルドか確認してください。破壊可能物は同期しません。");
                foreach (var obstacle in _index.Items)
                {
                    obstacle.EndReplica();
                }

                return;
            }

            for (var i = 0; i < states.Length; i++)
            {
                var obstacle = _index.Get(i);
                if (obstacle == null)
                {
                    continue;
                }

                var state = states[i];

                // 非表示の物はイベントの購読者(見た目など)が止まっているので、表示してから反映し、隠すのは反映の後にする。
                if (state.IsActive)
                {
                    obstacle.gameObject.SetActive(true);
                }

                obstacle.ApplyReplicatedSnapshot(state.Health, state.IsDestroyed, state.IsInvulnerable, state.DestroyCount,
                    ToAttacker(state.DestroyedBy), ToAttacker(state.LastAttacker), state.RegenProgress01);

                if (!state.IsActive)
                {
                    obstacle.gameObject.SetActive(false);
                }
            }

            _hasFullState = true;
        }

        [Rpc(SendTo.NotServer)]
        private void ApplyEventsRpc(DestructibleNetEvent[] events)
        {
            // 全体の状態を受け取る前の変化は、全体の状態に含まれているので捨てる。
            if (!_hasFullState || _layoutMismatch)
            {
                return;
            }

            foreach (var e in events)
            {
                var obstacle = _index.Get(e.Index);
                if (obstacle == null)
                {
                    continue;
                }

                var source = (DamageSource)e.Source;
                switch (e.Kind)
                {
                    case DestructibleNetEventKind.Damage:
                        obstacle.ApplyReplicatedDamage(e.Health, e.Amount, source, ToAttacker(e.Attacker));
                        break;
                    case DestructibleNetEventKind.Health:
                        obstacle.ApplyReplicatedHealth(e.Health);
                        break;
                    case DestructibleNetEventKind.Destroy:
                        obstacle.ApplyReplicatedDestroy(source, ToAttacker(e.Attacker));
                        break;
                    case DestructibleNetEventKind.Regenerate:
                        obstacle.ApplyReplicatedRegenerate();
                        break;
                    case DestructibleNetEventKind.Invulnerable:
                        obstacle.ApplyReplicatedInvulnerable(e.Flag);
                        break;
                    case DestructibleNetEventKind.Active:
                        obstacle.gameObject.SetActive(e.Flag);
                        break;
                }
            }
        }

        // 位置は取りこぼしても次がすぐ届くのでUnreliable。入れ替わって届いた古い位置は連番で捨てる。
        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void FloatingPositionsRpc(uint sequence, DestructibleNetPosition[] positions)
        {
            if (!_hasFullState || _layoutMismatch || (int)(sequence - _lastPositionSequence) < 0)
            {
                return;
            }

            _lastPositionSequence = sequence;
            foreach (var p in positions)
            {
                if (p.Index < _floaters.Length && _floaters[p.Index] != null)
                {
                    _floaters[p.Index].ApplyReplicatedPosition(new Vector2(p.X, p.Y));
                }
            }
        }

        // ------------------------------------------------------------------ 共通

        private static sbyte ToNet(DestructibleAttacker attacker)
        {
            return attacker.HasPlayer ? (sbyte)attacker.PlayerIndex : (sbyte)-1;
        }

        // Componentの参照は送れないので、プレイヤー番号からその人の砲台を引いて組み立て直す。
        private DestructibleAttacker ToAttacker(sbyte playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= PlayerCount)
            {
                return DestructibleAttacker.None;
            }

            return new DestructibleAttacker(playerIndex, _turrets[playerIndex]);
        }

        private void CollectTurrets()
        {
            Array.Clear(_turrets, 0, _turrets.Length);
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret.playerIndex >= 0 && turret.playerIndex < PlayerCount && _turrets[turret.playerIndex] == null)
                {
                    _turrets[turret.playerIndex] = turret;
                }
            }
        }
    }
}
