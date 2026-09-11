using UnityEngine;

namespace MS2026.ShaderFX
{
    // A small, deliberately non-exhaustive set of easing curves for EffectTarget.PlayFloat/
    // PlayColor (design doc §5「ランタイムTween API」). Covers the shapes actually used for
    // hit-flash/dissolve-style transitions; add more here if a project needs them.
    public enum ShaderFXEase
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
    }

    public static class ShaderFXEasing
    {
        public static float Evaluate(ShaderFXEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case ShaderFXEase.InQuad: return t * t;
                case ShaderFXEase.OutQuad: return 1f - (1f - t) * (1f - t);
                case ShaderFXEase.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case ShaderFXEase.InCubic: return t * t * t;
                case ShaderFXEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case ShaderFXEase.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                default: return t;
            }
        }
    }
}
