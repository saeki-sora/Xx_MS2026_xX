using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵（群衆）の質感（見た目だけ。2026-11-09 見た目強化）の設定。ステージの見た目の設定（StageLookProfile.enemyLook）が持ち、
    /// <see cref="EnemyLookGlobals"/> が群衆のシェーダーへ入れる。
    /// ・縁を濃くする: 明るい床の上でも敵の形がはっきり読めるように（主役が読める）
    /// ・ぷるぷる: 1秒12コマで揺れる（なめらかすぎない、アニメのリズム）
    /// ・当たった瞬間に横へぷにっと潰れる（今の白く光る被弾と一緒に）
    /// 通信しない（各PCで描く）。当たり判定・動きには関係しない。
    /// </summary>
    [Serializable]
    public sealed class EnemyLookSettings
    {
        [Tooltip("OFFにすると、敵の質感（縁・ぷるぷる・潰れ）を出さない（今までの見た目）。")]
        public bool enabled = true;

        [Range(0f, 1f), Tooltip("絵の縁を濃い色に寄せる量（0で出さない）。明るい床の上での敵の見やすさ。")]
        public float edgeDarken = 0.55f;

        [Tooltip("縁の色（濃い紫にすると、床のミントや白いビームの上でもはっきりする）。")]
        public Color edgeColor = new Color(0.17f, 0.08f, 0.27f, 1f);

        [Range(0.01f, 0.2f), Tooltip("縁の太さ（絵の1コマの大きさに対する割合）。")]
        public float edgeWidth = 0.05f;

        [Range(0f, 0.2f), Tooltip("ぷるぷる揺れる量（0で揺らさない）。")]
        public float jiggle = 0.06f;

        [Range(2f, 60f), Tooltip("揺れのコマ数（1秒あたり）。少ないほどカクッとしたアニメらしい揺れ。60でなめらか。")]
        public float jiggleFramesPerSecond = 12f;

        [Range(0f, 0.5f), Tooltip("ビームが当たった瞬間に横へ潰れる量（0で潰れない）。")]
        public float hitSquash = 0.18f;
    }
}
