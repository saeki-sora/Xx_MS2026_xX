using UnityEngine.Timeline;

namespace MS2026.ShaderFX.Timeline
{
    // Custom Timeline track (design doc §5「Timeline / Animationクリップ対応」) — drives an
    // EffectTarget's individual-difference parameters (Dissolve/HitFlash/custom float) from
    // clips instead of script-driven Tween calls. Bind the track to a GameObject with an
    // EffectTarget in the Timeline window's track binding field.
    [TrackClipType(typeof(ShaderFXParameterClip))]
    [TrackBindingType(typeof(EffectTarget))]
    [TrackColor(0.2f, 0.8f, 0.7f)]
    public sealed class ShaderFXParameterTrack : TrackAsset
    {
    }
}
