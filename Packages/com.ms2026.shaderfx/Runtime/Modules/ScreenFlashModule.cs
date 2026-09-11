using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("画面フラッシュ", "画面系",
        Description = "画面全体を指定した色でフラッシュ(点滅)させます。被弾時の赤フラッシュ、回復時の白フラッシュ、" +
            "画面切り替え時の演出など、インパクトを出したい瞬間に使います。")]
    public sealed class ScreenFlashModule : EffectModule
    {
        [Tooltip("フラッシュさせる色です。被弾なら赤、回復なら白などが定番です。")]
        public Color color = Color.white;

        [Range(0f, 1f)]
        [Tooltip("フラッシュの強さです。0で通常表示、1で完全にその色一色になります。" +
            "被弾の瞬間だけ1にして、Tween APIなどですぐ0へ戻す使い方が基本です。")]
        public float amount;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.screenFlashEnabled = true;
            settings.screenFlashColor = color;
            settings.screenFlashAmount = amount;
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, amount);

        public override string GetSummary() => $"Amount {amount:0.00}";
    }
}
