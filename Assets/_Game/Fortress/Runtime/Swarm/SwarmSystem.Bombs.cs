using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆弾の敵（<see cref="EnemyBombSettings"/>）の爆発。
    /// 1. 詰め直しのジョブ（<see cref="SwarmCompactScanJob"/>）が、倒された・コアで自爆した爆弾を記録する。
    /// 2. ジョブの回収時に、その場で演出を出し（全PC）、Host・オフラインでは「まわりへのダメージ」を
    ///    連鎖の間隔（chainDelaySeconds）だけ待たせて列（<see cref="BombBlastQueue"/>）に積む。
    /// 3. 時刻が来たダメージは、群衆の敵へは次のレーザー判定のジョブで（<see cref="SwarmLaserJob"/> の blasts）、
    ///    1体ずつの敵(Actor)・破壊可能物・コアへはメインスレッドで与える。巻き込まれた爆弾はまた 1. に戻る（連鎖）。
    /// ネット対戦のClientは自分ではダメージを与えない。Hostで「倒された／コアに着いた」と届いた爆弾の位置で演出だけ出す
    /// （結果の体力・撃破・コアのHPは今までの同期で届く）。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        /// <summary>このフレームに爆発した爆弾の一覧（演出用。一覧は次のフレームで使い回すので、受け取った場で使い切ること）。</summary>
        public event Action<IReadOnlyList<SwarmExplosion>> BombsExploded;

        /// <summary>まだダメージを届けていない爆発の数（Host・オフライン）。</summary>
        public int PendingBlastCount => _blastQueue.Count;

        private readonly BombBlastQueue _blastQueue = new BombBlastQueue();
        private readonly List<BombBlast> _dueBlasts = new List<BombBlast>();
        private readonly List<SwarmExplosion> _reportedExplosions = new List<SwarmExplosion>();
        private readonly Dictionary<int, byte> _pendingExplodeIds = new Dictionary<int, byte>();
        private readonly HashSet<DestructibleObstacle> _blastObstacles = new HashSet<DestructibleObstacle>();
        private readonly List<Collider2D> _blastColliders = new List<Collider2D>();
        private readonly List<EnemyController> _blastActors = new List<EnemyController>();
        private readonly LaserTurret[] _turretsByPlayer = new LaserTurret[KillPlayerCount];

        private NativeList<SwarmExplosion> _explosions;
        private NativeArray<SwarmBlast> _blastsJob;
        private NativeHashMap<int, byte> _explodeIds;
        private int _blastCount;

        private void InitializeBombs()
        {
            _explosions = new NativeList<SwarmExplosion>(64, Allocator.Persistent);
            _blastsJob = new NativeArray<SwarmBlast>(SwarmLimits.MaxBlastsPerFrame, Allocator.Persistent);
            _explodeIds = new NativeHashMap<int, byte>(256, Allocator.Persistent);
        }

        private void DisposeBombs()
        {
            if (_explosions.IsCreated)
            {
                _explosions.Dispose();
            }

            if (_explodeIds.IsCreated)
            {
                _explodeIds.Dispose();
            }

            DisposeArray(ref _blastsJob);
        }

        /// <summary>Client: Hostで倒された／コアに着いた敵の番号を覚えておく（爆弾なら、消えるときに爆発の演出を出す）。</summary>
        private void RememberReplicaExplosion(int id, EnemyRemovalReason reason)
        {
            if (reason == EnemyRemovalReason.Died || reason == EnemyRemovalReason.ReachedCore)
            {
                _pendingExplodeIds[id] = (byte)reason;
            }
        }

        // ジョブに渡す「爆発の演出を出す番号」(Client)を用意する。消す番号(_removeIds)と同じフレームに渡す。
        private void PrepareBombJobInputs()
        {
            _explodeIds.Clear();
            foreach (var pair in _pendingExplodeIds)
            {
                _explodeIds[pair.Key] = pair.Value;
            }

            _pendingExplodeIds.Clear();
        }

        private void DiscardBombJobInputs()
        {
            _pendingExplodeIds.Clear();
            _blastCount = 0;
        }

        private void ClearBombs()
        {
            _blastQueue.Clear();
            _pendingExplodeIds.Clear();
            _blastCount = 0;
        }

        /// <summary>ジョブの回収後に呼ぶ。爆発した爆弾の演出を出し、Hostではまわりへのダメージを列に積む。</summary>
        private void CollectExplosions()
        {
            var count = _explosions.Length;
            if (count == 0)
            {
                return;
            }

            _reportedExplosions.Clear();
            var now = Time.time;
            for (var i = 0; i < count; i++)
            {
                var explosion = _explosions[i];
                var definition = GetRegisteredType(explosion.type);
                var bomb = definition != null ? definition.bomb : null;
                if (bomb == null)
                {
                    continue;
                }

                _reportedExplosions.Add(explosion);
                PlayExplosionEffects(bomb, explosion);

                if (IsReplica)
                {
                    continue;
                }

                _blastQueue.Add(new BombBlast
                {
                    dueTime = now + bomb.chainDelaySeconds,
                    center = explosion.position,
                    radius = bomb.explosionRadius,
                    enemyDamage = bomb.enemyDamage,
                    obstacleDamage = bomb.obstacleDamage,
                    coreDamage = explosion.cause == SwarmExplosionCause.ReachedCore ? 0f : bomb.coreDamage,
                    owner = explosion.owner
                });
            }

            _explosions.Clear();
            if (_reportedExplosions.Count > 0)
            {
                BombsExploded?.Invoke(_reportedExplosions);
            }
        }

        private static void PlayExplosionEffects(EnemyBombSettings bomb, SwarmExplosion explosion)
        {
            var position = new Vector3(explosion.position.x, explosion.position.y, 0f);
            FortressEffectPlayer.PlayOnce(bomb.onExplode, position, Quaternion.identity, explosion.owner);
            if (bomb.showBlastRing)
            {
                BombBlastRing.Spawn(explosion.position, bomb.explosionRadius, bomb.blastRingColor);
            }
        }

        /// <summary>
        /// 時刻が来た爆発のダメージを取り出す（ジョブを組む前に呼ぶ。Host・オフラインのみ）。群衆の敵の分はジョブ用の配列に入れ、
        /// それ以外（Actorの敵・破壊可能物・コア）にはこの場で与える。
        /// </summary>
        private void PrepareBlasts()
        {
            _blastCount = 0;
            if (IsReplica || _blastQueue.Count == 0)
            {
                return;
            }

            _blastQueue.TakeDue(Time.time, SwarmLimits.MaxBlastsPerFrame, _dueBlasts);
            if (_dueBlasts.Count == 0)
            {
                return;
            }

            RefreshTurretsByPlayer();
            foreach (var blast in _dueBlasts)
            {
                if (blast.enemyDamage > 0f)
                {
                    _blastsJob[_blastCount++] = new SwarmBlast
                    {
                        center = blast.center,
                        radius = blast.radius,
                        damage = blast.enemyDamage,
                        owner = blast.owner
                    };
                }

                DamageActors(blast);
                DamageObstacles(blast);
                DamageCores(blast);
            }
        }

        private void DamageActors(BombBlast blast)
        {
            if (blast.enemyDamage <= 0f || EnemyController.Active.Count == 0)
            {
                return;
            }

            var turret = TurretOf(blast.owner);
            var radiusSq = blast.radius * blast.radius;

            // 倒れた敵は Active から抜けるので、写してから回す。
            _blastActors.Clear();
            _blastActors.AddRange(EnemyController.Active);
            foreach (var actor in _blastActors)
            {
                if (actor == null || ((Vector2)actor.transform.position - blast.center).sqrMagnitude > radiusSq)
                {
                    continue;
                }

                if (turret != null)
                {
                    actor.ApplyLaserDamage(blast.enemyDamage, null, turret);
                }
                else
                {
                    actor.TakeDamage(blast.enemyDamage);
                }
            }
        }

        private void DamageObstacles(BombBlast blast)
        {
            if (blast.obstacleDamage <= 0f)
            {
                return;
            }

            var filter = new ContactFilter2D();
            filter.NoFilter();
            Physics2D.OverlapCircle(blast.center, blast.radius, filter, _blastColliders);

            _blastObstacles.Clear();
            foreach (var hit in _blastColliders)
            {
                var obstacle = hit != null ? hit.GetComponentInParent<DestructibleObstacle>() : null;
                // スマッシュボールは爆弾では割れない（プレイヤーがレーザーで割るもの。2026-10-19 ユーザー決定）。
                if (obstacle != null && !obstacle.TryGetComponent<SmashBallModule>(out _))
                {
                    _blastObstacles.Add(obstacle);
                }
            }

            var turret = TurretOf(blast.owner);
            var attacker = blast.owner >= 0 ? new DestructibleAttacker(blast.owner, turret) : DestructibleAttacker.None;
            foreach (var obstacle in _blastObstacles)
            {
                obstacle.ApplyDamage(blast.obstacleDamage, DamageSource.Explosion, attacker);
            }
        }

        private void DamageCores(BombBlast blast)
        {
            if (blast.coreDamage <= 0f)
            {
                return;
            }

            // コアには当たり判定(Collider2D)が無いので、敵が「コアに着いた」とみなす距離と同じだけ余裕を見る。
            var reach = blast.radius + _settings.arrivalRadius;
            foreach (var core in _cores)
            {
                if (core != null && ((Vector2)core.transform.position - blast.center).sqrMagnitude <= reach * reach)
                {
                    core.TakeDamage(blast.coreDamage);
                }
            }
        }

        private LaserTurret TurretOf(int player)
        {
            return player >= 0 && player < _turretsByPlayer.Length ? _turretsByPlayer[player] : null;
        }

        private void RefreshTurretsByPlayer()
        {
            for (var p = 0; p < _turretsByPlayer.Length; p++)
            {
                if (_turretsByPlayer[p] == null || _turretsByPlayer[p].playerIndex != p)
                {
                    _turretsByPlayer[p] = null;
                }
            }

            var missing = false;
            for (var p = 0; p < _turretsByPlayer.Length; p++)
            {
                missing |= _turretsByPlayer[p] == null;
            }

            if (!missing)
            {
                return;
            }

            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                var p = turret.playerIndex;
                if (p >= 0 && p < _turretsByPlayer.Length && _turretsByPlayer[p] == null)
                {
                    _turretsByPlayer[p] = turret;
                }
            }
        }
    }
}
