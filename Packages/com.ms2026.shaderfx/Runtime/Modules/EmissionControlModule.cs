using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("発光(エミッション)", "オブジェクト系",
        Description = "オブジェクト自体を発光させる効果です。暗い場所でも目立たせたい時や、機械の光る部分などに使います。")]
    public sealed class EmissionControlModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("発光の色です。")]
        public Color color = Color.white;

        [Range(0f, 10f)]
        [Tooltip("発光の強さです。値を大きくすると強く光り、0にすると発光しなくなります。")]
        public float intensity = 1f;

        [Range(0f, 10f)]
        [Tooltip("点滅(明滅)の速さです。0で点滅せず一定の明るさのまま光ります。値を大きくするほど素早く点滅します。")]
        public float pulseSpeed;

        public override string Keyword => "_FX_EMISSION";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.FXEmissionColor, color);
            material.SetFloat(ShaderIDs.FXEmissionIntensity, intensity);
            material.SetFloat(ShaderIDs.FXEmissionPulseSpeed, pulseSpeed);
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, intensity, pulseSpeed);

        public override string GetSummary() => pulseSpeed > 0f ? $"Pulse x{intensity:0.0}" : $"x{intensity:0.0}";
    }
}
