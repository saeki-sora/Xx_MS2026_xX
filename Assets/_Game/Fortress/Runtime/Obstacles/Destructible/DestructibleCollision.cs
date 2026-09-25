using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 破壊可能物の当たり判定と、敵の経路（NavigationObstacle）への影響を担当する。
    /// 壊れると判定を消して経路を開き、再生すると塞ぎ直す。瓦礫を残す設定なら壊れた後は通行コスト地帯になる。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DestructibleCollision : MonoBehaviour
    {
        private DestructibleObstacle _obstacle;
        private NavigationObstacle _navigation;
        private readonly List<Collider2D> _colliders = new List<Collider2D>();
        private readonly Dictionary<Collider2D, bool> _originalTriggers = new Dictionary<Collider2D, bool>();

        /// <summary>設定の形に合う当たり判定を用意する。無ければ作り、形が違えば作り替える（Customなら何もしない）。</summary>
        public void EnsureShape()
        {
            var obstacle = GetComponent<DestructibleObstacle>();
            var shape = obstacle != null ? obstacle.settings.collision.shape : DestructibleShape.Box;
            if (shape == DestructibleShape.Custom)
            {
                return;
            }

            foreach (var existing in GetComponents<Collider2D>())
            {
                if (MatchesShape(existing, shape))
                {
                    return;
                }
            }

            foreach (var existing in GetComponents<Collider2D>())
            {
                DestroyComponent(existing);
            }

            switch (shape)
            {
                case DestructibleShape.Circle:
                    gameObject.AddComponent<CircleCollider2D>().radius = 0.5f;
                    break;
                case DestructibleShape.Capsule:
                    gameObject.AddComponent<CapsuleCollider2D>().size = Vector2.one;
                    break;
                default:
                    gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                    break;
            }
        }

        private void Awake()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            _navigation = GetComponent<NavigationObstacle>();
            EnsureShape();
            CacheColliders();
        }

        private void OnEnable()
        {
            if (_obstacle == null)
            {
                _obstacle = GetComponent<DestructibleObstacle>();
                _navigation = GetComponent<NavigationObstacle>();
            }

            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Destroyed += OnStateChanged;
            _obstacle.Regenerated += OnStateChanged;
            _obstacle.SettingsChanged += OnSettingsChanged;
            Apply();
        }

        private void OnDisable()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Destroyed -= OnStateChanged;
            _obstacle.Regenerated -= OnStateChanged;
            _obstacle.SettingsChanged -= OnSettingsChanged;
        }

        private void Reset()
        {
            EnsureShape();
        }

        private void OnStateChanged(DestructibleObstacle obstacle)
        {
            Apply();
        }

        private void OnSettingsChanged()
        {
#if UNITY_EDITOR
            // OnValidateの最中は生成・破棄ができないので、少し待ってから反映する。
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    EnsureShape();
                    CacheColliders();
                    Apply();
                }
            };
#else
            Apply();
#endif
        }

        private void CacheColliders()
        {
            GetComponentsInChildren(true, _colliders);
            foreach (var collider in _colliders)
            {
                if (!_originalTriggers.ContainsKey(collider))
                {
                    _originalTriggers[collider] = collider.isTrigger;
                }
            }
        }

        private void Apply()
        {
            if (_colliders.Count == 0)
            {
                CacheColliders();
            }

            var settings = _obstacle.settings.collision;

            if (!_obstacle.IsDestroyed)
            {
                SetColliders(true, false);
                _navigation.mode = NavigationObstacleMode.Block;
                _navigation.enabled = settings.blocksEnemies;
            }
            else if (settings.leavesRubble)
            {
                SetColliders(true, true);
                _navigation.mode = NavigationObstacleMode.Cost;
                _navigation.costMultiplier = settings.rubbleCostMultiplier;
                _navigation.speedMultiplier = settings.rubbleSpeedMultiplier;
                _navigation.enabled = true;
            }
            else
            {
                SetColliders(false, false);
            }

            NavigationObstacle.NotifyChanged();
        }

        /// <param name="enabled">当たり判定を有効にするか。</param>
        /// <param name="trigger">trueなら、物理的には素通りできるトリガーにする（レーザーも貫通する）。</param>
        private void SetColliders(bool enabled, bool trigger)
        {
            foreach (var collider in _colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = enabled;
                collider.isTrigger = trigger || (_originalTriggers.TryGetValue(collider, out var original) && original);
            }
        }

        private static bool MatchesShape(Collider2D collider, DestructibleShape shape)
        {
            return shape switch
            {
                DestructibleShape.Box => collider is BoxCollider2D,
                DestructibleShape.Circle => collider is CircleCollider2D,
                DestructibleShape.Capsule => collider is CapsuleCollider2D,
                _ => true
            };
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }
    }
}
