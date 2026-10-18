using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ビームがまわりを照らす光の設定（見た目だけ）。<see cref="LaserTuningConfig.beamLight"/> が持ち、<see cref="LaserBeamLight"/> が使う。
    /// 床・背景の物は本物の点光源（ビームに沿って数個）で、群衆の敵はシェーダーで、プレイヤー色に照らす。
    /// 当たり判定・ダメージには影響しない。ネット対戦でも通信せず、各PCが自分で描く。
    /// </summary>
    [Serializable]
    public sealed class LaserLightSettings
    {
        [Tooltip("OFFにすると、ビームはまわりを照らさない。")]
        public bool enabled = true;

        [Header("床・背景の物")]
        [Min(0f), Tooltip("床・背景の物を照らす明るさ。0なら照らさない。")]
        public float floorIntensity = 2.5f;

        [Min(0.1f), Tooltip("光1個が届く距離（メートル）。大きいほど太い光の帯になる。")]
        public float floorRange = 2.8f;

        [Min(0.5f), Tooltip("ビームに沿って光を置く間隔（メートル）。小さいほどなめらかだが、光の数が増えて少し重くなる。")]
        public float lightSpacing = 2.5f;

        [Min(0f), Tooltip("光を床からどれだけ浮かせるか（メートル）。高いほど広くぼんやり、低いほど狭くはっきり照らす。")]
        public float lightHeight = 0.8f;

        [Min(0f), Tooltip("壁などに当たっている場所の光の明るさの倍率。")]
        public float impactBoost = 1.6f;

        [Header("群衆の敵")]
        [Min(0f), Tooltip("ビームの近くの敵が照らされる強さ。0なら照らさない。")]
        public float enemyStrength = 0.8f;

        [Min(0.1f), Tooltip("ビームから何メートルまでの敵を照らすか。")]
        public float enemyRadius = 1.3f;

        [Header("共通")]
        [Range(0f, 1f), Tooltip("一番弱く握ったときの明るさ（強く握ったときを1とした割合）。")]
        public float weakGripBrightness = 0.45f;

        [Min(0.1f), Tooltip("撃ち始め・撃ち終わりに光がつく／消える速さ。大きいほどパッと切り替わる。")]
        public float fadeSpeed = 14f;
    }
}
