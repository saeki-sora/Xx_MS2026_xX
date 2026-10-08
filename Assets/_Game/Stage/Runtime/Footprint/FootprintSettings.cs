using System;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 3Dモデルから「敵が通れない範囲（床の上の輪郭）」を作るときの設定。
    /// 床はXY平面（z=0）、高さはカメラ側（-Z）に向かって増える。
    /// </summary>
    [Serializable]
    public struct FootprintSettings
    {
        [Tooltip("床からこの高さまでにある部分だけを「壁」とみなす。浮いている部分（例: 横たわった歯ブラシの持ち手）の下は敵がくぐれる。")]
        [Min(0.01f)]
        public float blockHeight;

        [Tooltip("輪郭をこの距離だけ外側へ広げる。敵がモデルにめり込んで見えるときに増やす（ワールド単位）。")]
        [Min(0f)]
        public float padding;

        [Tooltip("この幅より狭い隙間・くぼみを埋める。細かい凹凸で敵が引っかかるときに増やす（ワールド単位）。")]
        [Min(0f)]
        public float closeGaps;

        [Tooltip("形を調べるマス目の細かさ（ワールド単位）。小さいほど正確だが計算が重い。")]
        [Min(0.005f)]
        public float resolution;

        [Tooltip("輪郭の頂点を間引く強さ（ワールド単位）。大きいほど角が丸まり軽くなる。")]
        [Min(0f)]
        public float simplify;

        [Tooltip("ONなら、輪郭の内側にできた穴（筒の中など）を埋める。普通はON。")]
        public bool fillHoles;

        [Tooltip("この面積より小さい切れ端は捨てる（ワールド単位²）。")]
        [Min(0f)]
        public float minArea;

        public static FootprintSettings Default => new FootprintSettings
        {
            blockHeight = 0.6f,
            padding = 0.05f,
            closeGaps = 0.1f,
            resolution = 0.05f,
            simplify = 0.04f,
            fillHoles = true,
            minArea = 0.02f
        };

        /// <summary>範囲外の値を安全な値に直したコピー。</summary>
        public FootprintSettings Sanitized()
        {
            var s = this;
            s.blockHeight = Mathf.Max(0.01f, s.blockHeight);
            s.padding = Mathf.Max(0f, s.padding);
            s.closeGaps = Mathf.Max(0f, s.closeGaps);
            s.resolution = Mathf.Max(0.005f, s.resolution);
            s.simplify = Mathf.Max(0f, s.simplify);
            s.minArea = Mathf.Max(0f, s.minArea);
            return s;
        }
    }
}
