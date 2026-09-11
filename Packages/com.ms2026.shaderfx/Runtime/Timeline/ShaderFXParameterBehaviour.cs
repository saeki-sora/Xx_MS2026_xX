using System;
using UnityEngine;
using UnityEngine.Playables;

namespace MS2026.ShaderFX.Timeline
{
    // Which EffectTarget.SetInstanceX call a clip drives. DissolveAmount/HitFlashAmount are the
    // two individual-difference parameters the shader declares as GPU-instanced (see
    // ShaderFXUber.shader) — Custom falls back to a plain (non-instanced) named float property.
    public enum ShaderFXTimelineParameter
    {
        DissolveAmount,
        HitFlashAmount,
        Custom,
    }

    [Serializable]
    public sealed class ShaderFXParameterBehaviour : PlayableBehaviour
    {
        public ShaderFXTimelineParameter parameter = ShaderFXTimelineParameter.DissolveAmount;
        public string customPropertyName;
        public float fromValue;
        public float toValue = 1f;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as EffectTarget;
            if (target == null) return;

            double duration = playable.GetDuration();
            double normalizedTime = duration > 0.0 ? playable.GetTime() / duration : 1.0;
            float value = Mathf.LerpUnclamped(fromValue, toValue, curve.Evaluate((float)normalizedTime));

            switch (parameter)
            {
                case ShaderFXTimelineParameter.DissolveAmount:
                    target.SetInstanceDissolveAmount(value);
                    break;
                case ShaderFXTimelineParameter.HitFlashAmount:
                    target.SetInstanceHitFlashAmount(value);
                    break;
                case ShaderFXTimelineParameter.Custom:
                    if (!string.IsNullOrEmpty(customPropertyName)) target.SetInstanceFloat(customPropertyName, value);
                    break;
            }
        }
    }
}
