using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面のふちの黒い靄（見た目だけ）の設定。敵が画面の外から出てくるので、各カメラから見て「コアと反対側の画面の端」だけを
    /// 暗い靄にして、その中を夜の見え方（暗い紺・ビームの光だまり・敵は冷たい縁の光で浮かぶシルエット）にする。
    /// コアのまわりは今の明るい見え方のまま。ステージの見た目の設定（StageLookProfile.edgeMist）が持ち、<see cref="EdgeMistLook"/> が描く。
    /// ネット対戦でも通信せず、各PCが自分のカメラで描く。
    /// </summary>
    [Serializable]
    public sealed class EdgeMistSettings
    {
        [Tooltip("OFFにすると靄を出さない。")]
        public bool enabled = true;

        [Range(0f, 1f), Tooltip("靄が始まる位置。コアから画面の端までを1として、この割合より外側から靄がかかり始める（小さいほど靄が広い）。")]
        public float start = 0.42f;

        [Range(0.05f, 1f), Tooltip("靄が一番濃くなるまでの幅（コア〜画面の端を1とした割合）。大きいほど、ふんわり濃くなる。")]
        public float softness = 0.42f;

        [Range(0f, 1f), Tooltip("コアに近い側の画面の端（コアと端の距離が短い所）の靄を弱める量。1にすると、コアから遠い側の端だけに靄が出る。")]
        public float nearSideFade = 0.5f;

        [Range(1.5f, 8f), Tooltip("靄の形の四角さ。小さいほど画面の角の靄が丸くなめらかに、大きいほど画面の端と平行な四角い形になる。")]
        public float cornerRoundness = 2.6f;

        [Range(0f, 1f), Tooltip("靄の一番濃い所の暗さ（1で真っ暗）。")]
        public float darkness = 0.86f;

        [Tooltip("靄そのものの色（暗い所に足される色）。")]
        public Color mistColor = new Color(0.02f, 0.025f, 0.075f, 1f);

        [Tooltip("靄の中の物に掛ける色（青っぽくすると夜の冷たい見え方になる）。")]
        public Color shadeTint = new Color(0.55f, 0.62f, 1f, 1f);

        [Tooltip("靄の内側のふちにうっすら乗る色（靄らしさを出す）。")]
        public Color edgeBandColor = new Color(0.32f, 0.16f, 0.62f, 1f);

        [Range(0f, 1f), Tooltip("靄の内側のふちの色の濃さ（0で出さない）。")]
        public float edgeBandStrength = 0.1f;

        [Header("ゆらぎ")]
        [Range(0f, 0.6f), Tooltip("靄のふちのゆらぎの大きさ（0でまっすぐな境目）。")]
        public float wobble = 0.2f;

        [Range(0.5f, 10f), Tooltip("ゆらぎの細かさ（大きいほど細かい）。")]
        public float wobbleScale = 3f;

        [Range(0f, 1f), Tooltip("ゆらぎの流れる速さ。")]
        public float flowSpeed = 0.08f;

        [Header("靄の中の光")]
        [Range(0f, 1f), Tooltip("光っている物（ビーム・エフェクトなど）を靄の中でも明るいまま残す量。")]
        public float keepLights = 0.9f;

        [Min(0f), Tooltip("この明るさより明るい色を「光っている物」として残す（1より上は HDR の光だけ）。")]
        public float keepThreshold = 0.95f;

        [Min(0.01f), Tooltip("「光っている物」とみなす明るさの幅（大きいほど、少し暗い物も残る）。")]
        public float keepSoftness = 0.6f;

        [Min(0f), Tooltip("ビームが靄を照らす明るさ（ビームの光の色で、まわりの靄がほんのり光る）。")]
        public float beamGlow = 1.2f;

        [Min(0.1f), Tooltip("ビームが靄を照らす範囲（ビームが床を照らす範囲の何倍か）。")]
        public float beamGlowRange = 1.6f;

        [Range(0f, 1f), Tooltip("ビームの光が当たっている所だけ、靄の下の元の色を見せる量（光だまりで床の色が見える）。")]
        public float beamReveal = 0.6f;

        [Header("靄の中の敵")]
        [ColorUsage(false, true), Tooltip("靄の中の敵に付く縁の光の色（HDR。1より明るいと光のにじみも付く）。")]
        public Color enemyRimColor = new Color(0.75f, 0.9f, 2.4f, 1f);

        [Range(0f, 0.3f), Tooltip("縁の光の太さ（敵の絵の大きさに対する割合）。0で出さない。")]
        public float enemyRimWidth = 0.08f;
    }
}
