using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("ホログラム", "オブジェクト系",
        Description = "縁が強く光る半透明のフレネル発光に、走査線(スキャンライン)と明滅を加えた、SF的な投影映像のような効果です。")]
    public sealed class HologramModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("ホログラムの発光色です。シアンや水色が定番です。")]
        public Color color = new(0.2f, 0.9f, 1f, 1f);

        [Range(0.5f, 8f)]
        [Tooltip("縁の発光の鋭さです。値を大きくすると輪郭付近だけが強く光り、小さくすると全体的に光ります。")]
        public float fresnelPower = 2.5f;

        [Range(0f, 40f)]
        [Tooltip("走査線が流れる速さです。0で静止した走査線になります。")]
        public float scanlineSpeed = 4f;

        [Range(1f, 200f)]
        [Tooltip("走査線の細かさです。値を大きくするほど線の本数が増えます。")]
        public float scanlineDensity = 60f;

        [Range(0f, 20f)]
        [Tooltip("明滅(ちらつき)の速さです。0で明滅しません。")]
        public float flickerSpeed = 6f;

        [Range(0f, 1f)]
        [Tooltip("明滅の強さです。値を大きくするほど明るさの変化が激しくなります。")]
        public float flickerIntensity = 0.15f;

        public override string Keyword => "_FX_HOLOGRAM";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.HologramColor, color);
            material.SetFloat(ShaderIDs.HologramFresnelPower, fresnelPower);
            material.SetFloat(ShaderIDs.HologramScanlineSpeed, scanlineSpeed);
            material.SetFloat(ShaderIDs.HologramScanlineDensity, scanlineDensity);
            material.SetFloat(ShaderIDs.HologramFlickerSpeed, flickerSpeed);
            material.SetFloat(ShaderIDs.HologramFlickerIntensity, flickerIntensity);
        }

        public override int ComputeParameterHash() =>
            HashCode.Combine(color, fresnelPower, scanlineSpeed, HashCode.Combine(scanlineDensity, flickerSpeed, flickerIntensity));

        public override string GetSummary() => $"Fresnel {fresnelPower:0.0}";
    }
}
