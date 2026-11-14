using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】砲台の丸い体を、障害物（Collider2D）に沿って滑らせながら動かす。
    /// 「トリガーではない Collider2D」と「敵が通れない壁（Block の NavigationObstacle）のトリガー」に当たる。砲台・Actor の敵の Collider には当たらない。
    /// </summary>
    internal static class TurretMoveCollision
    {
        private const int MaxSlides = 3;
        private const int MaxDepenetrationSteps = 4;
        private const float Skin = 0.02f;

        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[32];
        private static readonly Collider2D[] Overlaps = new Collider2D[32];

        /// <summary>直前の <see cref="Move"/> で当たった（止められた・押し出された）物。何にも当たらなければ null。確かめる用。</summary>
        public static Collider2D LastBlocker { get; private set; }

        /// <summary>from から delta だけ動かす。壁に当たったら壁に沿って滑る。動いた後の場所を返す。</summary>
        public static Vector2 Move(Vector2 from, Vector2 delta, float radius, LayerMask mask)
        {
            LastBlocker = null;
            var filter = MakeFilter(mask);
            var position = Depenetrate(from, radius, filter);
            var remaining = delta;

            for (var slide = 0; slide < MaxSlides; slide++)
            {
                var length = remaining.magnitude;
                if (length < 1e-5f)
                {
                    break;
                }

                var dir = remaining / length;
                if (!TryCast(position, radius, dir, length + Skin, filter, out var hit))
                {
                    position += remaining;
                    break;
                }

                LastBlocker = hit.collider;
                var travel = Mathf.Max(0f, hit.distance - Skin);
                position += dir * travel;

                // 当たった面に沿う分だけ残す（面に押し込む分は捨てる）。
                var leftover = remaining - dir * travel;
                var into = Vector2.Dot(leftover, hit.normal);
                if (into < 0f)
                {
                    leftover -= hit.normal * into;
                }

                remaining = leftover;
            }

            return Depenetrate(position, radius, filter);
        }

        /// <summary>障害物にめり込んでいたら外へ出す（動く障害物に押された・敵に押し込まれたとき用）。</summary>
        public static Vector2 Depenetrate(Vector2 position, float radius, LayerMask mask)
        {
            return Depenetrate(position, radius, MakeFilter(mask));
        }

        private static Vector2 Depenetrate(Vector2 position, float radius, ContactFilter2D filter)
        {
            for (var step = 0; step < MaxDepenetrationSteps; step++)
            {
                var count = Physics2D.OverlapCircle(position, radius, filter, Overlaps);
                var moved = false;
                for (var i = 0; i < count; i++)
                {
                    var collider = Overlaps[i];
                    if (!IsSolid(collider))
                    {
                        continue;
                    }

                    var closest = collider.ClosestPoint(position);
                    var diff = position - closest;
                    var dist = diff.magnitude;
                    if (dist > 1e-4f)
                    {
                        if (dist < radius)
                        {
                            position += diff / dist * (radius - dist + Skin * 0.5f);
                            moved = true;
                            if (LastBlocker == null) LastBlocker = collider;
                        }

                        continue;
                    }

                    // 中心が障害物の中に入ってしまった。障害物の中心から遠ざかる向きへ少しずつ出す。
                    var away = position - (Vector2)collider.bounds.center;
                    away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                    position += away * radius;
                    moved = true;
                    if (LastBlocker == null) LastBlocker = collider;
                }

                if (!moved)
                {
                    break;
                }
            }

            return position;
        }

        private static bool TryCast(Vector2 origin, float radius, Vector2 dir, float distance, ContactFilter2D filter, out RaycastHit2D nearest)
        {
            nearest = default;
            var found = false;
            var count = Physics2D.CircleCast(origin, radius, dir, filter, Hits, distance);
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (!IsSolid(hit.collider))
                {
                    continue;
                }

                // 始めから触れていて、離れる向きに動くなら当たりにしない（壁から離れられなくなるのを防ぐ）。
                if (hit.distance <= 0f && Vector2.Dot(hit.normal, dir) >= 0f)
                {
                    continue;
                }

                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsSolid(Collider2D collider)
        {
            if (collider == null || !collider.enabled)
            {
                return false;
            }

            // トリガーでも、敵が通れない壁（「レーザーが素通りする壁」など）には当たる。遅くなる地帯・瓦礫（Cost）は通れる。
            if (collider.isTrigger)
            {
                var navigation = collider.GetComponentInParent<NavigationObstacle>();
                if (navigation == null || !navigation.isActiveAndEnabled || navigation.mode != NavigationObstacleMode.Block)
                {
                    return false;
                }
            }

            // 砲台どうし・Actor の敵（敵は別の「押される」計算で扱う）には当たらない。
            return collider.GetComponentInParent<LaserTurret>() == null
                   && collider.GetComponentInParent<EnemyController>() == null;
        }

        private static ContactFilter2D MakeFilter(LayerMask mask)
        {
            // トリガーも拾い、当たるかどうかは IsSolid で決める。
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(mask);
            return filter;
        }
    }
}
