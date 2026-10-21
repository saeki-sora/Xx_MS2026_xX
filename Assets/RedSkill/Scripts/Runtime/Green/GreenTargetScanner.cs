using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 衛星が狙う敵を探す係。コアに近い敵ほど先に狙う（爆弾の敵はさらに先に）。同じ敵に弾が重ならないよう、弾が飛んでいる敵は「予約済み」にする。
    /// 群衆の敵は、群衆の計算が止まっていて配列を読める瞬間（<see cref="SwarmSystem.StorageReadable"/>）にだけ読む。
    /// 群衆の敵は並び順が毎フレーム変わるので、通信用の番号（netId）で追いかける。Client でも同じ番号が届いているので、各PCが自分で狙える。
    /// </summary>
    internal sealed class GreenTargetScanner : IDisposable
    {
        private const int MaxCandidates = 32;

        /// <summary>狙う敵1体。群衆の敵なら SwarmId（0 以上）、Actor の敵なら Actor。</summary>
        public struct Target
        {
            public int SwarmId;
            public EnemyController Actor;
            public Vector2 Position;
            public float Score;

            public bool IsSwarm => Actor == null && SwarmId >= 0;
        }

        private struct Tracked
        {
            public Vector2 Position;
            public int SeenScan;
        }

        private readonly Target[] _swarmTop = new Target[MaxCandidates];
        private readonly List<Target> _candidates = new List<Target>(MaxCandidates * 2);
        private readonly Dictionary<int, Tracked> _tracked = new Dictionary<int, Tracked>();
        private readonly HashSet<EnemyController> _reservedActors = new HashSet<EnemyController>();
        private readonly List<bool> _bombTypes = new List<bool>(8);

        private SwarmSystem _swarm;
        private int _swarmTopCount;
        private int _scan;
        private bool _active;
        private Vector2 _center;
        private float _range;
        private float _bombBias;
        private bool _includeActors;

        /// <summary>狙う中心（コア）・半径・爆弾を先に狙う距離・Actor の敵も狙うか。毎フレーム呼んでよい。</summary>
        public void Configure(Vector2 center, float range, float bombBias, bool includeActors)
        {
            _center = center;
            _range = Mathf.Max(0.1f, range);
            _bombBias = Mathf.Max(0f, bombBias);
            _includeActors = includeActors;
        }

        /// <summary>探すのを始める／やめる（やめると予約も消える）。</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (!active)
            {
                Subscribe(null);
                _tracked.Clear();
                _reservedActors.Clear();
                _swarmTopCount = 0;
                _candidates.Clear();
            }
        }

        public void Dispose()
        {
            SetActive(false);
        }

        /// <summary>
        /// 毎フレーム、弾を撃つ前に1回呼ぶ。群衆の切り替わり（新しい試合）に付いていき、今フレームの狙う候補を並べ直す。
        /// </summary>
        public void BeginFrame()
        {
            if (!_active)
            {
                return;
            }

            var current = SwarmSystem.Current;
            if (current != _swarm)
            {
                Subscribe(current);
                _tracked.Clear();
                _swarmTopCount = 0;
            }

            _candidates.Clear();
            for (var i = 0; i < _swarmTopCount; i++)
            {
                if (!_tracked.ContainsKey(_swarmTop[i].SwarmId))
                {
                    _candidates.Add(_swarmTop[i]);
                }
            }

            if (_includeActors)
            {
                AddActorCandidates();
            }

            _candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
        }

        /// <summary>まだ誰も狙っていない、一番コアに近い敵を1体選んで予約する。いなければ false。</summary>
        public bool TryTake(out Target target)
        {
            if (_candidates.Count == 0)
            {
                target = default;
                return false;
            }

            target = _candidates[0];
            _candidates.RemoveAt(0);
            if (target.IsSwarm)
            {
                _tracked[target.SwarmId] = new Tracked { Position = target.Position, SeenScan = _scan };
            }
            else
            {
                _reservedActors.Add(target.Actor);
            }

            return true;
        }

        /// <summary>予約を外す（弾が当たった・消えたとき）。</summary>
        public void Release(in Target target)
        {
            if (target.IsSwarm)
            {
                _tracked.Remove(target.SwarmId);
            }
            else if (!ReferenceEquals(target.Actor, null))
            {
                // 倒されて消えた敵（Unity では null 扱い）も、予約の表からは外す。
                _reservedActors.Remove(target.Actor);
            }
        }

        /// <summary>狙っている敵の今の位置。もういない（倒された・コアに着いた）なら false。</summary>
        public bool TryGetPosition(in Target target, out Vector2 position)
        {
            if (target.IsSwarm)
            {
                if (_tracked.TryGetValue(target.SwarmId, out var tracked) && tracked.SeenScan >= _scan - 1)
                {
                    position = tracked.Position;
                    return true;
                }

                position = default;
                return false;
            }

            if (target.Actor != null && target.Actor.isActiveAndEnabled)
            {
                position = target.Actor.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        private void AddActorCandidates()
        {
            var actors = EnemyController.Active;
            var range2 = _range * _range;
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || !actor.isActiveAndEnabled || _reservedActors.Contains(actor))
                {
                    continue;
                }

                var position = (Vector2)actor.transform.position;
                var d2 = (position - _center).sqrMagnitude;
                if (d2 > range2)
                {
                    continue;
                }

                var bomb = actor.definition != null && actor.definition.bomb != null && actor.definition.bomb.explodeOnDeath;
                _candidates.Add(new Target
                {
                    SwarmId = -1,
                    Actor = actor,
                    Position = position,
                    Score = Mathf.Sqrt(d2) - (bomb ? _bombBias : 0f)
                });
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

        /// <summary>群衆の配列を読める瞬間: 予約中の敵の位置を更新し、狙う範囲の中でコアに近い順に候補を残す。</summary>
        private void HandleStorageReadable()
        {
            var swarm = _swarm;
            var storage = swarm != null ? swarm.Storage : null;
            if (storage == null)
            {
                return;
            }

            _scan++;
            RefreshBombTypes(swarm);
            _swarmTopCount = 0;

            var count = storage.Count;
            var range2 = _range * _range;
            var replica = swarm.IsReplica;
            var hasTracked = _tracked.Count > 0;
            for (var i = 0; i < count; i++)
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

                var id = storage.netId[i];
                if (hasTracked && _tracked.ContainsKey(id))
                {
                    _tracked[id] = new Tracked { Position = position, SeenScan = _scan };
                    continue;
                }

                var dx = position.x - _center.x;
                var dy = position.y - _center.y;
                var d2 = dx * dx + dy * dy;
                if (d2 > range2)
                {
                    continue;
                }

                var type = storage.typeIdx[i];
                var bomb = type >= 0 && type < _bombTypes.Count && _bombTypes[type];
                var score = Mathf.Sqrt(d2) - (bomb ? _bombBias : 0f);
                Insert(new Target { SwarmId = id, Actor = null, Position = position, Score = score });
            }
        }

        /// <summary>候補の表（コアに近い順・最大 <see cref="MaxCandidates"/> 体）に入れる。</summary>
        private void Insert(in Target target)
        {
            if (_swarmTopCount == MaxCandidates && target.Score >= _swarmTop[MaxCandidates - 1].Score)
            {
                return;
            }

            var index = Mathf.Min(_swarmTopCount, MaxCandidates - 1);
            while (index > 0 && _swarmTop[index - 1].Score > target.Score)
            {
                if (index < MaxCandidates)
                {
                    _swarmTop[index] = _swarmTop[index - 1];
                }

                index--;
            }

            _swarmTop[index] = target;
            if (_swarmTopCount < MaxCandidates)
            {
                _swarmTopCount++;
            }
        }

        private void RefreshBombTypes(SwarmSystem swarm)
        {
            _bombTypes.Clear();
            var types = swarm.RegisteredTypes;
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                _bombTypes.Add(type != null && type.bomb != null && type.bomb.explodeOnDeath);
            }
        }
    }
}
