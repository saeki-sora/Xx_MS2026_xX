using UnityEngine;

namespace MS2026.SpriteAnim
{
    /// <summary>
    /// SpriteAnimator が実際に絵を出す先を抽象化するインターフェース。
    /// SpriteRenderer(2D) と UI Image のどちらにも同じ SpriteAnimator / SpriteAnimationSet で対応できるようにするための層。
    /// </summary>
    public interface ISpriteAnimationTarget
    {
        GameObject GameObject { get; }
        Sprite Sprite { get; set; }
        Color Color { get; set; }
        bool FlipX { get; set; }
        bool FlipY { get; set; }

        /// <summary>クロスフェード用に、見た目が同じ複製レイヤーを生成する（旧アニメーションを一時的に重ねて表示するため）。</summary>
        ISpriteAnimationTarget CreateBlendLayer();

        /// <summary>CreateBlendLayer で作った複製レイヤーを破棄する。</summary>
        void DestroyBlendLayer(ISpriteAnimationTarget layer);
    }
}
