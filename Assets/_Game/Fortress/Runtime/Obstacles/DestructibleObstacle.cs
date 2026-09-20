using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザーで破壊でき、時間経過で再生する障害物。破壊・再生のたびにColliderの有効/無効を切り替えて
    /// NavigationObstacle経由で経路を再計算させる。見た目はSpriteRendererの色で耐久を表現する。
    /// </summary>
    [RequireComponent(typeof(NavigationObstacle))]
    public sealed class DestructibleObstacle : MonoBehaviour, ILaserTarget
    {
        [Tooltip("耐久値。レーザーのダメージがこの値を超えると破壊される。")]
        [Min(1f)]
        public float maxHealth = 30f;

        [Tooltip("破壊された後、時間経過で元に戻るか。")]
        public bool regenerates = true;

        [Tooltip("破壊されてから再生するまでの時間(秒)。")]
        [Min(0f)]
        public float regenDelaySeconds = 10f;

        [Tooltip("破壊中に、元の位置に残る「跡」の濃さ。0で完全に消える。再生位置の目印になる。")]
        [Range(0f, 1f)]
        public float destroyedGhostAlpha = 0.15f;

        public event Action<DestructibleObstacle> Destroyed;
        public event Action<DestructibleObstacle> Regenerated;

        public float CurrentHealth { get; private set; }
        public bool IsDestroyed { get; private set; }

        /// <summary>再生までの進み具合(0-1)。破壊中のみ意味を持つ。</summary>
        public float RegenProgress01 => IsDestroyed && regenDelaySeconds > 0f
            ? Mathf.Clamp01(_regenTimer / regenDelaySeconds)
            : 0f;

        private Collider2D[] _colliders;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private float _regenTimer;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }

            RefreshVisual();
        }

        private void Update()
        {
            if (!IsDestroyed || !regenerates)
            {
                return;
            }

            _regenTimer += Time.deltaTime;
            if (_regenTimer >= regenDelaySeconds)
            {
                Regenerate();
            }
        }

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            var multiplier = tuning != null ? tuning.obstacleDamageMultiplier : 1f;
            TakeDamage(baseDamage * multiplier);
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            RefreshVisual();

            if (CurrentHealth <= 0f)
            {
                DestroyObstacle();
            }
        }

        public void Regenerate()
        {
            IsDestroyed = false;
            _regenTimer = 0f;
            CurrentHealth = maxHealth;
            SetCollidersEnabled(true);
            RefreshVisual();
            Regenerated?.Invoke(this);
        }

        private void DestroyObstacle()
        {
            IsDestroyed = true;
            _regenTimer = 0f;
            SetCollidersEnabled(false);
            RefreshVisual();
            Destroyed?.Invoke(this);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var collider in _colliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }

            NavigationObstacle.NotifyChanged();
        }

        private void RefreshVisual()
        {
            if (_renderers == null)
            {
                return;
            }

            var health01 = maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }

                var baseColor = _baseColors[i];
                if (IsDestroyed)
                {
                    baseColor.a *= destroyedGhostAlpha;
                    _renderers[i].color = baseColor;
                    continue;
                }

                var damaged = new Color(baseColor.r * 0.35f, baseColor.g * 0.35f, baseColor.b * 0.35f, baseColor.a);
                _renderers[i].color = Color.Lerp(damaged, baseColor, health01);
            }
        }
    }
}
