using MS2026.Fortress.Net;
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

        private static readonly System.Collections.Generic.List<LaserBeamVisual> ActiveList =
            new System.Collections.Generic.List<LaserBeamVisual>();

        private LaserTurret _turret;

        /// <summary>シーン内で有効なレーザー表示の一覧（背景の焦げ・光りなど、着弾を見て反応する見た目用）。</summary>
        public static System.Collections.Generic.IReadOnlyList<LaserBeamVisual> Active => ActiveList;

        /// <summary>撃っている砲台（Awake以降で有効）。</summary>
        public LaserTurret Turret => _turret;

        /// <summary>このフレーム、レーザーが何か（壁・破壊可能物など、トリガー以外）に当たっているか。着弾点の演出が使う。</summary>
        public bool HasImpact { get; private set; }

        /// <summary>当たっている場所（<see cref="HasImpact"/> のときだけ意味がある）。</summary>
        public Vector3 ImpactPoint { get; private set; }

        /// <summary>当たった面の向き（砲台側を向く）。</summary>
        public Vector2 ImpactNormal { get; private set; }

        /// <summary>当たっている当たり判定（<see cref="HasImpact"/> のときだけ意味がある）。全PCで同じように求まる。</summary>
        public Collider2D ImpactCollider { get; private set; }

        private void OnEnable()
        {
            ActiveList.Add(this);
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
            HasImpact = false;
            ImpactCollider = null;
        }

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
                HasImpact = false;
                ImpactCollider = null;
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

            // ネット対戦のClientは見た目(当たった位置までの線)だけ描き、ダメージはHostの計算結果を受け取る。
            var hasAuthority = FortressNet.HasSimulationAuthority;

            HasImpact = nearest >= 0;
            ImpactCollider = nearest >= 0 ? HitBuffer[nearest].collider : null;
            if (nearest >= 0)
            {
                var hit = HitBuffer[nearest];
                endPoint = hit.point;
                ImpactPoint = new Vector3(hit.point.x, hit.point.y, origin.z);
                ImpactNormal = hit.normal;

                var target = hasAuthority ? hit.collider.GetComponentInParent<ILaserTarget>() : null;
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

            // 群衆がHostに従うClient(IsReplica)なら、ダメージ0で被弾フラッシュ(見た目)だけ出す(SwarmSystem側で0にする)。
            var swarm = SwarmSystem.Current;
            if (swarm != null && (hasAuthority || swarm.IsReplica))
            {
                var tuning = _turret.tuning;
                swarm.QueueBeam(
                    origin,
                    endPoint,
                    _turret.CurrentThicknessMeters * 0.5f,
                    tuning.maxDamagePerSecond * _turret.CurrentThickness01,
                    tuning.pierceEnemies ? tuning.maxPierceCount : 1,
                    _turret.playerIndex);
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
