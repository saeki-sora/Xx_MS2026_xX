using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("ポスタリゼーション(減色)", "画面系",
        Description = "画面の色の階調(グラデーションの滑らかさ)を減らして、イラストやアニメ塗りのような表現にする効果です。")]
    public sealed class PosterizeModule : EffectModule
    {
        [Range(2, 16)]
        [Tooltip("色の段階数です。値を小さくするとくっきりした少ない色数(劇画調)になり、大きくすると元の見た目に近づきます。")]
        public int levels = 4;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.posterizeEnabled = true;
            settings.posterizeLevels = levels;
        }

        public override int ComputeParameterHash() => HashCode.Combine(levels);

        public override string GetSummary() => $"{levels} Levels";
    }
}
