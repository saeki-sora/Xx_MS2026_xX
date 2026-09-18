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

                var enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    var damage = _turret.tuning.maxDamagePerSecond * _turret.CurrentThickness01 * Time.deltaTime;
                    enemy.TakeDamage(damage);
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
