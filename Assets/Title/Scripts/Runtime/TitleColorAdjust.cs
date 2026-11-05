using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// 配置物ごとの色の調整（明るさ・コントラスト・鮮やかさ・中間の明るさ・色味）。
    /// 自分と子の SpriteRenderer 全部に効く（ただし、自分の Title Color Adjust を持つ子から下は、その子の設定が効く）。
    /// 編集中もすぐ見た目に出る。シェーダーは ShaderFX/Uber Sprite が必要（Title_SpriteFX のマテリアル。ほかのシェーダーの絵には効かない）。
    /// エフェクト（白フラッシュ・ふちの光など）の光は調整しない。絵そのものだけ。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TitleColorAdjust : MonoBehaviour
    {
        [Tooltip("明るさの倍率（1 = そのまま、0.8 = 2割暗く、1.2 = 2割明るく）。")]
        [Range(0f, 2f)] public float brightness = 1f;
        [Tooltip("コントラスト（1 = そのまま。小さいと眠い感じ、大きいとくっきり）。")]
        [Range(0f, 2f)] public float contrast = 1f;
        [Tooltip("鮮やかさ（1 = そのまま、0 = 白黒、大きいほど色が濃い）。")]
        [Range(0f, 2f)] public float saturation = 1f;
        [Tooltip("中間の明るさ（1 = そのまま。小さいと中間の色が暗く、大きいと明るくなる。白と黒はほぼ変わらない）。")]
        [Range(0.2f, 3f)] public float midtones = 1f;
        [Tooltip("色味（掛け算。白 = そのまま）。")]
        public Color tint = Color.white;

        private static readonly int OnId = Shader.PropertyToID("_ColorAdjustOn");
        private static readonly int BrightnessId = Shader.PropertyToID("_ColorAdjustBrightness");
        private static readonly int ContrastId = Shader.PropertyToID("_ColorAdjustContrast");
        private static readonly int SaturationId = Shader.PropertyToID("_ColorAdjustSaturation");
        private static readonly int GammaId = Shader.PropertyToID("_ColorAdjustGamma");
        private static readonly int TintId = Shader.PropertyToID("_ColorAdjustTint");

        private readonly List<SpriteRenderer> renderers = new();
        private readonly List<SpriteRenderer> found = new();
        private MaterialPropertyBlock block;
        private bool applied;

        /// <summary>そのままの見た目（調整なし）か。</summary>
        public bool IsNeutral =>
            Mathf.Approximately(brightness, 1f) && Mathf.Approximately(contrast, 1f) && Mathf.Approximately(saturation, 1f)
            && Mathf.Approximately(midtones, 1f) && tint == Color.white;

        /// <summary>効く SpriteRenderer（自分の Title Color Adjust を持つ子から下は除く）。</summary>
        public IReadOnlyList<SpriteRenderer> Renderers
        {
            get
            {
                Collect();
                return renderers;
            }
        }

        /// <summary>シェーダーが色の調整に対応していない絵の数（Inspector の注意書き用）。</summary>
        public int UnsupportedCount
        {
            get
            {
                var count = 0;
                foreach (var r in Renderers)
                {
                    var m = r != null ? r.sharedMaterial : null;
                    if (m == null || !m.HasProperty(OnId)) count++;
                }
                return count;
            }
        }

        public void ResetValues()
        {
            brightness = 1f;
            contrast = 1f;
            saturation = 1f;
            midtones = 1f;
            tint = Color.white;
        }

        private void OnEnable() => LateUpdate();

        private void OnValidate()
        {
            if (isActiveAndEnabled) LateUpdate();
        }

        private void OnDisable()
        {
            // 調整を外す（ほかの値、ShaderFX の個体差などはそのまま）。
            if (!applied) return;
            foreach (var r in renderers) SetOn(r, false);
            applied = false;
        }

        // ShaderFX の EffectTarget なども同じ MaterialPropertyBlock を使い、外したり入れ直したりするので、毎フレーム入れ直す。
        // 歯磨き粉の「Slice」のように再生中に増える子にも効く。
        private void LateUpdate()
        {
            Collect();
            var neutral = IsNeutral;
            if (neutral && !applied) return; // 何もしていない絵は MaterialPropertyBlock を付けない（まとめて描く効率を保つ）
            foreach (var r in renderers) SetOn(r, !neutral);
            applied = !neutral;
        }

        private void Collect()
        {
            renderers.Clear();
            GetComponentsInChildren(true, found);
            foreach (var r in found)
            {
                if (r != null && r.GetComponentInParent<TitleColorAdjust>(true) == this) renderers.Add(r);
            }
        }

        private void SetOn(SpriteRenderer r, bool on)
        {
            if (r == null) return;
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetFloat(OnId, on ? 1f : 0f);
            if (on)
            {
                block.SetFloat(BrightnessId, brightness);
                block.SetFloat(ContrastId, contrast);
                block.SetFloat(SaturationId, saturation);
                block.SetFloat(GammaId, midtones);
                block.SetVector(TintId, tint); // シェーダーは sRGB に近い値で掛けるので、リニアに変換しない（SetColor だと変換される）
            }
            r.SetPropertyBlock(block);
        }
    }
}
