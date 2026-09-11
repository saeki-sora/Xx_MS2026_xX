using System;
using UnityEngine;

namespace MS2026.ShaderFX
{
    // Platform/quality-driven effect thinning (design doc §5「品質スケーリング」), e.g. running
    // 画面系 effects at half resolution on mobile. Deliberately a tiny static surface rather than
    // a ScriptableObject settings asset — there's exactly one knob so far (screen-effect
    // resolution scale) and a static property is enough to read from ScreenFXPass every frame
    // without an asset reference to wire up.
    public static class ShaderFXQuality
    {
        private static float? resolutionScaleOverride;

        // Set to force a specific scale (e.g. from a project's own settings menu); set back to
        // null to return to the automatic QualitySettings/platform-based guess below.
        public static float? ResolutionScaleOverride
        {
            get => resolutionScaleOverride;
            set => resolutionScaleOverride = value;
        }

        // Automatic default: mobile platforms, or a Quality Level whose name suggests it's the
        // low/mobile tier, run 画面系 effects at half resolution (then upscale — see
        // ScreenFXPass's final "Copy" blit). Everything else stays full-resolution. This is a
        // heuristic, not a guarantee your project's Quality Level names match it — set
        // ResolutionScaleOverride explicitly if the guess is wrong for your setup.
        public static float ScreenEffectResolutionScale
        {
            get
            {
                if (resolutionScaleOverride.HasValue) return Mathf.Clamp(resolutionScaleOverride.Value, 0.1f, 1f);
                return LooksLikeLowEndTarget() ? 0.5f : 1f;
            }
        }

        private static bool LooksLikeLowEndTarget()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            int level = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;
            if (level < 0 || level >= names.Length) return false;

            string name = names[level];
            return name.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Low", StringComparison.OrdinalIgnoreCase) >= 0;
#endif
        }
    }
}
