using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("ドット絵化(モザイク)", "画面系",
        Description = "画面をドット絵風の粗いモザイク状にする効果です。レトロゲーム風の演出や、特定オブジェクトを目立たなくする用途に使えます。")]
    public sealed class PixelateModule : EffectModule
    {
        [Range(1f, 64f)]
        [Tooltip("ドット1つ分の大きさ(ピクセル数)です。値を大きくするとドットが粗く大きくなり、小さくすると元の見た目に近づきます。")]
        public float blockSize = 8f;

        [Tooltip("有効にすると、指定した Group に属するオブジェクトだけがドット化されます(例:「敵だけドット化」)。" +
                  "対象にするには先に EffectDirector.SetGroupRenderingLayer でビットを割り当てておく必要があります。")]
        public bool restrictToGroup;

        [Tooltip("ドット化の対象にする Group です(restrictToGroup が有効な場合のみ使われます)。")]
        public EffectGroup group = EffectGroup.Enemy;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.pixelateEnabled = true;
            settings.pixelateBlockSize = blockSize;
            settings.pixelateRestrictGroup = restrictToGroup ? group : EffectGroup.None;
        }

        public override int ComputeParameterHash() => HashCode.Combine(blockSize, restrictToGroup, group);

        public override string GetSummary() => restrictToGroup ? $"{blockSize:0}px / {group}" : $"{blockSize:0}px / Screen";
    }
}
