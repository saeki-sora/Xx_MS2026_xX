using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 破壊可能物の見た目を組み立てて切り替える。見た目はPrefab（2Dスプライト/3Dモデル/アニメ付き何でも）を子として生成し、
    /// 状態（通常・ダメージ段階・破壊後）に応じて表示を切り替える。Prefabが空ならルートの仮の四角を使う。
    /// 生成した見た目はシーンに保存されず、読み込みのたびに設定から作り直す。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DestructibleVisual : MonoBehaviour
    {
        private const string ContainerName = "[DestructibleVisual]";
        private const float DarkestBrightness = 0.35f;
        private const float ShakeSeconds = 0.1f;

        private DestructibleObstacle _obstacle;
        private Transform _container;
        private DestructibleVisualNode _placeholder;
        private DestructibleVisualNode _base;
        private DestructibleVisualNode _destroyed;
        private readonly List<DestructibleVisualNode> _stages = new List<DestructibleVisualNode>();
        private Vector3 _lastScale;
        private float _flashRemaining;
        private float _shakeRemaining;
        private bool _rebuildQueued;

        private void OnEnable()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.HealthChanged += OnStateChanged;
            _obstacle.StageChanged += OnStageChanged;
            _obstacle.Destroyed += OnStateChanged;
            _obstacle.Regenerated += OnStateChanged;
            _obstacle.Damaged += OnDamaged;
            _obstacle.SettingsChanged += QueueRebuild;

            if (Application.isPlaying)
            {
                Rebuild();
            }
            else
            {
                QueueRebuild();
            }
        }

        private void OnDisable()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.HealthChanged -= OnStateChanged;
            _obstacle.StageChanged -= OnStageChanged;
            _obstacle.Destroyed -= OnStateChanged;
            _obstacle.Regenerated -= OnStateChanged;
            _obstacle.Damaged -= OnDamaged;
            _obstacle.SettingsChanged -= QueueRebuild;

            DestroyBuiltVisuals();
            _placeholder?.SetVisible(true);
        }

        private void Update()
        {
            if (_obstacle == null || _container == null)
            {
                return;
            }

            if (transform.lossyScale != _lastScale)
            {
                FitAll();
            }

            if (Application.isPlaying)
            {
                UpdateHitReaction();
            }
        }

        private void OnStateChanged(DestructibleObstacle obstacle)
        {
            Refresh();
        }

        private void OnStageChanged(DestructibleObstacle obstacle, int stage)
        {
            Refresh();
        }

        private void OnDamaged(DestructibleObstacle obstacle, float amount, DamageSource source)
        {
            var visual = _obstacle.settings.visual;
            _flashRemaining = visual.hitFlashSeconds;
            _shakeRemaining = visual.hitShake > 0f ? ShakeSeconds : 0f;
        }

        private void QueueRebuild()
        {
#if UNITY_EDITOR
            // OnValidateの最中や読み込み中は生成・破棄ができないので、少し待ってから作り直す。
            if (_rebuildQueued)
            {
                return;
            }

            _rebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _rebuildQueued = false;
                if (this != null && isActiveAndEnabled)
                {
                    Rebuild();
                }
            };
#else
            Rebuild();
#endif
        }

        private void Rebuild()
        {
            DestroyBuiltVisuals();

            var visual = _obstacle.settings.visual;
            _placeholder = BuildPlaceholder(visual);

            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);
            _container.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            _base = BuildPrefabNode(visual.visualPrefab);
            _destroyed = BuildPrefabNode(visual.destroyedPrefab);
            foreach (var stage in visual.damageStages)
            {
                _stages.Add(stage != null ? BuildPrefabNode(stage.visualPrefab) : null);
            }

            _placeholder.ApplySortingOffset(visual.sortingOrderOffset);
            _lastScale = transform.lossyScale;
            Refresh();
        }

        private DestructibleVisualNode BuildPlaceholder(DestructibleVisualSettings visual)
        {
            var placeholderVisual = GetComponent<PlaceholderVisual>();
            if (placeholderVisual != null)
            {
                if (visual.controlPlaceholderColor)
                {
                    placeholderVisual.color = visual.placeholderColor;
                }

                placeholderVisual.Apply();
            }

            return new DestructibleVisualNode(null, GetComponents<SpriteRenderer>());
        }

        private DestructibleVisualNode BuildPrefabNode(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, _container);
            instance.name = prefab.name;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            var visual = _obstacle.settings.visual;
            DestructibleVisualFitter.Fit(instance.transform, visual.fitMode, visual.visualOffset);

            var node = new DestructibleVisualNode(instance, CollectRenderers(instance));
            node.ApplySortingOffset(visual.sortingOrderOffset);
            return node;
        }

        private static Renderer[] CollectRenderers(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) && !(renderer is LineRenderer))
                {
                    result.Add(renderer);
                }
            }

            return result.ToArray();
        }

        private void FitAll()
        {
            var visual = _obstacle.settings.visual;
            foreach (var node in EnumerateNodes())
            {
                if (node.Transform != null)
                {
                    DestructibleVisualFitter.Fit(node.Transform, visual.fitMode, visual.visualOffset);
                }
            }

            _lastScale = transform.lossyScale;
        }

        private void DestroyBuiltVisuals()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name == ContainerName)
                {
                    child.gameObject.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            _container = null;
            _base = null;
            _destroyed = null;
            _stages.Clear();
        }

        /// <summary>現在の状態に合う見た目だけを表示し、色を反映する。</summary>
        private void Refresh()
        {
            if (_container == null)
            {
                return;
            }

            var visual = _obstacle.settings.visual;
            var tint = Color.white;
            DestructibleVisualNode target;

            if (_obstacle.IsDestroyed)
            {
                if (_destroyed != null)
                {
                    target = _destroyed;
                }
                else
                {
                    target = visual.destroyedGhostAlpha > 0f ? BaseNode() : null;
                    tint = new Color(1f, 1f, 1f, visual.destroyedGhostAlpha);
                }
            }
            else
            {
                target = StageNode(_obstacle.StageIndex) ?? BaseNode();
                if (visual.darkenWithDamage)
                {
                    var brightness = Mathf.Lerp(DarkestBrightness, 1f, _obstacle.Health01);
                    tint = new Color(brightness, brightness, brightness, 1f);
                }

                if (_flashRemaining > 0f && visual.hitFlashSeconds > 0f)
                {
                    tint = Color.Lerp(tint, visual.hitFlashColor, _flashRemaining / visual.hitFlashSeconds);
                }
            }

            foreach (var node in EnumerateNodes())
            {
                node.SetVisible(node == target);
            }

            target?.ApplyTint(tint);
        }

        private void UpdateHitReaction()
        {
            var visual = _obstacle.settings.visual;

            if (_flashRemaining > 0f)
            {
                _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.deltaTime);
                Refresh();
            }

            if (_shakeRemaining > 0f)
            {
                _shakeRemaining = Mathf.Max(0f, _shakeRemaining - Time.deltaTime);
                _container.localPosition = _shakeRemaining > 0f
                    ? (Vector3)(Random.insideUnitCircle * visual.hitShake)
                    : Vector3.zero;
            }
        }

        private DestructibleVisualNode BaseNode()
        {
            return _base ?? _placeholder;
        }

        private DestructibleVisualNode StageNode(int index)
        {
            return index >= 0 && index < _stages.Count ? _stages[index] : null;
        }

        private IEnumerable<DestructibleVisualNode> EnumerateNodes()
        {
            if (_placeholder != null)
            {
                yield return _placeholder;
            }

            if (_base != null)
            {
                yield return _base;
            }

            if (_destroyed != null)
            {
                yield return _destroyed;
            }

            foreach (var stage in _stages)
            {
                if (stage != null)
                {
                    yield return stage;
                }
            }
        }
    }
}
