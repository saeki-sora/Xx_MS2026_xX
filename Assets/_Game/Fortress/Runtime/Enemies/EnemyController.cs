using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵の最小実装。コアを目指して進み、接触するとダメージを与えて消滅する。
    /// 進行方向はEnemyNavigation.Current（経路探索）に問い合わせ、無ければ目標へ直進する。
    /// </summary>
    public sealed class EnemyController : MonoBehaviour, ILaserTarget
    {
        public EnemyTypeDefinition definition;
        public Transform target;

        public event Action<EnemyController> OnDeath;

        private float _currentHealth;
        private Vector2 _moveDirection;
        private const float ArrivalDistance = 0.2f;

        private void Start()
        {
            _currentHealth = definition != null ? definition.maxHealth : 10f;

            if (target == null)
            {
                var core = FindFirstObjectByType<CoreCrystalController>();
                if (core != null)
                {
                    target = core.transform;
                }
            }

            ApplyPlaceholderVisual();
            EnsureHitCollider();
        }

        private void Update()
        {
            if (target == null || definition == null)
            {
                return;
            }

            var position = (Vector2)transform.position;
            var toTarget = (Vector2)target.position - position;

            if (toTarget.magnitude <= ArrivalDistance)
            {
                target.GetComponent<CoreCrystalController>()?.TakeDamage(definition.damageToCore);
                Destroy(gameObject);
                return;
            }

            var navigator = EnemyNavigation.Current;
            var desired = toTarget.normalized;
            if (navigator != null)
            {
                var navigated = navigator.GetDirection(position, definition.navigationProfile);
                if (navigated.sqrMagnitude > 1e-6f)
                {
                    desired = navigated;
                }
            }

            var blend = 1f - Mathf.Exp(-definition.turnSharpness * Time.deltaTime);
            _moveDirection = _moveDirection.sqrMagnitude < 1e-6f
                ? desired
                : Vector2.Lerp(_moveDirection, desired, blend);
            if (_moveDirection.sqrMagnitude > 1e-6f)
            {
                _moveDirection.Normalize();
            }

            var speed = definition.moveSpeed * (navigator != null ? navigator.GetSpeedMultiplier(position) : 1f);
            transform.position += (Vector3)(_moveDirection * speed * Time.deltaTime);
        }

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            TakeDamage(baseDamage);
        }

        public void TakeDamage(float amount)
        {
            if (_currentHealth <= 0f)
            {
                return;
            }

            _currentHealth -= amount;

            if (_currentHealth <= 0f)
            {
                OnDeath?.Invoke(this);
                Destroy(gameObject);
            }
        }

        private void ApplyPlaceholderVisual()
        {
            if (definition == null || definition.visualPrefab != null || GetComponentInChildren<SpriteRenderer>() != null)
            {
                return;
            }

            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite();
            spriteRenderer.color = definition.placeholderColor;
        }

        /// <summary>
        /// レーザーのRaycast判定(LaserBeamVisual)が敵に当たるためには何らかのCollider2Dが要る。
        /// 本番プレファブに専用のコライダーが無いケースの保険として、無ければ円形を追加する。
        /// </summary>
        private void EnsureHitCollider()
        {
            if (GetComponent<Collider2D>() != null)
            {
                return;
            }

            gameObject.AddComponent<CircleCollider2D>();
        }
    }
}
