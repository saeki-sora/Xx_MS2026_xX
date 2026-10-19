using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆弾の衝撃波（地面が輪の形に波打って広がる）。爆発ごとに四角形を1枚置き、Resources/Fortress/BombShockwave.shader で描く。
    /// 四角形は使い回す（同時に出せる数に上限あり。超えたら一番古いものを使う）。Play中のシーンの物として作るので、Playを止めれば消える。
    /// </summary>
    internal sealed class BombShockwaves : IDisposable
    {
        private const string ShaderResourcePath = "Fortress/BombShockwave";
        private const string RootName = "[Fortress] BombShockwaves";
        private const float Extent = 1.25f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int ExtentId = Shader.PropertyToID("_Extent");
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private sealed class Wave
        {
            public MeshRenderer renderer;
            public float age;
            public float duration;
            public float strength;
            public Color tint;
        }

        private readonly int _capacity;
        private readonly List<Wave> _waves = new List<Wave>();
        private readonly Material _material;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Transform _root;
        private int _next;

        public BombShockwaves(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            var shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            _quad = BuildQuad();
        }

        public void Spawn(Vector2 center, float radius, float strength, float duration, Color tint)
        {
            if (_material == null || radius <= 0f || duration <= 0f)
            {
                return;
            }

            var wave = Rent();
            if (wave == null)
            {
                return;
            }

            wave.age = 0f;
            wave.duration = duration;
            wave.strength = strength;
            wave.tint = tint;
            var size = radius * Extent * 2f;
            // カメラは +Z を向いているので、床(z=0)より少しだけ手前(-Z)に置く。
            wave.renderer.transform.SetPositionAndRotation(new Vector3(center.x, center.y, -0.02f), Quaternion.identity);
            wave.renderer.transform.localScale = new Vector3(size, size, 1f);
            Apply(wave);
            wave.renderer.enabled = true;
        }

        public void Update(float deltaTime)
        {
            for (var i = 0; i < _waves.Count; i++)
            {
                var wave = _waves[i];
                if (wave.renderer == null || !wave.renderer.enabled)
                {
                    continue;
                }

                wave.age += deltaTime;
                if (wave.age >= wave.duration)
                {
                    wave.renderer.enabled = false;
                    continue;
                }

                Apply(wave);
            }
        }

        private void Apply(Wave wave)
        {
            var t = Mathf.Clamp01(wave.age / wave.duration);
            // 速く広がって、ゆっくり止まる。
            var progress = 1f - (1f - t) * (1f - t);
            wave.renderer.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, progress);
            _block.SetFloat(StrengthId, wave.strength);
            _block.SetFloat(ExtentId, Extent);
            _block.SetColor(TintId, wave.tint);
            wave.renderer.SetPropertyBlock(_block);
        }

        private Wave Rent()
        {
            // 場面の切り替えで消えた物は作り直す。
            _waves.RemoveAll(w => w.renderer == null);
            for (var i = 0; i < _waves.Count; i++)
            {
                if (!_waves[i].renderer.enabled)
                {
                    return _waves[i];
                }
            }

            if (_waves.Count < _capacity)
            {
                var wave = new Wave { renderer = CreateRenderer() };
                _waves.Add(wave);
                return wave;
            }

            if (_waves.Count == 0)
            {
                return null;
            }

            _next = (_next + 1) % _waves.Count;
            return _waves[_next];
        }

        private MeshRenderer CreateRenderer()
        {
            if (_root == null)
            {
                _root = new GameObject(RootName).transform;
            }

            var go = new GameObject("Shockwave");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }

            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
            }

            if (_quad != null)
            {
                UnityEngine.Object.Destroy(_quad);
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh
            {
                name = "Fortress_ShockwaveQuad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
