using System.Collections.Generic;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 盾持ちの盾（<see cref="EnemyShieldSettings"/>）を出す（<see cref="SwarmSystem"/> が Play 開始時に自動で付ける）。
    /// ・盾持ち（<see cref="SwarmSystem.ShieldBearers"/>）ごとに <see cref="EnemyShieldView"/> を置き、毎フレーム位置を合わせる（全PC）
    /// ・Host・オフライン: 最近いちばん当ててきている人（いなければ一番近い砲台）を選び、その砲台の方へゆっくり回す。向きは <see cref="EnemyShieldNet"/> で配る
    /// ・Client: 届いた向きへ回す
    /// ・盾持ちが倒されたら盾が割れる演出（<see cref="EnemyShieldSettings.onBreak"/>、空なら仮の破片）。コアに着いて消えたときは何も出さない
    /// 群衆の計算の後（実行順 110）に動くので、ビームの判定に使われるのは次のフレーム（1フレーム遅れ）。
    /// </summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SwarmSystem))]
    public sealed class EnemyShieldDirector : MonoBehaviour
    {
        private const float TurretScanInterval = 1f;
        private const float ClientCatchUp = 2f;
        private const float DeathMatchDistance = 1.5f;

        private struct Death
        {
            public Vector2 position;
            public int owner;
        }

        private readonly Dictionary<int, EnemyShieldView> _byId = new Dictionary<int, EnemyShieldView>();
        private readonly List<EnemyShieldView> _active = new List<EnemyShieldView>();
        private readonly Stack<EnemyShieldView> _pool = new Stack<EnemyShieldView>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<Death> _deaths = new List<Death>();
        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private readonly EnemyShieldNet _net = new EnemyShieldNet();
        private SwarmSystem _swarm;
        private Transform _root;
        private float _nextTurretScan;

        private void Awake()
        {
            _swarm = GetComponent<SwarmSystem>();
        }

        private void OnEnable()
        {
            if (_swarm != null)
            {
                _swarm.AgentsDied += OnAgentsDied;
            }
        }

        private void OnDisable()
        {
            if (_swarm != null)
            {
                _swarm.AgentsDied -= OnAgentsDied;
            }

            _net.Dispose();
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        private void Update()
        {
            var bearers = _swarm.ShieldBearers;
            if (bearers.Count == 0 && _active.Count == 0)
            {
                _deaths.Clear();
                _net.Tick(_active);
                return;
            }

            var dt = Time.deltaTime;
            var authority = FortressNet.HasSimulationAuthority;
            RefreshTurrets();
            _seen.Clear();
            for (var b = 0; b < bearers.Count; b++)
            {
                var bearer = bearers[b];
                var shield = bearer.Type.shield;
                _seen.Add(bearer.NetId);
                var view = GetView(bearer.NetId);
                view.Bind(shield);

                var focus = view.Focus;
                var angle = view.AngleDegrees;
                if (authority)
                {
                    var fallback = NearestTurret(bearer.Position);
                    focus = EnemyShieldMath.ChooseFocus(bearer.Threat, view.Initialized ? view.Focus : -1, shield.switchRatio, fallback);
                    var aim = AimAt(bearer.Position, focus);
                    angle = view.Initialized ? EnemyShieldMath.TurnToward(angle, aim, shield.turnDegreesPerSecond * dt) : aim;
                }
                else if (_net.TryGet(bearer.NetId, out var state))
                {
                    focus = state.focus;
                    angle = view.Initialized
                        ? EnemyShieldMath.TurnToward(angle, state.angleDegrees, shield.turnDegreesPerSecond * ClientCatchUp * dt)
                        : state.angleDegrees;
                }
                else if (!view.Initialized)
                {
                    angle = Mathf.Atan2(-bearer.Position.y, -bearer.Position.x) * Mathf.Rad2Deg;
                }

                if (view.Initialized && focus != view.Focus && focus >= 0 && !shield.onFocusChanged.IsEmpty)
                {
                    FortressEffectPlayer.PlayOnce(shield.onFocusChanged, bearer.Position, Quaternion.identity, focus);
                }

                view.Apply(bearer.Position, angle, focus);
                view.Initialized = true;
            }

            ReleaseMissing();
            Physics2D.SyncTransforms();
            _deaths.Clear();
            _net.Tick(_active);
        }

        private EnemyShieldView GetView(int netId)
        {
            if (_byId.TryGetValue(netId, out var view))
            {
                return view;
            }

            if (_root == null)
            {
                _root = new GameObject("[Fortress] EnemyShields").transform;
            }

            view = _pool.Count > 0 ? _pool.Pop() : EnemyShieldView.Create(_root);
            view.gameObject.SetActive(true);
            view.NetId = netId;
            view.Initialized = false;
            view.Focus = -1;
            view.name = "[Shield] " + netId;
            _byId.Add(netId, view);
            _active.Add(view);
            return view;
        }

        /// <summary>今フレームにいなかった盾持ちの盾を片付ける。倒されていたら割れる演出。</summary>
        private void ReleaseMissing()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var view = _active[i];
                if (_seen.Contains(view.NetId))
                {
                    continue;
                }

                Vector2 position = view.transform.position;
                if (TryTakeDeath(position, out var owner))
                {
                    PlayBreak(view, position, owner);
                }

                _active.RemoveAt(i);
                _byId.Remove(view.NetId);
                _net.Forget(view.NetId);
                view.gameObject.SetActive(false);
                _pool.Push(view);
            }
        }

        private void PlayBreak(EnemyShieldView view, Vector2 position, int owner)
        {
            var shield = view.Settings;
            if (shield == null)
            {
                return;
            }

            if (!shield.onBreak.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(shield.onBreak, position, view.transform.rotation, owner);
                return;
            }

            // 仮の破片（演出が空のとき）。盾の大きさの範囲に、盾の色で散らす。
            var size = new Vector2(shield.frontDistance + shield.halfWidth, shield.halfWidth * 2f);
            PlaceholderDebrisBurst.Spawn(position, size, 14, shield.color);
        }

        private bool TryTakeDeath(Vector2 position, out int owner)
        {
            owner = -1;
            var best = -1;
            var bestDistance = DeathMatchDistance * DeathMatchDistance;
            for (var i = 0; i < _deaths.Count; i++)
            {
                var d = (_deaths[i].position - position).sqrMagnitude;
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            if (best < 0)
            {
                return false;
            }

            owner = _deaths[best].owner;
            _deaths.RemoveAt(best);
            return true;
        }

        private void OnAgentsDied(IReadOnlyList<SwarmDeath> deaths)
        {
            var types = _swarm.RegisteredTypes;
            for (var i = 0; i < deaths.Count; i++)
            {
                var death = deaths[i];
                if (death.type < 0 || death.type >= types.Count)
                {
                    continue;
                }

                var shield = types[death.type].shield;
                if (shield != null && shield.hasShield)
                {
                    _deaths.Add(new Death { position = death.position, owner = death.owner });
                }
            }
        }

        private void RefreshTurrets()
        {
            if (Time.unscaledTime < _nextTurretScan && _turrets.Count > 0)
            {
                return;
            }

            _nextTurretScan = Time.unscaledTime + TurretScanInterval;
            _turrets.Clear();
            _turrets.AddRange(FindObjectsByType<LaserTurret>(FindObjectsSortMode.None));
        }

        /// <summary>一番近い砲台のプレイヤー番号（砲台が無ければ -1）。</summary>
        private int NearestTurret(Vector2 position)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            foreach (var turret in _turrets)
            {
                if (turret == null)
                {
                    continue;
                }

                var d = ((Vector2)turret.transform.position - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = turret.playerIndex;
                }
            }

            return best;
        }

        /// <summary>その人の砲台への向き（度）。分からなければコアの方（原点）。</summary>
        private float AimAt(Vector2 from, int player)
        {
            Vector2 target = Vector2.zero;
            foreach (var turret in _turrets)
            {
                if (turret != null && turret.playerIndex == player)
                {
                    target = turret.transform.position;
                    break;
                }
            }

            var direction = target - from;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }
}
