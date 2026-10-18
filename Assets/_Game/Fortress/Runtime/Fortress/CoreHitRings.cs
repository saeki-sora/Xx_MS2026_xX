using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアがダメージを受けた瞬間に、コアから警告の光の輪を広げる（設定は <see cref="CoreCrystalController.hitRing"/>）。
    /// <see cref="CoreCrystalController"/> が起動時に自動で付ける。HP は Client にも届く（<see cref="CoreCrystalController.OnHealthChanged"/>）ので、全員の画面に出る。
    /// 輪は地面に寝た四角形（Resources/Fortress/CoreHitRing.shader）を使い回す。Play中のシーンの物として作るので、Playを止めれば消える。
    /// 既存のダメージの演出（エフェクト欄 onDamaged・カメラの揺れ・画面の赤さ）とは別に重ねて出る。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreCrystalController))]
    public sealed class CoreHitRings : MonoBehaviour
    {
        private const string ShaderResourcePath = "Fortress/CoreHitRing";
        private const int PoolSize = 6;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int InnerId = Shader.PropertyToID("_Inner");
        private static readonly int WidthId = Shader.PropertyToID("_Width");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class Ring
        {
            public MeshRenderer renderer;
            public float age;
            public float duration;
        }

        private static Material _material;
        private static Mesh _quad;

        private CoreCrystalController _core;
        private readonly List<Ring> _rings = new List<Ring>();
        private MaterialPropertyBlock _block;
        private Transform _root;
        private float _lastHealth = -1f;
        private float _lastRingTime = float.NegativeInfinity;
        private int _next;

        private void Awake()
        {
            _core = GetComponent<CoreCrystalController>();
        }

        private void Start()
        {
            _lastHealth = _core.CurrentHealth;
        }

        private void OnEnable()
        {
            _core.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            _core.OnHealthChanged -= HandleHealthChanged;
            foreach (var ring in _rings)
            {
                if (ring.renderer != null)
                {
                    ring.renderer.enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        /// <summary>要塞デザイナーの「試す」ボタン用。間隔の制限なしで輪を1つ出す。</summary>
        public void PlayHit()
        {
            Spawn();
        }

        private void HandleHealthChanged(float current, float max)
        {
            var hit = _lastHealth >= 0f && current < _lastHealth - 0.0001f;
            _lastHealth = current;
            var settings = _core.hitRing;
            if (!hit || settings == null || !settings.enabled || Time.time - _lastRingTime < settings.minInterval)
            {
                return;
            }

            Spawn();
        }

        private void Spawn()
        {
            var settings = _core.hitRing;
            if (settings == null || !EnsureAssets())
            {
                return;
            }

            _lastRingTime = Time.time;
            var ring = Rent();
            ring.age = 0f;
            ring.duration = Mathf.Max(0.05f, settings.seconds);

            var radius = Mathf.Max(0.5f, settings.radius);
            var position = transform.position;
            // カメラは +Z を向いているので、床より少しだけ手前(-Z)に置く。
            ring.renderer.transform.SetPositionAndRotation(new Vector3(position.x, position.y, position.z - 0.03f), Quaternion.identity);
            ring.renderer.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            Apply(ring, settings, radius);
            ring.renderer.enabled = true;
        }

        private void LateUpdate()
        {
            var settings = _core.hitRing;
            if (settings == null)
            {
                return;
            }

            var radius = Mathf.Max(0.5f, settings.radius);
            for (var i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.renderer == null || !ring.renderer.enabled)
                {
                    continue;
                }

                ring.age += Time.deltaTime;
                if (ring.age >= ring.duration)
                {
                    ring.renderer.enabled = false;
                    continue;
                }

                Apply(ring, settings, radius);
            }
        }

        private void Apply(Ring ring, CoreHitRingSettings settings, float radius)
        {
            ring.renderer.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, ring.age / ring.duration);
            _block.SetFloat(InnerId, Mathf.Clamp01(CoreVisualRadius() / radius));
            _block.SetFloat(WidthId, settings.width / radius);
            _block.SetColor(ColorId, new Color(settings.color.r, settings.color.g, settings.color.b, settings.brightness));
            ring.renderer.SetPropertyBlock(_block);
        }

        private float CoreVisualRadius()
        {
            var renderer = TryGetComponent<SpriteRenderer>(out var own) ? own : GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                return 0.5f;
            }

            var extents = renderer.bounds.extents;
            return Mathf.Max(extents.x, extents.y);
        }

        private Ring Rent()
        {
            _rings.RemoveAll(r => r.renderer == null);
            foreach (var ring in _rings)
            {
                if (!ring.renderer.enabled)
                {
                    return ring;
                }
            }

            if (_rings.Count < PoolSize)
            {
                var ring = new Ring { renderer = CreateRenderer() };
                _rings.Add(ring);
                return ring;
            }

            _next = (_next + 1) % _rings.Count;
            return _rings[_next];
        }

        private MeshRenderer CreateRenderer()
        {
            if (_root == null)
            {
                _root = new GameObject("[Core] HitRings").transform;
            }

            var go = new GameObject("HitRing");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private bool EnsureAssets()
        {
            _block ??= new MaterialPropertyBlock();
            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    return false;
                }

                _material = new Material(shader) { name = "CoreHitRing (runtime)", hideFlags = HideFlags.DontSave };
            }

            if (_quad == null)
            {
                _quad = new Mesh
                {
                    name = "Fortress_CoreHitRingQuad",
                    hideFlags = HideFlags.DontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                    },
                    triangles = new[] { 0, 2, 1, 2, 3, 1 }
                };
                _quad.RecalculateBounds();
            }

            return true;
        }
    }
}
