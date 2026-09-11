using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("グレースケール(白黒化)", "画面系",
        Description = "画面全体を白黒(モノクロ)にする効果です。回想シーンや特殊な状態表現などに使えます。")]
    public sealed class GrayscaleModule : EffectModule
    {
        [Range(0f, 1f)]
        [Tooltip("白黒化の強さです。0で通常のカラー表示、1で完全に白黒になります。")]
        public float intensity = 1f;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.grayscaleEnabled = true;
            settings.grayscaleIntensity = intensity;
        }

        public override int ComputeParameterHash() => HashCode.Combine(intensity);

        public override string GetSummary() => $"Intensity {intensity:0.00}";
    }
}
