using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵を倒した所に出す、アニメ調の破裂（見た目だけ）の設定。今までの撃破の光・タレットへ飛ぶ火花はそのままで、その上に重ねる。
    /// 3つの層: ①最初の2コマだけ光る白い星（芯の白飛び） ②倒した人の色のしずくが飛び散る（ベタ塗り＋白いつや） ③色の輪。
    /// 演出だけをコマ打ち（ゲームは60コマのまま、破裂は12コマ/秒で動く）にして、アニメのキレを出す。
    /// ステージの見た目の設定（StageLookProfile.killBurst）が持ち、<see cref="KillBurstLook"/> が描く。通信しない（各PCで描く）。
    /// </summary>
    [Serializable]
    public sealed class KillBurstSettings
    {
        [Tooltip("OFFにすると破裂を出さない。")]
        public bool enabled = true;

        [Range(0.1f, 2f), Tooltip("破裂の大きさ（m。しずくが飛ぶ距離の目安）。")]
        public float size = 0.9f;

        [Range(0.15f, 1.5f), Tooltip("破裂が消えるまでの秒数。")]
        public float lifeSeconds = 0.42f;

        [Range(4f, 60f), Tooltip("破裂を動かすコマ数（1秒あたり）。少ないほどカクッとしたアニメらしい動きになる。60でなめらか。")]
        public float framesPerSecond = 12f;

        [Range(0, 12), Tooltip("飛び散るしずくの数。")]
        public int dropCount = 6;

        [Min(0f), Tooltip("白い星の明るさ（1より大きいと光のにじみが付く）。")]
        public float starBrightness = 2.4f;

        [Min(0f), Tooltip("しずくと輪の明るさ（倒した人の色に掛ける）。")]
        public float colorBrightness = 1.25f;

        [Range(1, 64), Tooltip("1フレームに新しく出す破裂の最大数（たくさん同時に倒したときの重さを抑える）。")]
        public int maxNewPerFrame = 16;

        [Range(16, 512), Tooltip("同時に出ている破裂の最大数。")]
        public int maxActive = 220;

        [Tooltip("床からどれだけ上（カメラ側）に描くか（m）。")]
        public float lift = 0.45f;
    }
}
