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

        [Tooltip("ONなら、ビームの線をプレイヤー色にする。OFFなら下の「ビームの色」をそのまま使う（歯磨き粉の白など）。")]
        public bool usePlayerColor = true;

        [Tooltip("「プレイヤー色を使う」がOFFのときのビームの色。")]
        public Color beamColor = Color.white;

        [Tooltip("ビームの線に使うマテリアル。空ならUnityの標準のまま。自動で追加した線にだけ適用される。")]
        public Material beamMaterial;

        [Header("歯磨き粉のようなグニャグニャ（見た目だけ。当たり判定は直線のまま）")]
        [Tooltip("ビームの線が横にうねる大きさ（メートル）。0ならまっすぐな線のまま。")]
        public float wobbleAmplitude;

        [Tooltip("うねりの波の長さ（メートル）。小さいほど細かくうねる。")]
        public float wobbleWavelength = 3f;

        [Tooltip("うねりが流れる速さ。")]
        public float wobbleSpeed = 4f;

        [Tooltip("線の太さが場所ごとにふくらむ割合（0〜1）。0なら一定の太さ。")]
        [Range(0f, 1f)]
        public float bulgeAmount = 0.35f;

        [Tooltip("線を分ける数。多いほどなめらか。")]
        [Min(2)]
        public int wobbleSegments = 40;

        [Header("ビームの光（まわりのぼんやりした光。見た目だけ）")]
        [Tooltip("光の線に使うマテリアル（加算のにじむ光がおすすめ）。空なら光は出さない。")]
        public Material glowMaterial;

        [Tooltip("光の層の数。多いほどなめらかににじむ。")]
        [Range(0, 4)]
        public int glowLayers = 2;

        [Tooltip("一番外側の光の太さ（ビームの太さの何倍か）。")]
        public float glowWidthScale = 3f;

        [Tooltip("光の濃さ（0〜1）。")]
        [Range(0f, 1f)]
        public float glowAlpha = 0.18f;

        private LineRenderer[] _glowLines;
        private Vector3[] _glowBuffer;

        private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[16];

        private LaserTurret _turret;

        /// <summary>このフレーム、レーザーが何か（壁・破壊可能物など、トリガー以外）に当たっているか。着弾点の演出が使う。</summary>
        public bool HasImpact { get; private set; }

        /// <summary>当たっている場所（<see cref="HasImpact"/> のときだけ意味がある）。</summary>
        public Vector3 ImpactPoint { get; private set; }

        /// <summary>当たった面の向き（砲台側を向く）。</summary>
        public Vector2 ImpactNormal { get; private set; }

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                if (beamMaterial != null)
                {
                    lineRenderer.sharedMaterial = beamMaterial;
                }
            }
        }

        private Vector3[] _wobblePoints;

        private void EnsureGlow()
        {
            if (glowMaterial == null || glowLayers <= 0 || _glowLines != null)
            {
                return;
            }

            _glowLines = new LineRenderer[glowLayers];
            for (var i = 0; i < glowLayers; i++)
            {
                var go = new GameObject("BeamGlow" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.textureMode = LineTextureMode.Stretch;
                line.sharedMaterial = glowMaterial;
                line.sortingOrder = lineRenderer.sortingOrder - 1;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _glowLines[i] = line;
            }
        }

        private void SetGlowVisible(bool visible)
        {
            if (_glowLines == null)
            {
                return;
            }

            for (var i = 0; i < _glowLines.Length; i++)
            {
                _glowLines[i].enabled = visible;
            }
        }

        private void SyncGlow(Color color)
        {
            EnsureGlow();
            if (_glowLines == null)
            {
                return;
            }

            var count = lineRenderer.positionCount;
            if (_glowBuffer == null || _glowBuffer.Length < count)
            {
                _glowBuffer = new Vector3[count];
            }

            lineRenderer.GetPositions(_glowBuffer);
            for (var i = 0; i < _glowLines.Length; i++)
            {
                // 内側ほど細く、外側ほど太く。内側の層ほど濃い。
                var t = (i + 1f) / _glowLines.Length;
                var line = _glowLines[i];
                line.enabled = true;
                line.positionCount = count;
                line.SetPositions(_glowBuffer);
                var scale = Mathf.Lerp(1.4f, glowWidthScale, t);
                line.widthCurve = lineRenderer.widthCurve;
                line.widthMultiplier = lineRenderer.widthMultiplier * scale;
                var c = new Color(color.r, color.g, color.b, glowAlpha * (1f - t * 0.6f));
                line.startColor = c;
                line.endColor = c;
            }
        }

        private void ApplyWobble(Vector3 origin, Vector3 end)
        {
            var count = Mathf.Max(2, wobbleSegments) + 1;
            if (_wobblePoints == null || _wobblePoints.Length != count)
            {
                _wobblePoints = new Vector3[count];
                lineRenderer.positionCount = count;
            }

            var delta = end - origin;
            var length = delta.magnitude;
            var dir = length > 0.0001f ? delta / length : Vector3.right;
            var perp = new Vector3(-dir.y, dir.x, 0f);
            var wavelength = Mathf.Max(0.05f, wobbleWavelength);
            var time = Time.time * wobbleSpeed;
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)(count - 1);
                var dist = t * length;
                // 根元と先端は線に合わせ、中ほどほど大きくうねらせる。
                var envelope = Mathf.Sin(t * Mathf.PI);
                var phase = dist / wavelength * Mathf.PI * 2f;
                var offset = (Mathf.Sin(phase - time) + 0.5f * Mathf.Sin(phase * 1.7f + time * 1.3f)) * wobbleAmplitude * envelope;
                _wobblePoints[i] = origin + dir * dist + perp * offset;
            }

            lineRenderer.SetPositions(_wobblePoints);

            // 歯磨き粉のように、太さも場所ごとにふくらんでうねる。
            var width = _turret.CurrentThicknessMeters;
            lineRenderer.widthMultiplier = 1f;
            if (_bulgeCurve == null)
            {
                _bulgeCurve = new AnimationCurve();
            }

            _bulgeCurve.keys = BuildBulgeKeys(count, width, time);
            lineRenderer.widthCurve = _bulgeCurve;
        }

        private AnimationCurve _bulgeCurve;
        private Keyframe[] _bulgeKeys;

        private Keyframe[] BuildBulgeKeys(int segments, float width, float time)
        {
            const int Keys = 8;
            if (_bulgeKeys == null)
            {
                _bulgeKeys = new Keyframe[Keys];
            }

            for (var i = 0; i < Keys; i++)
            {
                var t = i / (float)(Keys - 1);
                var bulge = 1f + Mathf.Sin(t * 12f - time * 0.8f + i) * bulgeAmount * 0.5f;
                _bulgeKeys[i] = new Keyframe(t, width * bulge);
            }

            return _bulgeKeys;
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
                SetGlowVisible(false);
                HasImpact = false;
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
            if (wobbleAmplitude > 0f)
            {
                ApplyWobble(origin, endPoint);
            }
            else
            {
                lineRenderer.startWidth = _turret.CurrentThicknessMeters;
                lineRenderer.endWidth = _turret.CurrentThicknessMeters;
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, endPoint);
            }

            var color = usePlayerColor ? FortressColors.PlayerColor(_turret.playerIndex) : beamColor;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            SyncGlow(color);
        }
    }
}
