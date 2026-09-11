using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("氷結(フロスト)", "オブジェクト系",
        Description = "表面が凍りついたような、彩度を落とした青みがかった質感にきらめくハイライトを加える効果です。氷属性の敵や凍結状態異常などに使えます。")]
    public sealed class FrostModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("氷の色合いです。元の色にこの色を混ぜて凍った質感にします。")]
        public Color color = new(0.65f, 0.85f, 1f, 1f);

        [Range(0f, 1f)]
        [Tooltip("凍り具合の強さです。0で通常表示、1で完全に氷の色合いに覆われます。")]
        public float amount = 0.7f;

        [ColorUsage(true, true)]
        [Tooltip("氷の結晶がきらめくハイライトの色です。")]
        public Color sparkleColor = new(0.9f, 0.98f, 1f, 1f);

        [Range(0.5f, 0.99f)]
        [Tooltip("きらめきの出やすさです。値を小さくするときらめきが増え、大きくすると減ります。")]
        public float sparkleThreshold = 0.85f;

        public override string Keyword => "_FX_FROST";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.FrostColor, color);
            material.SetFloat(ShaderIDs.FrostAmount, amount);
            material.SetColor(ShaderIDs.FrostSparkleColor, sparkleColor);
            material.SetFloat(ShaderIDs.FrostSparkleThreshold, sparkleThreshold);
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, amount, sparkleColor, sparkleThreshold);

        public override string GetSummary() => $"Amount {amount:0.00}";
    }
}
