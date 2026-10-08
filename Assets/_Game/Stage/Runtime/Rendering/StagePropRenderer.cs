using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 背景オブジェクトの見た目の状態（<see cref="StagePropLook"/>）を、子のRendererへまとめて渡す。
    /// 反応部品（透け・熱・壊れ）は Look を書き換えて <see cref="MarkDirty"/> を呼ぶだけでよい。
    /// 何も変化が無いときはシェーダーに値を渡さない（SRP Batcher の軽い描画のまま）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StageProp))]
    public sealed class StagePropRenderer : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private StageProp _prop;
        private bool _dirty = true;
        private bool _applied;

        public StagePropLook Look { get; } = new StagePropLook();

        public IReadOnlyList<Renderer> Renderers => _renderers;

        public void MarkDirty() => _dirty = true;

        /// <summary>モデルを差し替えたときなど、Rendererの一覧を取り直す。</summary>
        public void CollectRenderers()
        {
            _renderers.Clear();
            if (_prop == null)
            {
                _prop = GetComponent<StageProp>();
            }
            var root = _prop != null && _prop.visualRoot != null ? _prop.visualRoot : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer))
                {
                    _renderers.Add(r);
                }
            }

            _applied = true;
            _dirty = true;
        }

        /// <summary>見た目全体を囲む箱（ワールド座標）。Rendererが無ければ自分の位置の大きさ0の箱。</summary>
        public Bounds ComputeBounds()
        {
            var has = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in _renderers)
            {
                if (r == null)
                {
                    continue;
                }

                if (has)
                {
                    bounds.Encapsulate(r.bounds);
                }
                else
                {
                    bounds = r.bounds;
                    has = true;
                }
            }

            return bounds;
        }

        /// <summary>表示・非表示（壊れて完全に消えた後など）。</summary>
        public void SetVisible(bool visible)
        {
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }

        private void OnEnable()
        {
            _block ??= new MaterialPropertyBlock();
            CollectRenderers();
        }

        private void OnDisable()
        {
            ClearBlocks();
        }

        private void LateUpdate()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            if (Look.IsNeutral)
            {
                if (_applied)
                {
                    ClearBlocks();
                }

                return;
            }

            Fill(_block, Look);
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(_block);
                }
            }

            _applied = true;
        }

        private void ClearBlocks()
        {
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    r.SetPropertyBlock(null);
                }
            }

            _applied = false;
        }

        private static void Fill(MaterialPropertyBlock block, StagePropLook look)
        {
            block.Clear();
            block.SetFloat(StageShaderIds.Fade, look.Fade);
            block.SetFloat(StageShaderIds.NoHole, look.NoHole ? 1f : 0f);
            block.SetFloat(StageShaderIds.Darken, look.Darken);
            block.SetFloat(StageShaderIds.Flash, look.Flash);
            block.SetColor(StageShaderIds.FlashColor, look.FlashColor);
            block.SetFloat(StageShaderIds.Dissolve, look.Dissolve);
            block.SetColor(StageShaderIds.DissolveColor, look.DissolveColor);
            block.SetFloat(StageShaderIds.HeatCount, look.HeatCount);
            block.SetVectorArray(StageShaderIds.HeatPoints, look.HeatPoints);
            block.SetVectorArray(StageShaderIds.HeatValues, look.HeatValues);
        }
    }
}
