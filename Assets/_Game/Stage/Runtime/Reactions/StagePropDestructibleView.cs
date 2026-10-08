using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 「壊せる壁」の3Dモデルの見た目を、破壊可能物（DestructibleObstacle）の状態に合わせる。
    /// 被弾で光る → 耐久が減るほど暗く → 壊れると溶けるように消える → 再生すると溶けた状態から現れる。
    /// 耐久・当たり判定・通路・ネット同期・演出(SE/VFX)は破壊可能物の仕組みがそのまま担当する（ここは見た目だけ）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StagePropRenderer))]
    public sealed class StagePropDestructibleView : MonoBehaviour
    {
        private DestructibleObstacle _obstacle;
        private StagePropRenderer _renderer;
        private StagePropHeat _heat;
        private float _flash;
        private float _dissolveTarget;
        private bool _animating;

        private void Awake()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            _renderer = GetComponent<StagePropRenderer>();
            _heat = GetComponent<StagePropHeat>();
        }

        private void OnEnable()
        {
            if (_obstacle == null)
            {
                // 破壊可能物の部品が無い（役割が「壊せる壁」でない）ときは何もしない。
                enabled = false;
                return;
            }

            _obstacle.Damaged += OnDamaged;
            _obstacle.HealthChanged += OnHealthChanged;
            _obstacle.Destroyed += OnDestroyed;
            _obstacle.Regenerated += OnRegenerated;
            SnapToState();
        }

        private void OnDisable()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Damaged -= OnDamaged;
            _obstacle.HealthChanged -= OnHealthChanged;
            _obstacle.Destroyed -= OnDestroyed;
            _obstacle.Regenerated -= OnRegenerated;
        }

        private void OnDamaged(DestructibleObstacle obstacle, float amount, DamageSource source)
        {
            _flash = 1f;
            _animating = true;
        }

        private void OnHealthChanged(DestructibleObstacle obstacle)
        {
            _renderer.Look.Darken = (1f - obstacle.Health01) * StageLookProfile.Current.darkenAtZeroHealth;
            _renderer.MarkDirty();
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            _dissolveTarget = 1f;
            _animating = true;
        }

        private void OnRegenerated(DestructibleObstacle obstacle)
        {
            if (_heat != null)
            {
                _heat.ClearMarks();
            }
            _renderer.SetVisible(true);
            _renderer.Look.Dissolve = Mathf.Max(_renderer.Look.Dissolve, 0.999f);
            _dissolveTarget = 0f;
            _animating = true;
            OnHealthChanged(obstacle);
        }

        /// <summary>読み込み直後・ネット参加直後などに、アニメなしで今の状態の見た目にする。</summary>
        private void SnapToState()
        {
            var destroyed = _obstacle.IsDestroyed;
            var look = _renderer.Look;
            look.Dissolve = destroyed ? 1f : 0f;
            look.Darken = (1f - _obstacle.Health01) * StageLookProfile.Current.darkenAtZeroHealth;
            _dissolveTarget = look.Dissolve;
            _renderer.SetVisible(!destroyed);
            _renderer.MarkDirty();
        }

        private void Update()
        {
            if (!_animating)
            {
                return;
            }

            var profile = StageLookProfile.Current;
            var look = _renderer.Look;

            _flash = profile.hitFlashSeconds > 0f ? Mathf.Max(0f, _flash - Time.deltaTime / profile.hitFlashSeconds) : 0f;
            look.Flash = _flash;
            look.FlashColor = profile.hitFlashColor;
            look.DissolveColor = profile.dissolveEdgeColor;
            look.Dissolve = Mathf.MoveTowards(look.Dissolve, _dissolveTarget, Time.deltaTime / profile.dissolveSeconds);

            if (look.Dissolve >= 1f && _dissolveTarget >= 1f)
            {
                _renderer.SetVisible(false);
            }

            _animating = _flash > 0f || !Mathf.Approximately(look.Dissolve, _dissolveTarget);
            _renderer.MarkDirty();
        }
    }
}
