using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("被弾フラッシュ", "オブジェクト系",
        Description = "被弾時などに一瞬だけ色を差し替えて光らせる演出です。白にすると定番の被弾フラッシュになります。")]
    public sealed class HitFlashModule : EffectModule
    {
        [Tooltip("フラッシュさせる色です。白にすると被弾時の定番の白点滅になります。")]
        public Color color = Color.white;

        [Range(0f, 1f)]
        [Tooltip("フラッシュの強さです。0で通常表示、1で完全にフラッシュ色一色になります。被弾の瞬間だけ1にして、すぐ0へ戻す使い方が基本です。")]
        public float amount;

        public override string Keyword => "_FX_HITFLASH";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetColor(ShaderIDs.HitFlashColor, color);
            material.SetFloat(ShaderIDs.HitFlashAmount, amount);
        }

        public override int ComputeParameterHash() => HashCode.Combine(color, amount);

        public override string GetSummary() => $"Amount {amount:0.00}";
    }
}
