using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// ビルボードの適用・解除の中核(MonoBehaviourではない)。設定に従って対象を集め、マテリアルを差し替え、シェーダーのグローバル値を設定する。
    /// 設定がOFF(またはnull)なら全て元に戻す。ゲーム中は BillboardDirector が、エディタのプレビューは描画の前後だけ使う。
    /// </summary>
    public sealed class BillboardController : IDisposable
    {
        private const string ShaderResourcePath = "Fortress/BillboardSprite";
        private const string ShaderName = "MS2026/Fortress/BillboardSprite";

        private readonly List<IBillboardTargetSource> _sources;
        private readonly BillboardMaterialSwapper _swapper = new BillboardMaterialSwapper();
        private readonly List<SpriteRenderer> _buffer = new List<SpriteRenderer>();
        private Material _material;
        private bool _missingShaderLogged;

        public BillboardController()
            : this(BillboardTargetSources.CreateDefault())
        {
        }

        public BillboardController(IEnumerable<IBillboardTargetSource> sources)
        {
            _sources = new List<IBillboardTargetSource>(sources);
        }

        /// <summary>今ビルボードになっているSpriteRendererの数。</summary>
        public int ActiveRendererCount => _swapper.Count;

        public bool IsActive { get; private set; }

        public void AddSource(IBillboardTargetSource source)
        {
            if (source != null && !_sources.Contains(source))
            {
                _sources.Add(source);
            }
        }

        /// <param name="rescan">trueなら対象をシーンから集め直す(重いので毎フレームは呼ばない)。未適用の状態からは必ず集める。</param>
        public void Apply(BillboardSettings settings, bool rescan)
        {
            if (settings == null || !settings.enabled || !EnsureMaterial())
            {
                Restore();
                return;
            }

            BillboardShaderGlobals.Apply(settings);

            if (rescan || !IsActive)
            {
                Collect(settings);
                _swapper.Sync(_buffer, _material);
            }

            _swapper.Refresh(_material);
            IsActive = true;
        }

        /// <summary>全て元のマテリアル・平らな描画に戻す。</summary>
        public void Restore()
        {
            _swapper.RestoreAll();
            BillboardShaderGlobals.Reset();
            IsActive = false;
        }

        public void Dispose()
        {
            Restore();

            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_material);
                }
                else
                {
                    Object.DestroyImmediate(_material);
                }
            }

            _material = null;
        }

        private void Collect(BillboardSettings settings)
        {
            _buffer.Clear();
            foreach (var source in _sources)
            {
                if (source.Category == BillboardTargets.None || settings.Includes(source.Category))
                {
                    source.Collect(_buffer);
                }
            }

            _buffer.RemoveAll(renderer => renderer == null || renderer.GetComponentInParent<BillboardIgnore>(true) != null);
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
            {
                return true;
            }

            var shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                if (!_missingShaderLogged)
                {
                    Debug.LogWarning($"[Billboard] シェーダー {ShaderName} が見つからないため、絵を立たせられません。");
                    _missingShaderLogged = true;
                }

                return false;
            }

            _material = new Material(shader) { name = "BillboardSprite (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            return true;
        }
    }
}
