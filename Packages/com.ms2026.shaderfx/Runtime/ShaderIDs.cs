using UnityEngine;

namespace MS2026.ShaderFX
{
    internal static class ShaderIDs
    {
        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static readonly int RimColor = Shader.PropertyToID("_RimColor");
        public static readonly int RimPower = Shader.PropertyToID("_RimPower");
        public static readonly int RimIntensity = Shader.PropertyToID("_RimIntensity");

        public static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        public static readonly int DissolveEdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
        public static readonly int DissolveEdgeColor = Shader.PropertyToID("_DissolveEdgeColor");
        public static readonly int DissolveNoiseScale = Shader.PropertyToID("_DissolveNoiseScale");

        public static readonly int HitFlashColor = Shader.PropertyToID("_HitFlashColor");
        public static readonly int HitFlashAmount = Shader.PropertyToID("_HitFlashAmount");

        public static readonly int FXEmissionColor = Shader.PropertyToID("_FXEmissionColor");
        public static readonly int FXEmissionIntensity = Shader.PropertyToID("_FXEmissionIntensity");
        public static readonly int FXEmissionPulseSpeed = Shader.PropertyToID("_FXEmissionPulseSpeed");

        public static readonly int FireColor = Shader.PropertyToID("_FireColor");
        public static readonly int FireIntensity = Shader.PropertyToID("_FireIntensity");
        public static readonly int FireScrollSpeed = Shader.PropertyToID("_FireScrollSpeed");
        public static readonly int FireNoiseScale = Shader.PropertyToID("_FireNoiseScale");

        public static readonly int FrostColor = Shader.PropertyToID("_FrostColor");
        public static readonly int FrostAmount = Shader.PropertyToID("_FrostAmount");
        public static readonly int FrostSparkleColor = Shader.PropertyToID("_FrostSparkleColor");
        public static readonly int FrostSparkleThreshold = Shader.PropertyToID("_FrostSparkleThreshold");

        public static readonly int HologramColor = Shader.PropertyToID("_HologramColor");
        public static readonly int HologramFresnelPower = Shader.PropertyToID("_HologramFresnelPower");
        public static readonly int HologramScanlineSpeed = Shader.PropertyToID("_HologramScanlineSpeed");
        public static readonly int HologramScanlineDensity = Shader.PropertyToID("_HologramScanlineDensity");
        public static readonly int HologramFlickerSpeed = Shader.PropertyToID("_HologramFlickerSpeed");
        public static readonly int HologramFlickerIntensity = Shader.PropertyToID("_HologramFlickerIntensity");

        public static readonly int GlitchAmount = Shader.PropertyToID("_GlitchAmount");
        public static readonly int GlitchBlockSize = Shader.PropertyToID("_GlitchBlockSize");
        public static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        public static readonly int GlitchRGBSplit = Shader.PropertyToID("_GlitchRGBSplit");

        public static readonly int UVScrollColor = Shader.PropertyToID("_UVScrollColor");
        public static readonly int UVScrollDirection = Shader.PropertyToID("_UVScrollDirection");
        public static readonly int UVScrollSpeed = Shader.PropertyToID("_UVScrollSpeed");
        public static readonly int UVScrollPatternScale = Shader.PropertyToID("_UVScrollPatternScale");
        public static readonly int UVScrollSharpness = Shader.PropertyToID("_UVScrollSharpness");

        public static readonly int ToonSteps = Shader.PropertyToID("_ToonSteps");
        public static readonly int ToonShadeColor = Shader.PropertyToID("_ToonShadeColor");
    }
}
