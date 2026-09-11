using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("UVスクロール(エネルギーライン)", "オブジェクト系",
        Description = "流れるように動く筋状の加算光を表面に重ねる効果です。溶岩の筋、魔法陣の光る線、機械の配線の発光など、汎用的に使えます。")]
    public sealed class UVScrollModule : EffectModule
    {
        [ColorUsage(true, true)]
        [Tooltip("流れる光の色です。")]
        public Color color = new(1f, 0.7f, 0.1f, 1f);

        [Tooltip("光が流れる方向です。(1,0)で横方向、(0,1)で縦方向に流れます。")]
        public Vector2 direction = new(0f, 1f);

        [Range(0f, 10f)]
        [Tooltip("光が流れる速さです。")]
        public float speed = 1f;

        [Range(1f, 64f)]
        [Tooltip("筋模様の細かさです。値を大きくすると細い筋が多数、小さくすると太い筋が少数になります。" +
            "オブジェクトが小さい場合は値を大きめにしないと模様が出にくいので注意してください。")]
        public float patternScale = 30f;

        [Range(0.5f, 0.99f)]
        [Tooltip("筋の鋭さです。値を大きくすると細くくっきりした筋に、小さくすると太くぼんやりした筋になります。")]
        public float sharpness = 0.6f;

        public override string Keyword => "_FX_UVSCROLL";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.UVScrollColor, color);
            material.SetVector(ShaderIDs.UVScrollDirection, direction.normalized);
            material.SetFloat(ShaderIDs.UVScrollSpeed, speed);
            material.SetFloat(ShaderIDs.UVScrollPatternScale, patternScale);
            material.SetFloat(ShaderIDs.UVScrollSharpness, sharpness);
        }

        public override int ComputeParameterHash() =>
            HashCode.Combine(color, direction, speed, patternScale, sharpness);

        public override string GetSummary() => $"Speed {speed:0.0}";
    }
}
