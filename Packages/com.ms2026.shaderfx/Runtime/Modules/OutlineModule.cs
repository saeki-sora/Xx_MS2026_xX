using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("アウトライン(輪郭線)", "画面系",
        Description = "画面内のオブジェクトの輪郭やエッジに線を描く効果です。トゥーン(アニメ)調の見た目にしたい時に使います。")]
    public sealed class OutlineModule : EffectModule
    {
        [Tooltip("輪郭線の色です。")]
        public Color color = Color.black;

        [Range(0.5f, 5f)]
        [Tooltip("輪郭線の太さです。値を大きくすると太く、小さくすると細くなります。")]
        public float thickness = 1.5f;

        [Range(0.001f, 1f)]
        [Tooltip("奥行き(手前/奥)の差をどのくらいで輪郭とみなすかのしきい値です。値を小さくすると些細な段差にも反応しやすくなり、線が増えます。")]
        public float depthThreshold = 0.05f;

        [Range(0.01f, 2f)]
        [Tooltip("面の向きの差をどのくらいで輪郭とみなすかのしきい値です。値を小さくすると緩やかな折れ目にも反応しやすくなり、線が増えます。")]
        public float normalThreshold = 0.4f;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.outlineEnabled = true;
            settings.outlineColor = color;
            settings.outlineThickness = thickness;
            settings.outlineDepthThreshold = depthThreshold;
            settings.outlineNormalThreshold = normalThreshold;
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, thickness, depthThreshold, normalThreshold);

        public override string GetSummary() => $"Thickness {thickness:0.0}";
    }
}
