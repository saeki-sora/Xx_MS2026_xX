using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    [Serializable]
    [EffectModuleInfo("グリッチ(デジタル乱れ)", "オブジェクト系",
        Description = "映像がブロック状にずれたり、色がRGBにズレて分離したりする、デジタル的な故障・乱れの演出です。ロボットの故障やSF的なテレポート演出などに使えます。" +
            "テクスチャの模様がある表面ほど効果がはっきり見えます(単色のベタ塗りマテリアルだと、ズレる模様自体が無いため目立ちません)。")]
    public sealed class GlitchModule : EffectModule
    {
        [Range(0f, 1f)]
        [Tooltip("乱れの強さです。0で通常表示、1で最大まで映像が乱れます。")]
        public float amount = 0.5f;

        [Range(2f, 64f)]
        [Tooltip("ブロックずれの細かさです。値を大きくすると細かいブロック単位で、小さくすると大きな塊単位でずれます。")]
        public float blockSize = 16f;

        [Range(0f, 40f)]
        [Tooltip("乱れが切り替わる速さです。値を大きくするほど激しく点滅するように乱れます。")]
        public float speed = 12f;

        [Range(0f, 0.1f)]
        [Tooltip("赤・緑・青の色ズレの大きさです。値を大きくするほど色がバラバラにズレて見えます。")]
        public float rgbSplit = 0.02f;

        public override string Keyword => "_FX_GLITCH";

        public override void ApplyTo(Material material)
        {
            material.EnableKeyword(Keyword);
            material.SetFloat(ShaderIDs.GlitchAmount, amount);
            material.SetFloat(ShaderIDs.GlitchBlockSize, blockSize);
            material.SetFloat(ShaderIDs.GlitchSpeed, speed);
            material.SetFloat(ShaderIDs.GlitchRGBSplit, rgbSplit);
        }

        public override int ComputeParameterHash() => HashCode.Combine(amount, blockSize, speed, rgbSplit);

        public override string GetSummary() => $"Amount {amount:0.00}";
    }
}
