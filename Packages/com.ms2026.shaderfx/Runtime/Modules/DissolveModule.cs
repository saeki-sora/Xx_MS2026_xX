using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("ディゾルブ(溶解)", "オブジェクト系",
        Description = "オブジェクトがノイズ状に少しずつ消えていく(または現れる)演出です。消滅エフェクトの定番です。")]
    public sealed class DissolveModule : EffectModule
    {
        [Range(0f, 1f)]
        [Tooltip("消える進み具合です。0で全く消えておらず、1で完全に消えます。アニメーションさせる場合はこの値を0→1へ変化させます。")]
        public float amount;

        [Range(0.001f, 0.5f)]
        [Tooltip("消えていく境界線の太さです。値を大きくすると縁取りが太く、小さくすると細くなります。")]
        public float edgeWidth = 0.08f;

        [ColorUsage(true, true)]
        [Tooltip("消えていく境界線が光る色です。燃えているような表現にしたい場合はオレンジ〜赤系が定番です。")]
        public Color edgeColor = new(1f, 0.55f, 0.05f, 1f);

        [Range(1f, 64f)]
        [Tooltip("消え方の模様の細かさです。値を大きくすると模様が細かくなり、小さくすると大きな塊で消えていきます。")]
        public float noiseScale = 12f;

        public override string Keyword => "_FX_DISSOLVE";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetFloat(ShaderIDs.DissolveAmount, amount);
            material.SetFloat(ShaderIDs.DissolveEdgeWidth, edgeWidth);
            material.SetColor(ShaderIDs.DissolveEdgeColor, edgeColor);
            material.SetFloat(ShaderIDs.DissolveNoiseScale, noiseScale);
        }

        public override int ComputeParameterHash() => HashCode.Combine(amount, edgeWidth, edgeColor, noiseScale);

        public override string GetSummary() => $"Amount {amount:0.00}";
    }
}
