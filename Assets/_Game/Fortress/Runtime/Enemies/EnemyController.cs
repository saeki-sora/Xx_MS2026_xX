using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵の最小実装。コア目掛けて直進し、接触するとダメージを与えて消滅する。
    /// パス移動やノックバックなどの演出は将来フェーズで拡張する。
    /// </summary>
    public sealed class EnemyController : MonoBehaviour
    {
        public EnemyTypeDefinition definition;
        public Transform target;

        public event Action<EnemyController> OnDeath;

        private float _currentHealth;
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

            var toTarget = target.position - transform.position;
            var distance = toTarget.magnitude;

            if (distance <= ArrivalDistance)
            {
                target.GetComponent<CoreCrystalController>()?.TakeDamage(definition.damageToCore);
                Destroy(gameObject);
                return;
            }

            var step = (Vector2)toTarget.normalized * definition.moveSpeed * Time.deltaTime;
            transform.position += (Vector3)step;
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
