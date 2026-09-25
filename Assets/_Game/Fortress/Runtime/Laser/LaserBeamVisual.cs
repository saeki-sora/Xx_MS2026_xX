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

        private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[16];

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

            var origin = _turret.MuzzlePosition;
            Vector2 direction = _turret.AimDirection;
            var range = _turret.tuning.range;
            Vector3 endPoint = origin + (Vector3)(direction * range);

            // トリガー（通行コスト地帯など）は貫通させ、最も近い実体にだけ当てる。
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(targetLayerMask);
            var hitCount = Physics2D.Raycast(origin, direction, filter, HitBuffer, range);

            var nearest = -1;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < hitCount; i++)
            {
                if (HitBuffer[i].distance < nearestDistance)
                {
                    nearestDistance = HitBuffer[i].distance;
                    nearest = i;
                }
            }

            if (nearest >= 0)
            {
                var hit = HitBuffer[nearest];
                endPoint = hit.point;

                var target = hit.collider.GetComponentInParent<ILaserTarget>();
                if (target != null)
                {
                    var damage = _turret.tuning.maxDamagePerSecond * _turret.CurrentThickness01 * Time.deltaTime;
                    if (target is IAttributedLaserTarget attributed)
                    {
                        attributed.ApplyLaserDamage(damage, _turret.tuning, _turret);
                    }
                    else
                    {
                        target.ApplyLaserDamage(damage, _turret.tuning);
                    }
                }
            }

            var swarm = SwarmSystem.Current;
            if (swarm != null)
            {
                var tuning = _turret.tuning;
                swarm.QueueBeam(
                    origin,
                    endPoint,
                    _turret.CurrentThicknessMeters * 0.5f,
                    tuning.maxDamagePerSecond * _turret.CurrentThickness01,
                    tuning.pierceEnemies ? tuning.maxPierceCount : 1);
            }

            lineRenderer.enabled = true;
            lineRenderer.loop = false;
            lineRenderer.positionCount = 2;
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
