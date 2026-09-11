using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("リムライト", "オブジェクト系",
        Description = "オブジェクトの輪郭(縁)に光を足す効果です。逆光のような縁取りの光を演出します。")]
    public sealed class RimLightModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("縁取りの光の色です。")]
        public Color color = new(0.3f, 0.9f, 1f, 1f);

        [Range(0.1f, 10f)]
        [Tooltip("光る縁の鋭さです。値を大きくすると光る範囲が細く鋭くなり、小さくすると広くぼんやりします。")]
        public float power = 3f;

        [Range(0f, 5f)]
        [Tooltip("光の強さです。値を大きくすると明るく目立ち、小さくすると控えめになります。0で見えなくなります。")]
        public float intensity = 1.5f;

        public override string Keyword => "_FX_RIM";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.RimColor, color);
            material.SetFloat(ShaderIDs.RimPower, power);
            material.SetFloat(ShaderIDs.RimIntensity, intensity);
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, power, intensity);

        public override string GetSummary() => $"Power {power:0.0} / x{intensity:0.0}";
    }
}
