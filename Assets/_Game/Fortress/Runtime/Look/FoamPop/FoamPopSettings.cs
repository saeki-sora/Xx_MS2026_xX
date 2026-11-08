using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵を倒したときの「光る泡」（見た目と効果音だけ）の設定。2026-11-09 にユーザーが比較ページで選んだ G 案。
    /// 流れ: ① 倒した瞬間、敵のいた所が白い泡のかたまりに包まれて縮む → ② 泡が数個ふくらみ、倒した人の色にほんのり光りながら浮く
    /// → ③ 泡は割れずに縮み、小さな光の点になって昇って消える。近くでたくさん倒れている所では泡の数を減らす（見やすさのため）。
    /// ステージの見た目の設定（StageLookProfile.foamPop）が持ち、<see cref="FoamPopLook"/> が描く。通信しない（各PCで描いて鳴らす）。
    /// </summary>
    [Serializable]
    public sealed class FoamPopSettings
    {
        [Tooltip("OFFにすると光る泡も効果音も出さない。")]
        public bool enabled = true;

        // ── ① 包まれる泡のかたまり ──
        [Range(0f, 0.5f), Tooltip("倒した瞬間の白い泡のかたまりが縮んで消えるまでの秒数。0 で出さない。")]
        public float wrapSeconds = 0.1f;

        [Range(0.2f, 2f), Tooltip("白い泡のかたまりの大きさ（敵の絵の大きさの何倍か）。")]
        public float wrapSize = 0.75f;

        // ── ② 光る泡 ──
        [Range(0, 8), Tooltip("1体あたりの泡の数（まわりが空いているとき）。")]
        public int bubbleCount = 5;

        [Range(0, 8), Tooltip("1体あたりの泡の数（まわりでたくさん倒れているとき）。")]
        public int crowdedBubbleCount = 2;

        [Min(0f), Tooltip("「混んでいる」とみなし始める数（近く 3m 四方で、直前 0.15 秒ほどに倒れた数）。これより少なければ上の数。")]
        public float crowdStart = 3.5f;

        [Min(0f), Tooltip("「とても混んでいる」とみなす数。これ以上なら「たくさん倒れているとき」の数になる（間はなめらかに減る）。")]
        public float crowdFull = 7f;

        [Range(0.02f, 0.5f), Tooltip("一番大きい泡の半径（m）。ほかの泡はこれの半分〜8割ほど。")]
        public float bubbleSize = 0.155f;

        [Range(0f, 0.8f), Tooltip("泡が散らばる広さ（敵のいた所からの半径・m）。")]
        public float bubbleSpread = 0.26f;

        [Range(0.05f, 1.5f), Tooltip("1つめの泡が縮み始めるまでの秒数。泡が見えている長さの目安（全体の長さはこれ＋下の2つ）。")]
        public float bubbleSeconds = 0.2f;

        [Range(0f, 0.3f), Tooltip("2つめ以降の泡が、1つずつ遅れて縮み始める秒数（少しずつ消えていく感じ）。")]
        public float bubbleStagger = 0.06f;

        [Range(0.02f, 0.5f), Tooltip("泡が縮んで光の点に変わるまでの秒数。")]
        public float shrinkSeconds = 0.08f;

        [Range(0f, 3f), Tooltip("泡が浮き上がる速さ（m/秒）。")]
        public float riseSpeed = 0.35f;

        // ── ③ 光の点 ──
        [Range(0f, 1.5f), Tooltip("光の点が消えるまでの秒数。0 で光の点を出さない。")]
        public float dotSeconds = 0.35f;

        [Range(0f, 4f), Tooltip("光の点が昇る速さ（m/秒）。")]
        public float dotRiseSpeed = 0.9f;

        // ── 色 ──
        [Tooltip("泡のふちの内側の帯のミント色（外側の帯は倒した人の色）。")]
        public Color mintColor = new Color(0.23f, 0.76f, 0.69f, 1f);

        [Range(0f, 1f), Tooltip("泡のふちのうち、外側の倒した人の色の帯の割合（0=ミントだけ、1=倒した人の色だけ）。")]
        public float playerColorMix = 0.5f;

        [Range(0f, 1f), Tooltip("泡のまわりの光と光の点を、倒した人の色からどれだけ白に寄せるか（0=そのままの色、1=真っ白）。")]
        public float glowWhiten = 0.35f;

        [Range(0f, 3f), Tooltip("泡のまわりのほんのりした光の強さ（0 で光らない）。")]
        public float glowStrength = 1f;

        [Range(0f, 1f), Tooltip("泡の中の白っぽい塗りの濃さ（0=ふちだけ）。")]
        public float bubbleFill = 0.5f;

        // ── 効果音 ──
        [Tooltip("倒したときの「シュワッ」という音。少しだけ倒したとき用。")]
        public FortressEffect sound = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("たくさんまとめて倒したとき用の音。空なら上の音を使う。")]
        public FortressEffect soundMany = new FortressEffect { tintWithPlayerColor = false };

        [Range(2, 100), Tooltip("この数以上をまとめて倒したら「たくさん」の音にする。")]
        public int manyKillsCount = 8;

        [Range(0.02f, 0.5f), Tooltip("この秒数の間に倒した分は、効果音を1回にまとめる。")]
        public float soundGroupSeconds = 0.08f;

        // ── 負荷 ──
        [Range(8, 1000), Tooltip("1フレームに新しく出す最大数（倒した敵の数。たくさん同時に倒したときの重さを抑える）。")]
        public int maxNewPerFrame = 200;

        [Range(64, 8000), Tooltip("同時に出ている泡・光の点などの部品の最大数。")]
        public int maxParts = 3000;

        [Tooltip("床からどれだけ上（カメラ側）に描くか（m）。")]
        public float lift = 0.45f;
    }
}
