using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("発火(ファイア)", "オブジェクト系",
        Description = "表面を揺らめく炎のような加算光で覆う効果です。燃えているキャラクターや炎属性の武器などに使えます。")]
    public sealed class FireModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("炎の色です。オレンジ〜赤系が定番ですが、緑や紫にすれば「毒炎」「魔法の炎」のような表現にもなります。")]
        public Color color = new(1f, 0.45f, 0.05f, 1f);

        [Range(0f, 10f)]
        [Tooltip("炎の明るさです。値を大きくすると強く燃え上がって見えます。")]
        public float intensity = 2f;

        [Range(0f, 10f)]
        [Tooltip("炎が揺らめく速さです。値を大きくするほど激しく揺れ動きます。")]
        public float scrollSpeed = 1.5f;

        [Range(1f, 64f)]
        [Tooltip("炎の模様の細かさです。値を大きくすると細かい炎の模様、小さくすると大きな塊で揺らめきます。" +
            "オブジェクトが小さい場合は値を大きめにしないと模様が出にくいので注意してください。")]
        public float noiseScale = 24f;

        public override string Keyword => "_FX_FIRE";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.FireColor, color);
            material.SetFloat(ShaderIDs.FireIntensity, intensity);
            material.SetFloat(ShaderIDs.FireScrollSpeed, scrollSpeed);
            material.SetFloat(ShaderIDs.FireNoiseScale, noiseScale);
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, intensity, scrollSpeed, noiseScale);

        public override string GetSummary() => $"x{intensity:0.0}";
    }
}
