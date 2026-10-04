using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 4台の砲台(<see cref="LaserTurret"/>)をネット対戦で同期する。シーンに1つだけ、NetworkObjectと一緒に置く
    /// (メニュー「Tools/要塞/ネットワーク/同期オブジェクトをシーンに配置」で作れる)。
    ///
    /// ・Client: 自分のPCのセンサーの握力をHostへ送るだけ。砲台は計算せず、Hostから届いた状態を表示する。
    /// ・Host: 自分の握力はローカルのセンサーから、他人の握力はClientから届いた値で4台とも計算し、結果を全員へ配る。
    /// ・通信していないとき: 何もしない(砲台は今まで通りGripInputBridgeを直接読む)。
    /// 握力の送り主は接続承認で決まったプレイヤー番号から引くので、Clientが他人の砲台を動かすことはできない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurretNetworkHub : NetworkBehaviour, ITurretGripSource
    {
        private const int PlayerCount = 4;

        [Tooltip("ネット対戦中、自分の砲台を動かすセンサーの番号。各PCにセンサーが1台なら、そのPCではP1(=0)として届くので0のままでよい。キーボードならQ。")]
        [Range(0, 3)]
        public int localGripSensorIndex;

        [Tooltip("Clientが握力をHostへ送る回数(回/秒)。握り始め・離した瞬間はこれとは別にすぐ送る。")]
        [Min(1f)]
        public float gripSendRate = 30f;

        [Tooltip("この秒数Clientから握力が届かなければ、離したとみなす(切断・フリーズ時に撃ちっぱなしになるのを防ぐ)。")]
        [Min(0.1f)]
        public float remoteGripTimeoutSeconds = 0.5f;

        // NGOはNetworkVariableを「フィールド」としてしか見つけないため、配列ではなく4つ並べる。
        private readonly NetworkVariable<TurretSnapshot> _turret0 = new();
        private readonly NetworkVariable<TurretSnapshot> _turret1 = new();
        private readonly NetworkVariable<TurretSnapshot> _turret2 = new();
        private readonly NetworkVariable<TurretSnapshot> _turret3 = new();

        private readonly LaserTurret[] _turrets = new LaserTurret[PlayerCount];
        private readonly RemoteGripBuffer _remoteGrips = new(PlayerCount);
        private NetworkVariable<TurretSnapshot>[] _states;
        private NetworkVariable<TurretSnapshot>.OnValueChangedDelegate[] _replicaHandlers;

        private float _sendCooldown;
        private bool _lastSentGripping;
        private uint _sendSequence;

        private void Awake()
        {
            _states = new[] { _turret0, _turret1, _turret2, _turret3 };
            _replicaHandlers = new NetworkVariable<TurretSnapshot>.OnValueChangedDelegate[PlayerCount];
            for (var i = 0; i < PlayerCount; i++)
            {
                var index = i;
                _replicaHandlers[i] = (_, current) => ApplyToReplica(index, current);
            }
        }

        public override void OnNetworkSpawn()
        {
            CollectTurrets();
            _remoteGrips.ClearAll();

            for (var i = 0; i < PlayerCount; i++)
            {
                var turret = _turrets[i];
                if (turret == null)
                {
                    continue;
                }

                if (IsServer)
                {
                    turret.GripSourceOverride = this;
                }
                else
                {
                    turret.BeginReplica();
                    turret.ApplySnapshot(_states[i].Value);
                    _states[i].OnValueChanged += _replicaHandlers[i];
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                _states[i].OnValueChanged -= _replicaHandlers[i];

                var turret = _turrets[i];
                if (turret == null)
                {
                    continue;
                }

                if (ReferenceEquals(turret.GripSourceOverride, this))
                {
                    turret.GripSourceOverride = null;
                }

                if (turret.IsReplica)
                {
                    turret.EndReplica();
                }
            }

            _remoteGrips.ClearAll();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                PublishTurretStates();
            }
            else
            {
                SendLocalGrip();
            }
        }

        // LaserTurret([DefaultExecutionOrder(-10)])の計算後に呼ばれる。実際の送信はネットワークのTick(既定30Hz)ごとで、変化がなければ送られない。
        private void PublishTurretStates()
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                if (_turrets[i] != null)
                {
                    _states[i].Value = _turrets[i].CaptureSnapshot();
                }
            }
        }

        private void SendLocalGrip()
        {
            var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;
            var isGripping = provider.IsGripping(localGripSensorIndex);
            var grip = provider.GetGripValue(localGripSensorIndex);

            _sendCooldown -= Time.unscaledDeltaTime;
            if (_sendCooldown > 0f && isGripping == _lastSentGripping)
            {
                return;
            }

            _sendCooldown = 1f / gripSendRate;
            _lastSentGripping = isGripping;
            _sendSequence++;
            SubmitGripRpc(isGripping, grip, _sendSequence);
        }

        // 取りこぼしても次の値がすぐ届くのでUnreliable(再送なし)で送る。古い値はRemoteGripBufferが連番で捨てる。
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, RequireOwnership = false)]
        private void SubmitGripRpc(bool isGripping, float grip01, uint sequence, RpcParams rpcParams = default)
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null || !bootstrap.ClientIdToPlayerIndex.TryGetValue(rpcParams.Receive.SenderClientId, out var playerIndex))
            {
                return;
            }

            _remoteGrips.Submit(playerIndex, isGripping, grip01, sequence, Time.unscaledTimeAsDouble, remoteGripTimeoutSeconds);
        }

        /// <summary>Host側でLaserTurretから呼ばれる。自分の分はローカルのセンサー、他人の分はClientから届いた値を返す。</summary>
        public void GetGrip(int playerIndex, out bool isGripping, out float grip01)
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap != null && playerIndex == bootstrap.LocalPlayerIndex)
            {
                var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;
                isGripping = provider.IsGripping(localGripSensorIndex);
                grip01 = provider.GetGripValue(localGripSensorIndex);
                return;
            }

            _remoteGrips.Get(playerIndex, Time.unscaledTimeAsDouble, remoteGripTimeoutSeconds, out isGripping, out grip01);
        }

        private void ApplyToReplica(int playerIndex, TurretSnapshot snapshot)
        {
            var turret = _turrets[playerIndex];
            if (turret != null && turret.IsReplica)
            {
                turret.ApplySnapshot(snapshot);
            }
        }

        private void CollectTurrets()
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                _turrets[i] = null;
            }

            var found = FindObjectsByType<LaserTurret>(FindObjectsSortMode.None);
            foreach (var turret in found)
            {
                var index = turret.playerIndex;
                if (index < 0 || index >= PlayerCount)
                {
                    continue;
                }

                if (_turrets[index] != null)
                {
                    Debug.LogWarning($"[Net] TurretNetworkHub: プレイヤー{index + 1}の砲台が複数あります({_turrets[index].name}, {turret.name})。先に見つかった方だけ同期します。", turret);
                    continue;
                }

                _turrets[index] = turret;
            }
        }
    }
}
