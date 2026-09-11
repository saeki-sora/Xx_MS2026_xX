using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    // 3D専用: メインライトの陰影(ndotl)を段階的に量子化する効果のため、そもそも陰影の勾配が
    // 存在するライティング計算を前提にしています。ShaderFXUberSprite.shader(2D)は現状アンリット
    // (光源計算をしない)描画のため、量子化する対象の陰影自体が存在せず、意味のある効果になりません。
    // そのため2D側には実装していません(2Dスプライトに割り当てても、コンパイルエラーにはならず
    // 単に見た目が変化しないだけです)。
    [Serializable]
    [EffectModuleInfo("トゥーン影(セルシェーディング)", "オブジェクト系",
        Description = "光の陰影をなめらかなグラデーションではなく、くっきりした段差(何段階か)で表現するアニメ調の陰影です。" +
            "3Dオブジェクト専用(2Dスプライトには効果がありません。2Dはアンリット描画のため陰影自体が存在しないためです)。")]
    public sealed class ToonShadingModule : EffectModule
    {
        [Range(1, 6)]
        [Tooltip("陰影の段階数です。2にすると「明/暗」のくっきり2色、値を増やすほど段階的なグラデーションに近づきます。")]
        public int steps = 2;

        [Tooltip("影になっている部分に混ぜる色です。単純に暗くするだけでなく、青みがかった影色にするなど演出に使えます。")]
        public Color shadeColor = new(0.55f, 0.55f, 0.65f, 1f);

        public override string Keyword => "_FX_TOON";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetFloat(ShaderIDs.ToonSteps, Mathf.Max(steps, 1));
            material.SetColor(ShaderIDs.ToonShadeColor, shadeColor);
        }

        public override int ComputeParameterHash() => HashCode.Combine(steps, shadeColor);

        public override string GetSummary() => $"{steps} steps";
    }
}
