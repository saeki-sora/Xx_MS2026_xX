using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>群衆の中の盾持ち1体分（<see cref="SwarmSystem.ShieldBearers"/>。盾の見た目・向きは <see cref="EnemyShieldDirector"/>）。</summary>
    public sealed class SwarmShieldBearer
    {
        public int NetId;
        public EnemyTypeDefinition Type;
        public Vector2 Position;
        public Vector2 Velocity;

        /// <summary>進む向き（ラジアン、なめらかにしたもの）。お供はこの向きを基準に並ぶ。Host・オフラインのみ。</summary>
        public float TravelAngle;

        /// <summary>プレイヤーごとの「最近当ててきた量」（秒ぶん。時間で薄れる）。Host・オフラインのみ。</summary>
        public readonly float[] Threat = new float[4];

        internal int SeenFrame;
    }

    /// <summary>
    /// 盾持ちの敵（<see cref="EnemyShieldSettings"/>、2026-10-29）の群衆側。
    /// ・湧かせるとき、お供も一緒に湧かせる（<see cref="Spawn"/>）
    /// ・毎フレーム、盾持ちを探して <see cref="ShieldBearers"/> にまとめる（全PC）
    /// ・Host: 盾持ちの近くを通ったビーム（スキル含む。<see cref="QueueBeam"/> に積まれた物）を撃った人ごとに数える（にらむ相手を決める元）
    /// ・Host: お供の行き先（盾持ちのまわりの決まった場所）を決め、進む向きの計算（<see cref="SwarmSteerJob"/>）に渡す
    /// 盾そのもの（当たり判定・見た目・向き・通信）は <see cref="EnemyShieldDirector"/>。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        private struct EscortRef
        {
            public int index;
            public int netId;
            public float2 position;
            public int type;
        }

        private readonly List<SwarmShieldBearer> _bearers = new List<SwarmShieldBearer>();
        private readonly Dictionary<int, SwarmShieldBearer> _bearerById = new Dictionary<int, SwarmShieldBearer>();
        private readonly Stack<SwarmShieldBearer> _bearerPool = new Stack<SwarmShieldBearer>();
        private readonly List<EscortRef> _escorts = new List<EscortRef>();
        private readonly List<EscortRef> _escortGroup = new List<EscortRef>();
        private readonly bool[] _bearerTypes = new bool[SwarmLimits.MaxTypes];
        private readonly bool[] _escortTypes = new bool[SwarmLimits.MaxTypes];
        private readonly bool[] _beamTouched = new bool[4];
        private NativeHashMap<int, float2> _follow;
        private bool _anyBearerType;
        private int _shieldFrame;

        /// <summary>今いる盾持ち（全PC。毎フレーム更新。一覧は使い回すので、持ち続けないこと）。</summary>
        public IReadOnlyList<SwarmShieldBearer> ShieldBearers => _bearers;

        private void InitializeShields()
        {
            _follow = new NativeHashMap<int, float2>(256, Allocator.Persistent);
        }

        private void DisposeShields()
        {
            if (_follow.IsCreated)
            {
                _follow.Dispose();
            }
        }

        /// <summary>盾持ちを湧かせたら、お供をまわりに湧かせる（Host・オフライン）。</summary>
        private void SpawnEscorts(EnemyTypeDefinition bearer, Vector2 position)
        {
            var shield = bearer.shield;
            if (shield == null || !shield.hasShield || shield.escortType == null || shield.escortCount <= 0
                || shield.escortType == bearer || shield.escortType.simulationMode != EnemySimulationMode.Swarm)
            {
                return;
            }

            var forward = NearestGoalDirection(position);
            var angle = Mathf.Atan2(forward.y, forward.x);
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            for (var k = 0; k < shield.escortCount; k++)
            {
                var slot = EnemyShieldMath.EscortSlot(k, shield.escortSpacing);
                var offset = new Vector2(slot.x * cos - slot.y * sin, slot.x * sin + slot.y * cos);
                SpawnOne(shield.escortType, position + offset);
            }
        }

        private Vector2 NearestGoalDirection(Vector2 from)
        {
            var best = Vector2.zero;
            var bestDistance = float.MaxValue;
            for (var g = 0; g < _goalCount; g++)
            {
                var goal = (Vector2)_goals[g];
                var d = (goal - from).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = goal;
                }
            }

            var direction = best - from;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.down;
        }

        /// <summary>種類の一覧が変わりうるので毎フレーム（<see cref="RefreshTypeParams"/> の最後）。盾持ち・お供の種類に印を付ける。</summary>
        private void RefreshShieldTypes()
        {
            _anyBearerType = false;
            for (var t = 0; t < SwarmLimits.MaxTypes; t++)
            {
                _bearerTypes[t] = false;
                _escortTypes[t] = false;
            }

            for (var t = 0; t < _types.Count; t++)
            {
                var shield = _types[t].shield;
                if (shield == null || !shield.hasShield)
                {
                    continue;
                }

                _bearerTypes[t] = true;
                _anyBearerType = true;
                var escort = shield.escortType != null ? _types.IndexOf(shield.escortType) : -1;
                if (escort >= 0 && escort != t)
                {
                    _escortTypes[escort] = true;
                }
            }

            for (var t = 0; t < _types.Count; t++)
            {
                var p = _typeParams[t];
                p.followsShield = _escortTypes[t] ? 1 : 0;
                _typeParams[t] = p;
            }
        }

        /// <summary>
        /// ジョブが止まっている間（<see cref="StorageReadable"/> の直前）に呼ぶ。盾持ちを集め、Host ではビームの記録とお供の行き先を決める。
        /// </summary>
        private void UpdateShields(float dt)
        {
            _follow.Clear();
            if (!_anyBearerType)
            {
                if (_bearers.Count > 0)
                {
                    ReleaseAllBearers();
                }

                return;
            }

            _shieldFrame++;
            _escorts.Clear();
            var authority = !IsReplica;
            var count = Storage.Count;
            var typeIdx = Storage.typeIdx;
            var pos = Storage.pos;
            var vel = Storage.vel;
            var ids = Storage.netId;
            for (var i = 0; i < count; i++)
            {
                var type = typeIdx[i];
                if (_bearerTypes[type])
                {
                    var id = ids[i];
                    if (!_bearerById.TryGetValue(id, out var bearer))
                    {
                        bearer = _bearerPool.Count > 0 ? _bearerPool.Pop() : new SwarmShieldBearer();
                        bearer.NetId = id;
                        System.Array.Clear(bearer.Threat, 0, bearer.Threat.Length);
                        var forward = NearestGoalDirection(pos[i]);
                        bearer.TravelAngle = Mathf.Atan2(forward.y, forward.x);
                        _bearerById.Add(id, bearer);
                        _bearers.Add(bearer);
                    }

                    bearer.Type = _types[type];
                    bearer.Position = pos[i];
                    bearer.Velocity = vel[i];
                    bearer.SeenFrame = _shieldFrame;
                }
                else if (authority && _escortTypes[type])
                {
                    _escorts.Add(new EscortRef { index = i, netId = ids[i], position = pos[i], type = type });
                }
            }

            for (var b = _bearers.Count - 1; b >= 0; b--)
            {
                var bearer = _bearers[b];
                if (bearer.SeenFrame != _shieldFrame)
                {
                    _bearers.RemoveAt(b);
                    _bearerById.Remove(bearer.NetId);
                    _bearerPool.Push(bearer);
                }
            }

            if (!authority || _bearers.Count == 0)
            {
                return;
            }

            for (var b = 0; b < _bearers.Count; b++)
            {
                var bearer = _bearers[b];
                var shield = bearer.Type.shield;
                if (bearer.Velocity.sqrMagnitude > 0.0025f)
                {
                    var target = Mathf.Atan2(bearer.Velocity.y, bearer.Velocity.x);
                    var step = 4f * dt;
                    bearer.TravelAngle += Mathf.Clamp(Mathf.DeltaAngle(bearer.TravelAngle * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad, -step, step);
                }

                EnemyShieldMath.Decay(bearer.Threat, dt, shield.threatMemorySeconds);
                RecordThreat(bearer, shield.threatRadius, dt);
            }

            AssignEscorts();
        }

        /// <summary>このフレームに積まれたビームのうち、盾持ちの近くを通った物を撃った人ごとに1回だけ数える（赤の極太レーザーは何本にも分けて積むため）。</summary>
        private void RecordThreat(SwarmShieldBearer bearer, float radius, float dt)
        {
            for (var p = 0; p < _beamTouched.Length; p++)
            {
                _beamTouched[p] = false;
            }

            float2 center = bearer.Position;
            for (var i = 0; i < _beamCount; i++)
            {
                var beam = _beamQueue[i];
                var owner = beam.owner;
                if (owner < 0 || owner >= _beamTouched.Length || _beamTouched[owner] || beam.damagePerSecond <= 0f)
                {
                    continue;
                }

                var reach = radius + beam.halfWidth;
                if (EnemyShieldMath.SegmentDistanceSq(center, beam.origin, beam.end) <= reach * reach)
                {
                    _beamTouched[owner] = true;
                    bearer.Threat[owner] += dt;
                }
            }
        }

        /// <summary>お供をいちばん近い盾持ち（そのお供の種類を連れている物）に割り当て、番号の順に輪の場所を配る。</summary>
        private void AssignEscorts()
        {
            if (_escorts.Count == 0)
            {
                return;
            }

            for (var b = 0; b < _bearers.Count; b++)
            {
                var bearer = _bearers[b];
                var shield = bearer.Type.shield;
                var escortType = shield.escortType != null ? _types.IndexOf(shield.escortType) : -1;
                if (escortType < 0)
                {
                    continue;
                }

                _escortGroup.Clear();
                float2 center = bearer.Position;
                var leash = shield.escortLeashDistance * shield.escortLeashDistance;
                for (var e = 0; e < _escorts.Count; e++)
                {
                    var escort = _escorts[e];
                    if (escort.type != escortType || math.distancesq(escort.position, center) > leash || !IsNearestBearer(escort, b, escortType))
                    {
                        continue;
                    }

                    _escortGroup.Add(escort);
                }

                _escortGroup.Sort((x, y) => x.netId.CompareTo(y.netId));
                var cos = math.cos(bearer.TravelAngle);
                var sin = math.sin(bearer.TravelAngle);
                for (var k = 0; k < _escortGroup.Count; k++)
                {
                    var slot = EnemyShieldMath.EscortSlot(k, shield.escortSpacing);
                    var target = center + new float2(slot.x * cos - slot.y * sin, slot.x * sin + slot.y * cos);
                    _follow.TryAdd(_escortGroup[k].index, target);
                }
            }
        }

        private bool IsNearestBearer(EscortRef escort, int bearerIndex, int escortType)
        {
            var mine = math.distancesq(escort.position, (float2)_bearers[bearerIndex].Position);
            for (var b = 0; b < _bearers.Count; b++)
            {
                if (b == bearerIndex)
                {
                    continue;
                }

                var other = _bearers[b];
                var otherEscort = other.Type.shield.escortType;
                if (otherEscort == null || _types.IndexOf(otherEscort) != escortType)
                {
                    continue;
                }

                var d = math.distancesq(escort.position, (float2)other.Position);
                if (d < mine || (d == mine && b < bearerIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleaseAllBearers()
        {
            foreach (var bearer in _bearers)
            {
                _bearerPool.Push(bearer);
            }

            _bearers.Clear();
            _bearerById.Clear();
        }
    }
}
