using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 背景オブジェクト1つの「今の見た目の状態」。透け・暗さ・光り・溶け・熱の点を各反応部品が書き込み、
    /// <see cref="StagePropRenderer"/> がまとめてシェーダーへ渡す。全部0（既定）なら素の見た目。
    /// </summary>
    public sealed class StagePropLook
    {
        /// <summary>0=透けない、1=完全に消える。</summary>
        public float Fade;

        /// <summary>trueなら「隠している部分に穴をあける」をしない。</summary>
        public bool NoHole;

        /// <summary>0=元の明るさ、1=真っ暗。</summary>
        public float Darken;

        /// <summary>0..1。被弾の瞬間の光り。</summary>
        public float Flash;

        public Color FlashColor = Color.white;

        /// <summary>0=普通、1=完全に溶けて消えた。</summary>
        public float Dissolve;

        public Color DissolveColor = new Color(1f, 0.55f, 0.15f);

        public readonly Vector4[] HeatPoints = new Vector4[StageShaderIds.MaxHeatPoints];
        public readonly Vector4[] HeatValues = new Vector4[StageShaderIds.MaxHeatPoints];
        public int HeatCount;

        /// <summary>素の見た目と同じか（同じならシェーダーに何も渡さず、軽い描画のままにする）。</summary>
        public bool IsNeutral =>
            Fade <= 0f && !NoHole && Darken <= 0f && Flash <= 0f && Dissolve <= 0f && HeatCount == 0;
    }
}
