using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面に斜めに差し込む光の筋（見た目だけ。霧は使わない）の設定。ステージの見た目の設定（StageLookProfile.lightRays）が持ち、
    /// <see cref="LightRaysLook"/> が描く。画面の明るさは大きく変えないよう、明るい所ほど足す量を減らす（スクリーン合成）。
    /// ネット対戦でも通信せず、各PCが自分のカメラで描く。
    /// </summary>
    [Serializable]
    public sealed class LightRaysSettings
    {
        [Tooltip("OFFにすると光の筋を出さない。")]
        public bool enabled = true;

        [Tooltip("光の筋の色。")]
        public Color color = new Color(1f, 0.96f, 0.86f, 1f);

        [Range(0f, 1.5f), Tooltip("光の筋の明るさ（明るい所ほど控えめに足される）。")]
        public float intensity = 0.3f;

        [Range(-80f, 80f), Tooltip("光の筋の傾き（度）。0で真上から、＋で左上から右下へ、−で右上から左下へ差し込む。")]
        public float angle = 28f;

        [Range(1f, 30f), Tooltip("光の筋の本数の多さ（大きいほど細かく多い）。")]
        public float density = 9f;

        [Range(1f, 8f), Tooltip("筋のくっきりさ（大きいほど、細くはっきりした筋になる）。")]
        public float sharpness = 3f;

        [Range(0.2f, 4f), Tooltip("光が届く長さ（画面の高さ=1）。差し込む側から遠いほど薄くなる。")]
        public float reach = 2.4f;

        [Range(0f, 2f), Tooltip("筋がゆっくり横に流れる速さ。")]
        public float driftSpeed = 0.25f;

        [Range(0f, 1f), Tooltip("光のゆらめき（明るさの揺れ）の大きさ。")]
        public float shimmer = 0.15f;
    }
}
