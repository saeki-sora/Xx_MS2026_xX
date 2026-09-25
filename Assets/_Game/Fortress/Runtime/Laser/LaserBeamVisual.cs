using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// LaserTurretの状態をLineRendererで可視化するデフォルト実装。
    /// 本番の見た目（シェーダーFX等）に差し替える場合は、このコンポーネントを
    /// 外して同じ役割の別コンポーネントに置き換えればよい。
    /// </summary>
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserBeamVisual : MonoBehaviour
    {
        [Tooltip("未設定の場合は自動でLineRendererを追加して使う。")]
        public LineRenderer lineRenderer;

        [Tooltip("レーザーが当たる対象のレイヤー（敵・地形障害物など）。")]
        public LayerMask targetLayerMask = ~0;

        private LaserTurret _turret;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
                lineRenderer.textureMode = LineTextureMode.Stretch;
            }
        }

        private void Update()
        {
            if (_turret == null || _turret.tuning == null)
            {
                return;
            }

            if (!_turret.IsFiring)
            {
                lineRenderer.enabled = false;
                return;
            }

            var origin = transform.position;
            Vector2 direction = transform.up;
            var range = _turret.tuning.range;
            Vector3 endPoint = origin + (Vector3)(direction * range);

            var hit = Physics2D.Raycast(origin, direction, range, targetLayerMask);
            if (hit.collider != null)
            {
                endPoint = hit.point;
                var baseDamagePerSecond = _turret.tuning.maxDamagePerSecond * Time.deltaTime;

                // 健在な障害物は衝突判定を持つのでレイはそこで止まる＝奥の敵には届かない（完全遮蔽）。
                // 破壊されると判定が消えて自動的にレイが素通りするようになるため、ここでの分岐だけでよい。
                var obstacle = hit.collider.GetComponentInParent<DestructibleObstacle>();
                if (obstacle != null)
                {
                    // 障害物側は太さ(強さ)への反応度合いを自分のチューニングで調整できるようにする
                    // (太さをそのまま等倍で使う敵へのダメージとは別経路)。
                    obstacle.TakeLaserDamage(baseDamagePerSecond, _turret.CurrentThickness01);
                }
                else
                {
                    var enemy = hit.collider.GetComponentInParent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(baseDamagePerSecond * _turret.CurrentThickness01);
                    }
                }
            }

            lineRenderer.enabled = true;
            lineRenderer.startWidth = _turret.CurrentThicknessMeters;
            lineRenderer.endWidth = _turret.CurrentThicknessMeters;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);

            var color = FortressColors.PlayerColor(_turret.playerIndex);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }
}
