using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// 砲台の SD の絵を見やすくする処理の値（<see cref="SdArtWindow"/> で調整する）。Assets/RedSkill/Data/SdArtSettings.asset に1つ置く。
    /// 窓の幅（〜の幅）は奇数に直して使う（偶数なら +1）。
    /// </summary>
    public sealed class SdArtSettings : ScriptableObject
    {
        [Tooltip("この暗さより暗い紺色を「線」とみなす（0〜1。小さいほど本当に暗い線だけ）。元の絵の線は 0.05〜0.10 くらい。")]
        [Range(0.02f, 0.6f)] public float lineMaxLum = 0.15f;
        [Tooltip("線とみなす青さ（青が赤・緑よりどれだけ強いか）。大きいほど、はっきり青い線だけ。")]
        [Range(0f, 0.6f)] public float lineBlueLead = 0.2f;
        [Tooltip("これより太い濃い青は「塗り」（手袋・髪など）とみなして太くしない（元の絵のピクセル。0 で区別しない）。")]
        [Range(0, 31)] public int fillSize = 9;
        [Tooltip("線を太くする幅（元の絵のピクセル。0 で太くしない）。大きくしすぎると目がつぶれる。")]
        [Range(0, 15)] public int lineGrow = 5;
        [Tooltip("太くした線のふちのぼかし。")]
        [Range(0f, 3f)] public float lineSoft = 0.8f;

        [Tooltip("色の鮮やかさ（1 = そのまま）。")]
        [Range(0.5f, 2f)] public float saturation = 1.15f;
        [Tooltip("明るい所と暗い所の差（1 = そのまま）。")]
        [Range(0.5f, 2f)] public float contrast = 1.1f;

        [Tooltip("キャラのまわりの紺のふちの太さ（書き出す絵のピクセル。0 でなし）。")]
        [Range(0, 21)] public int rim = 7;
        [Tooltip("その外の白いふちの太さ（0 でなし）。")]
        [Range(0, 31)] public int stickerWhite = 13;
        [Tooltip("白いふちの外の細い紺の線の太さ（0 でなし）。")]
        [Range(0, 15)] public int stickerLine = 5;

        [Tooltip("まわりのうっすら影のぼかし（0 で影なし）。")]
        [Range(0f, 16f)] public float shadowBlur = 6f;
        [Tooltip("影の広がり。")]
        [Range(0, 21)] public int shadowGrow = 5;
        [Tooltip("影の濃さ（0〜1）。")]
        [Range(0f, 1f)] public float shadowAlpha = 0.7f;

        [Tooltip("書き出す絵のキャラ部分の高さ（ピクセル）。ゲームでは 100 ピクセルくらいで表示されるので、512 で十分。")]
        [Range(128, 1024)] public int outHeight = 512;
        [Tooltip("ふち・影がはみ出さないように足す余白（片側のピクセル）。足りないとふちや影が切れる。")]
        [Range(0, 64)] public int pad = 24;

        [Tooltip("プレビューで、キャラを何ピクセルの高さで見るか（ゲームの画面では 70〜150 くらい）。")]
        [Range(40, 300)] public int previewHeight = 100;

        [HideInInspector]
        [Tooltip("最後に書き出したときの「絵全体の高さ ÷ キャラの高さ」。余白を変えたとき、一覧の絵の高さを同じ割合で直すために覚えておく。")]
        public float exportedCanvasScale = 1.09375f;

        public SdArtParams ToParams() => new SdArtParams
        {
            LineMaxLum = lineMaxLum, LineBlueLead = lineBlueLead, FillSize = Odd(fillSize), LineGrow = Odd(lineGrow), LineSoft = lineSoft,
            Saturation = saturation, Contrast = contrast, OutHeight = outHeight, Rim = Odd(rim), Pad = pad,
            StickerWhite = Odd(stickerWhite), StickerLine = Odd(stickerLine), ShadowBlur = shadowBlur, ShadowGrow = Odd(shadowGrow), ShadowAlpha = shadowAlpha,
        };

        /// <summary>今の値をすべて最初の値に戻す（書き出しの記録は残す）。</summary>
        public void ResetValues()
        {
            var keep = exportedCanvasScale;
            var fresh = CreateInstance<SdArtSettings>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), this);
            DestroyImmediate(fresh);
            exportedCanvasScale = keep;
        }

        private static int Odd(int size) => size <= 1 ? 0 : size % 2 == 0 ? size + 1 : size;
    }
}
