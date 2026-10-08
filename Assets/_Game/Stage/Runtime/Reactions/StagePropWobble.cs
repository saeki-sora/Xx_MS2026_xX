using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 敵の群れに押されると、押された向きと反対へ傾き、プルプルと戻る（見た目だけ。通路の形は変わらない）。
    /// 動かすのは「Visual（揺れの支点）」の回転と縦の縮みだけなので、どんなモデル・シェーダーでも使える。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StageProp))]
    public sealed class StagePropWobble : MonoBehaviour
    {
        private static readonly Vector3 Up = Vector3.back; // 床から上（カメラ側）は -Z

        private StageProp _prop;
        private DampedSpring2D _spring;
        private Vector2 _target;
        private float _lastCount;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private bool _hasBase;

        /// <summary>今の傾き（度。向き付き）。ツールの表示用。</summary>
        public Vector2 CurrentTilt => _spring.Value;

        /// <summary>直近に数えた、まわりの敵の数。ツールの表示用。</summary>
        public float LastPressure => _lastCount;

        /// <summary>試しに揺らす（ツールの「揺らしてみる」ボタン用）。</summary>
        public void Poke(Vector2 direction, float degrees)
        {
            _spring.Kick(ToLocal(direction).normalized * degrees * StageLookProfile.Current.stiffness * 0.1f);
        }

        /// <summary>床の上の向き（ワールド）を、このオブジェクトから見た向きに直す（オブジェクトはZ軸で回っていることがある）。</summary>
        private Vector2 ToLocal(Vector2 worldDirection)
        {
            var parent = _prop != null && _prop.visualRoot != null ? _prop.visualRoot.parent : transform;
            var local = parent != null ? parent.InverseTransformDirection(worldDirection) : (Vector3)worldDirection;
            return new Vector2(local.x, local.y);
        }

        internal bool TryGetPressureArea(out Bounds area)
        {
            var collider = _prop != null && _prop.footprint != null ? _prop.footprint.Collider : null;
            if (collider != null && collider.enabled && collider.pathCount > 0)
            {
                area = collider.bounds;
                return true;
            }

            area = default;
            return false;
        }

        internal void ReceivePressure(StageSwarmPressure.Sample sample)
        {
            var profile = StageLookProfile.Current;
            var strength = _prop.look.wobble ? _prop.look.wobbleStrength : 0f;
            var amount = Mathf.Clamp01(sample.Count / profile.pressureForFullTilt);
            var direction = ToLocal(sample.Direction);

            // 敵がいる向きの反対へ押される。
            _target = -direction * (amount * profile.maxTiltDegrees * strength);

            // 急に押し寄せたら、ぷるっと揺れのきっかけを足す。
            var surge = sample.Count - _lastCount;
            if (surge > profile.pressureForFullTilt * 0.25f && strength > 0f)
            {
                _spring.Kick(-direction * (profile.maxTiltDegrees * strength * 4f));
            }

            _lastCount = sample.Count;
        }

        private void Awake()
        {
            _prop = GetComponent<StageProp>();
        }

        private void OnEnable()
        {
            StageSwarmPressure.Register(this);
        }

        private void OnDisable()
        {
            StageSwarmPressure.Unregister(this);
            ResetPose();
        }

        private void LateUpdate()
        {
            var pivot = _prop != null ? _prop.visualRoot : null;
            if (pivot == null || !Application.isPlaying)
            {
                return;
            }

            if (!_hasBase)
            {
                _baseRotation = pivot.localRotation;
                _baseScale = pivot.localScale;
                _hasBase = true;
            }

            var profile = StageLookProfile.Current;
            _spring.Step(_target, profile.stiffness, profile.damping, Time.deltaTime);

            var tilt = _spring.Value;
            var degrees = tilt.magnitude;
            if (degrees < 0.001f && _spring.Velocity.sqrMagnitude < 1e-6f)
            {
                pivot.localRotation = _baseRotation;
                pivot.localScale = _baseScale;
                return;
            }

            var direction = new Vector3(tilt.x, tilt.y, 0f) / Mathf.Max(degrees, 1e-5f);
            var radians = degrees * Mathf.Deg2Rad;
            var tiltedUp = Up * Mathf.Cos(radians) + direction * Mathf.Sin(radians);
            pivot.localRotation = Quaternion.FromToRotation(Up, tiltedUp) * _baseRotation;

            var squash = profile.squash * Mathf.Clamp01(degrees / Mathf.Max(0.01f, profile.maxTiltDegrees));
            pivot.localScale = Vector3.Scale(_baseScale, new Vector3(1f + squash * 0.5f, 1f + squash * 0.5f, 1f - squash));
        }

        private void ResetPose()
        {
            if (_hasBase && _prop != null && _prop.visualRoot != null)
            {
                _prop.visualRoot.localRotation = _baseRotation;
                _prop.visualRoot.localScale = _baseScale;
            }

            _spring = default;
            _target = Vector2.zero;
        }
    }
}
