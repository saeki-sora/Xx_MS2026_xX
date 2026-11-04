using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 空中をただよう環境の粒（データの粒＝0と1の文字・泡・湯気）の設定。見た目だけ。ステージごと（StageSet.ambientMotes）。
    /// 敵（小さな塗りつぶしの玉・輪郭線と目がある）と見間違えないよう、粒は「シアンと白だけ・塗りつぶさない（文字・輪・ぼかし）」にしている。
    /// <see cref="AmbientMotesLook"/> が、ゲームのカメラに映っている範囲の空中に散らして描く。通信しない（各PCが自分の画面だけ）。
    /// </summary>
    [Serializable]
    public sealed class AmbientMoteSettings
    {
        [Tooltip("OFFにすると粒を出さない。")]
        public bool enabled = true;

        [Header("数（画面に同時に出ている数）")]
        [Range(0, 200), Tooltip("データの粒（0と1の文字）の数。サイバーなステージ向け。")]
        public int dataCount = 36;

        [Range(0, 100), Tooltip("泡（輪だけの丸）の数。")]
        public int bubbleCount = 6;

        [Range(0, 40), Tooltip("湯気（大きなぼんやりした白）の数。お風呂場向け。")]
        public int steamCount = 0;

        [Header("色（敵と見間違えないよう、シアンと白にする）")]
        public Color dataColor = new Color(0.55f, 1f, 1f, 0.8f);

        public Color bubbleColor = new Color(0.9f, 1f, 1f, 0.7f);

        public Color steamColor = new Color(1f, 1f, 1f, 0.12f);

        [Header("大きさ（m）")]
        [Range(0.05f, 1f), Tooltip("データの粒の文字の高さ。敵より小さくする。")]
        public float dataSize = 0.3f;

        [Tooltip("泡の大きさ（最小〜最大）。")]
        public Vector2 bubbleSize = new Vector2(0.28f, 0.55f);

        [Tooltip("湯気の大きさ（最小〜最大）。敵よりずっと大きくする。")]
        public Vector2 steamSize = new Vector2(2f, 3.4f);

        [Header("動き")]
        [Tooltip("床からの高さ（最小〜最大, m）。空中にいて足元の影が無いので、床を歩く敵と分かれる。")]
        public Vector2 height = new Vector2(0.6f, 2.4f);

        [Min(0f), Tooltip("画面の上へゆっくり流れる速さ（m/秒）。床を這う敵とは違う向きに動かす。")]
        public float riseSpeed = 0.3f;

        [Min(0f), Tooltip("左右にゆらゆら揺れる大きさ（m）。")]
        public float sway = 0.25f;

        [Tooltip("1つの粒が見えている秒数（最小〜最大）。出るとき・消えるときはふわっと変わる。")]
        public Vector2 lifeSeconds = new Vector2(4f, 9f);
    }
}
