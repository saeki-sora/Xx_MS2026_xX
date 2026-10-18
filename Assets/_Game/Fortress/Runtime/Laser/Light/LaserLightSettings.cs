using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ビームがまわりを照らす光の設定（見た目だけ）。<see cref="LaserTuningConfig.beamLight"/> が持ち、<see cref="LaserBeamLight"/> が使う。
    /// 床・背景の物（ステージの MS2026/Stage/Prop）はビームの線分を光源にして線に沿って途切れなく、群衆の敵はビームの近くの敵を、プレイヤー色に照らす。
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

        [Min(0.1f), Tooltip("ビームから光が届く距離（メートル）。大きいほど太い光の帯になる。")]
        public float floorRange = 2.8f;

        [Min(0f), Tooltip("光の線をビームからどれだけ浮かせるか（メートル）。高いほど広くぼんやり、低いほど狭くはっきり照らす。")]
        public float lightHeight = 0.8f;

        [Min(0f), Tooltip("壁などに当たっている場所の光の明るさの倍率。")]
        public float impactBoost = 1.6f;

        [Range(0f, 1f), Tooltip("照らされた所の色が、床・背景の物の色にどれだけ左右されるか。" +
                                 "1なら本物の光と同じく物の色が掛かる（緑の床に赤い光はくすむ）。0なら物の明るさだけ見て、光の色をそのまま乗せる（色がはっきり出る）。")]
        public float surfaceColorInfluence = 0.4f;

        [Header("群衆の敵")]
        [Min(0f), Tooltip("ビームの近くの敵が照らされる強さ。0なら照らさない。")]
        public float enemyStrength = 0.8f;

        [Min(0.1f), Tooltip("ビームから何メートルまでの敵を照らすか。")]
        public float enemyRadius = 1.3f;

        [Range(0f, 1f), Tooltip("照らされた敵に、光の色をそのまま上から乗せる量。大きいほど敵がはっきりプレイヤー色に染まる（暗い色の敵でも分かる）。")]
        public float enemyColorOverlay = 0.35f;

        [Header("共通")]
        [Range(0f, 2f), Tooltip("光の色の鮮やかさ。1でプレイヤー色そのまま、1より大きいほど濃く鮮やかに、小さいほど白っぽくなる。")]
        public float colorSaturation = 1.3f;

        [Range(0f, 1f), Tooltip("一番弱く握ったときの明るさ（強く握ったときを1とした割合）。")]
        public float weakGripBrightness = 0.45f;

        [Min(0.1f), Tooltip("撃ち始め・撃ち終わりに光がつく／消える速さ。大きいほどパッと切り替わる。")]
        public float fadeSpeed = 14f;
    }
}
