using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 副砲の当たり判定の共通の道具。
    /// 群衆の敵: <see cref="SwarmSystem.QueueBeam"/> に1フレームだけの当たり判定を置く（Client では群衆側がダメージを 0 にし、被弾の光だけ出す）。
    /// 群衆は同じフレームに受け付けるレーザーの数に上限（32）があるので、1回で置く数は少なくする。
    /// それ以外（壁・破壊できる障害物・ボスの部位・Actor の敵）: 物理の当たり判定で探し、結果を決めるPC（オフライン／Host）だけダメージを与える。
    /// </summary>
    internal static class SubGunHits
    {
        private static readonly Collider2D[] OverlapBuffer = new Collider2D[24];
        private static readonly HashSet<ILaserTarget> Seen = new HashSet<ILaserTarget>();
        private static readonly List<float> TypeRadius = new List<float>(8);
        private static SwarmSystem _radiusFor;

        /// <summary>群衆は「1秒あたりのダメージ × そのフレームの秒数」を与えるので、1フレームで damage になる値に直す。</summary>
        public static float InstantDps(SwarmSystem swarm, float damage)
        {
            var frame = Time.deltaTime;
            var settings = swarm != null ? swarm.ActiveSettings : null;
            if (settings != null)
            {
                frame = Mathf.Min(frame, settings.maxFrameDelta);
            }

            return damage / Mathf.Max(frame, 1f / 240f);
        }

        /// <summary>丸い範囲の群衆の敵に、1フレームで damage を与える（範囲の中の全員）。</summary>
        public static void QueueCircle(SwarmSystem swarm, Vector2 center, float radius, float damage, int owner)
        {
            if (swarm == null || radius <= 0f)
            {
                return;
            }

            swarm.QueueBeam(center, center, radius, damage > 0f ? InstantDps(swarm, damage) : 0f, 0, owner);
        }

        /// <summary>その場所の群衆の敵1体に、1フレームで damage を与える。</summary>
        public static void QueueSingle(SwarmSystem swarm, Vector2 position, Vector2 from, float damage, int owner)
        {
            if (swarm == null)
            {
                return;
            }

            var away = position - from;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
            swarm.QueueBeam(position - away * 0.02f, position + away * 0.02f, 0.03f, damage > 0f ? InstantDps(swarm, damage) : 0f, 1, owner);
        }

        /// <summary>
        /// 群衆の i 番目の敵の位置（Client は補正込み）。倒れている敵（Host・オフライン）は false。
        /// </summary>
        public static bool TryGetEnemy(SwarmAgentStorage storage, int i, bool replica, out Vector2 position)
        {
            var raw = storage.pos[i];
            position = new Vector2(raw.x, raw.y);
            if (replica)
            {
                var offset = storage.correction[i];
                position.x += offset.x;
                position.y += offset.y;
                return true;
            }

            return storage.hp[i] > 0f;
        }

        /// <summary>群衆の i 番目の敵の大きさ（当たりの半径）。</summary>
        public static float EnemyRadius(SwarmSystem swarm, SwarmAgentStorage storage, int i)
        {
            var types = swarm.RegisteredTypes;
            if (_radiusFor != swarm || TypeRadius.Count != types.Count)
            {
                _radiusFor = swarm;
                TypeRadius.Clear();
                for (var t = 0; t < types.Count; t++)
                {
                    var type = types[t];
                    TypeRadius.Add(type != null && type.swarm != null ? Mathf.Max(type.swarm.radius, type.swarm.hitRadius) : 0.22f);
                }
            }

            var index = storage.typeIdx[i];
            return index >= 0 && index < TypeRadius.Count ? TypeRadius[index] : 0.22f;
        }

        /// <summary>
        /// 丸い範囲の中の、群衆以外の物（Actor の敵・ボスの部位・破壊できる障害物）に1回ずつダメージを与える。
        /// 結果を決めるPCだけ。障害物には obstacleDamage、それ以外には enemyDamage。何かに当たったら true。
        /// </summary>
        public static bool DamageObjectsInCircle(Vector2 center, float radius, LayerMask mask, LaserTurret attacker,
            float enemyDamage, float obstacleDamage)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(mask);
            var count = Physics2D.OverlapCircle(center, radius, filter, OverlapBuffer);
            var any = false;
            Seen.Clear();
            for (var i = 0; i < count; i++)
            {
                var collider = OverlapBuffer[i];
                if (collider == null || IsOwn(collider, attacker))
                {
                    continue;
                }

                var target = collider.GetComponentInParent<ILaserTarget>();
                if (target == null || !Seen.Add(target))
                {
                    continue;
                }

                any = true;
                Apply(target, attacker, target is DestructibleObstacle ? obstacleDamage : enemyDamage);
            }

            Seen.Clear();
            return any;
        }

        /// <summary>丸い範囲に、壁や物（自分の砲台以外の、すり抜けない当たり判定）があるか。</summary>
        public static bool TouchesSolid(Vector2 center, float radius, LayerMask mask, LaserTurret attacker, out Collider2D hit)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(mask);
            var count = Physics2D.OverlapCircle(center, radius, filter, OverlapBuffer);
            for (var i = 0; i < count; i++)
            {
                var collider = OverlapBuffer[i];
                if (collider != null && !IsOwn(collider, attacker))
                {
                    hit = collider;
                    return true;
                }
            }

            hit = null;
            return false;
        }

        /// <summary>当たり判定1つ（本体のレーザーが当たっている物など）にダメージ。結果を決めるPCだけ。</summary>
        public static void DamageCollider(Collider2D collider, LaserTurret attacker, float enemyDamage, float obstacleDamage)
        {
            if (collider == null)
            {
                return;
            }

            var target = collider.GetComponentInParent<ILaserTarget>();
            if (target != null)
            {
                Apply(target, attacker, target is DestructibleObstacle ? obstacleDamage : enemyDamage);
            }
        }

        private static void Apply(ILaserTarget target, LaserTurret attacker, float amount)
        {
            if (amount <= 0f || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            // チューニングを渡さない（障害物ダメージ倍率を掛けない）。副砲の値がそのまま入る。
            if (target is IAttributedLaserTarget attributed && attacker != null)
            {
                attributed.ApplyLaserDamage(amount, null, attacker);
            }
            else
            {
                target.ApplyLaserDamage(amount, null);
            }
        }

        private static bool IsOwn(Collider2D collider, LaserTurret attacker)
        {
            return attacker != null && (collider.transform == attacker.transform || collider.transform.IsChildOf(attacker.transform));
        }
    }
}
