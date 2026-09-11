using UnityEngine;

namespace MS2026.ShaderFX
{
    // Aggregated snapshot of active screen-space effects. EffectDirector rebuilds this
    // whenever a registered target's screen modules change (event-driven, not per-frame);
    // ScreenFXFeature just reads it at render time.
    public sealed class ScreenFXSettings
    {
        public bool grayscaleEnabled;
        public float grayscaleIntensity = 1f;

        public bool posterizeEnabled;
        public int posterizeLevels = 4;

        public bool pixelateEnabled;
        public float pixelateBlockSize = 8f;

        // Set by PixelateModule when it asks to be restricted to one EffectGroup.
        // EffectDirector resolves this to a renderingLayerMask bit (via SetGroupRenderingLayer)
        // after aggregation; None means whole-screen.
        public EffectGroup pixelateRestrictGroup = EffectGroup.None;

        // Resolved by EffectDirector, not by modules directly. 0 = whole screen.
        public uint pixelateRenderingLayerMask;

        public bool outlineEnabled;
        public Color outlineColor = Color.black;
        public float outlineThickness = 1f;
        public float outlineDepthThreshold = 0.05f;
        public float outlineNormalThreshold = 0.4f;

        public bool shockwaveEnabled;
        public Vector2 shockwaveCenter = new(0.5f, 0.5f);
        public float shockwaveProgress; // 0=震源, 1=画面全体へ広がりきった状態
        public float shockwaveStrength = 0.05f;
        public float shockwaveWidth = 0.15f;

        public bool screenFlashEnabled;
        public Color screenFlashColor = Color.white;
        public float screenFlashAmount = 1f;

        public bool HasAnyEffect => grayscaleEnabled || posterizeEnabled || pixelateEnabled || outlineEnabled
            || shockwaveEnabled || screenFlashEnabled;

        public void Reset()
        {
            grayscaleEnabled = false;
            posterizeEnabled = false;
            pixelateEnabled = false;
            pixelateRestrictGroup = EffectGroup.None;
            pixelateRenderingLayerMask = 0;
            outlineEnabled = false;
            shockwaveEnabled = false;
            screenFlashEnabled = false;
        }
    }
}
