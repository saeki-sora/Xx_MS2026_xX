using System;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>手前の背景オブジェクトが砲台・コアを隠したときの見せ方。</summary>
    public enum StageOcclusionMode
    {
        /// <summary>透けない。</summary>
        None,

        /// <summary>隠している部分だけ、丸く穴をあけたように透かす（おすすめ）。</summary>
        Hole,

        /// <summary>隠しているとき、オブジェクト全体を薄くする。</summary>
        WholeFade
    }

    /// <summary>背景オブジェクト1つ分の「見た目の反応」の設定。どれも見た目だけで、通信はしない（各PCが自分で計算する）。</summary>
    [Serializable]
    public sealed class StagePropLookSettings
    {
        [Tooltip("手前でこのオブジェクトが砲台・コアを隠したときの見せ方。")]
        public StageOcclusionMode occlusion = StageOcclusionMode.Hole;

        [Tooltip("「全体を薄く」のときの薄さ。0で完全に消え、1で透けない。")]
        [Range(0f, 1f)]
        public float wholeFadeAlpha = 0.3f;

        [Tooltip("ONなら、このオブジェクトの裏に隠れた敵を、単色の影（シルエット）で透かして見せる。")]
        public bool showHiddenEnemies = true;

        [Tooltip("ONなら、レーザーが当たった所が赤く光り、当て続けると焦げ跡が残る。")]
        public bool heatReactive = true;

        [Tooltip("光り方・焦げ方の強さの倍率。")]
        [Range(0f, 3f)]
        public float heatScale = 1f;

        [Tooltip("ONなら、敵の群れが押し寄せるとプルプル揺れる。")]
        public bool wobble = true;

        [Tooltip("揺れの大きさの倍率。重そうな物は小さく、軽そうな物は大きく。")]
        [Range(0f, 3f)]
        public float wobbleStrength = 1f;
    }
}
