using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 葉に触れた敵にダメージを与える係（飛び道具は無い）。
    /// 群衆の敵: 群衆の計算が止まっていて配列を読める瞬間（<see cref="SwarmSystem.StorageReadable"/>。このフレームのレーザーの当たり判定を
    /// 受け付ける直前）に、葉の近くの敵を探し、触れていればその敵の場所に小さな当たり判定（<see cref="SwarmSystem.QueueBeam"/>、1体だけ）を
    /// 1フレームだけ置く。同じ敵には <see cref="GreenLeafRingSettings.rehitSeconds"/> 秒あけないと当たらない（通信用の番号 netId で見分ける）。
    /// Client でも同じように置く（群衆側がダメージを 0 にして被弾フラッシュだけ出す）。倒した敵は緑のプレイヤーの手柄になる。
    /// Actor の敵: 毎フレーム距離を見て、結果を決めるPC（オフライン／Host）だけダメージ。
    /// 当たった場所は <see cref="Hits"/> に入る（演出用。全PCがそれぞれ出す）。
    /// </summary>
    internal sealed class GreenLeafContacts : IDisposable
    {
        /// <summary>当たった瞬間1件（演出用）。</summary>
        public struct Hit
        {
            public Vector2 Position;
            public int Leaf;
        }

        private const float ActorRadius = 0.4f;
        private const float Reach = 2f;

        private readonly GreenLeafRing _ring;
        private readonly List<Hit> _hits = new List<Hit>(32);
        private readonly Dictionary<int, float> _swarmCooldown = new Dictionary<int, float>();
        private readonly Dictionary<EnemyController, float> _actorCooldown = new Dictionary<EnemyController, float>();
        private readonly List<int> _expired = new List<int>();
        private readonly List<EnemyController> _expiredActors = new List<EnemyController>();
        private readonly List<float> _typeRadius = new List<float>(8);
        private readonly Vector2[] _leafPositions = new Vector2[GreenLeafRing.MaxLeaves];
        private readonly bool[] _leafTouchable = new bool[GreenLeafRing.MaxLeaves];

        private SwarmSystem _swarm;
        private LaserTurret _owner;
        private bool _active;
        private int _leafCount;
        private float _hitRadius;
        private Vector2 _center;
        private float _outerRadius;
        private float _nextCleanup;

        public GreenLeafContacts(GreenLeafRing ring)
        {
            _ring = ring;
        }

        /// <summary>このフレームまでに当たった場所（演出を出したら <see cref="ClearHits"/>）。</summary>
        public IReadOnlyList<Hit> Hits => _hits;

        public void ClearHits()
        {
            _hits.Clear();
        }

        /// <summary>当たり判定を始める／やめる。</summary>
        public void SetActive(bool active, LaserTurret owner)
        {
            _owner = owner;
            if (_active == active)
            {
                return;
            }

            _active = active;
            if (!active)
            {
                Subscribe(null);
                _swarmCooldown.Clear();
                _actorCooldown.Clear();
                _leafCount = 0;
            }
        }

        public void Dispose()
        {
            SetActive(false, null);
        }

        /// <summary>
        /// 毎フレーム、葉を動かした後に呼ぶ。今の葉の位置を覚え（群衆の当たり判定は、この後の群衆の更新で行う）、Actor の敵に当てる。
        /// </summary>
        public void Tick(Vector2 core)
        {
            if (!_active)
            {
                return;
            }

            var current = SwarmSystem.Current;
            if (current != _swarm)
            {
                Subscribe(current);
                _swarmCooldown.Clear();
            }

            var settings = _owner != null ? Settings : null;
            if (settings == null)
            {
                _leafCount = 0;
                return;
            }

            _center = core;
            _leafCount = _ring.Count;
            _hitRadius = settings.hitRadius * _ring.SizeScale;
            var outer = 0f;
            for (var i = 0; i < _leafCount; i++)
            {
                _leafPositions[i] = _ring.PositionOf(i);
                _leafTouchable[i] = _ring.IsTouchable(i);
                if (_leafTouchable[i])
                {
                    outer = Mathf.Max(outer, (_leafPositions[i] - core).magnitude);
                }
            }

            _outerRadius = outer + _hitRadius + Reach;
            if (settings.hitActors)
            {
                HitActors(settings);
            }

            if (Time.time >= _nextCleanup)
            {
                _nextCleanup = Time.time + 2f;
                Cleanup();
            }
        }

        /// <summary>このフレームは当てない（吸い込まれている間など）。</summary>
        public void Pause()
        {
            _leafCount = 0;
        }

        private GreenLeafRingSettings Settings => _ringDefinition?.leaves;

        private GreenMintSkill _ringDefinition;

        /// <summary>設定ファイル（ランナーが最初に1回渡す）。</summary>
        public void Configure(GreenMintSkill definition)
        {
            _ringDefinition = definition;
        }

        private void HitActors(GreenLeafRingSettings settings)
        {
            var actors = EnemyController.Active;
            var now = Time.time;
            var authority = FortressNet.HasSimulationAuthority;
            for (var a = 0; a < actors.Count; a++)
            {
                var actor = actors[a];
                if (actor == null || !actor.isActiveAndEnabled)
                {
                    continue;
                }

                if (_actorCooldown.TryGetValue(actor, out var until) && until > now)
                {
                    continue;
                }

                var position = (Vector2)actor.transform.position;
                var leaf = TouchingLeaf(position, ActorRadius);
                if (leaf < 0)
                {
                    continue;
                }

                _actorCooldown[actor] = now + settings.rehitSeconds;
                if (authority && settings.damage > 0f && _owner != null)
                {
                    actor.ApplyLaserDamage(settings.damage, null, _owner);
                }

                AddHit(position, leaf);
            }
        }

        /// <summary>position（半径 radius の敵）に触れている葉の番号。無ければ -1。</summary>
        private int TouchingLeaf(Vector2 position, float radius)
        {
            var reach = _hitRadius + radius;
            var reach2 = reach * reach;
            for (var i = 0; i < _leafCount; i++)
            {
                if (_leafTouchable[i] && (position - _leafPositions[i]).sqrMagnitude <= reach2)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AddHit(Vector2 position, int leaf)
        {
            if (_hits.Count < 64)
            {
                _hits.Add(new Hit { Position = position, Leaf = leaf });
            }
        }

        private void Subscribe(SwarmSystem swarm)
        {
            if (_swarm != null)
            {
                _swarm.StorageReadable -= HandleStorageReadable;
            }

            _swarm = swarm;
            if (_swarm != null)
            {
                _swarm.StorageReadable += HandleStorageReadable;
            }
        }

        /// <summary>群衆の配列を読める瞬間: 葉に触れている敵に、その場所で1フレームだけ当たり判定を置く。</summary>
        private void HandleStorageReadable()
        {
            var settings = Settings;
            var swarm = _swarm;
            var storage = swarm != null ? swarm.Storage : null;
            if (!_active || settings == null || storage == null || _leafCount == 0 || _owner == null)
            {
                return;
            }

            RefreshTypeRadius(swarm);
            var now = Time.time;
            var replica = swarm.IsReplica;
            var frame = Time.deltaTime;
            var swarmSettings = swarm.ActiveSettings;
            if (swarmSettings != null)
            {
                frame = Mathf.Min(frame, swarmSettings.maxFrameDelta);
            }

            // 群衆側は「1秒あたりのダメージ × そのフレームの秒数」を与えるので、1フレームで damage になるように割る。
            var dps = settings.damage / Mathf.Max(frame, 1f / 240f);
            var outer2 = _outerRadius * _outerRadius;
            var placed = 0;
            var count = storage.Count;
            for (var i = 0; i < count && placed < settings.maxHitsPerFrame; i++)
            {
                var raw = storage.pos[i];
                var position = new Vector2(raw.x, raw.y);
                if (replica)
                {
                    var offset = storage.correction[i];
                    position.x += offset.x;
                    position.y += offset.y;
                }
                else if (storage.hp[i] <= 0f)
                {
                    continue;
                }

                if ((position - _center).sqrMagnitude > outer2)
                {
                    continue;
                }

                var type = storage.typeIdx[i];
                var radius = type >= 0 && type < _typeRadius.Count ? _typeRadius[type] : 0.22f;
                var leaf = TouchingLeaf(position, radius);
                if (leaf < 0)
                {
                    continue;
                }

                var id = storage.netId[i];
                if (_swarmCooldown.TryGetValue(id, out var until) && until > now)
                {
                    continue;
                }

                _swarmCooldown[id] = now + settings.rehitSeconds;
                placed++;
                // 敵の真上に、短く細い当たり判定（1体だけ）。葉の中心から敵へ向けて置く。
                var away = position - _leafPositions[leaf];
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                swarm.QueueBeam(position - away * 0.02f, position + away * 0.02f, 0.03f, settings.damage > 0f ? dps : 0f, 1, _owner.playerIndex);
                AddHit(position, leaf);
            }
        }

        private void RefreshTypeRadius(SwarmSystem swarm)
        {
            var types = swarm.RegisteredTypes;
            if (_typeRadius.Count == types.Count)
            {
                return;
            }

            _typeRadius.Clear();
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                _typeRadius.Add(type != null && type.swarm != null ? Mathf.Max(type.swarm.radius, type.swarm.hitRadius) : 0.22f);
            }
        }

        private void Cleanup()
        {
            var now = Time.time;
            _expired.Clear();
            foreach (var pair in _swarmCooldown)
            {
                if (pair.Value <= now)
                {
                    _expired.Add(pair.Key);
                }
            }

            for (var i = 0; i < _expired.Count; i++)
            {
                _swarmCooldown.Remove(_expired[i]);
            }

            _expiredActors.Clear();
            foreach (var pair in _actorCooldown)
            {
                if (pair.Key == null || pair.Value <= now)
                {
                    _expiredActors.Add(pair.Key);
                }
            }

            for (var i = 0; i < _expiredActors.Count; i++)
            {
                _actorCooldown.Remove(_expiredActors[i]);
            }
        }
    }
}
