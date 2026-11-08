using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯のコア（3Dモデル）の見た目の設定（見た目だけ）。<see cref="CoreCrystalController.toothLook"/> が持ち、<see cref="CoreToothLook"/> が使う。
    /// 光の色・脈の速さ・ひびが入り始める HP・白く光る秒数は、宝石の設定（<see cref="CoreCrystalController.gemLook"/>）と同じ値を使う。
    /// 汚れの進み方は <see cref="CoreDirtVisual"/> のカーブを使う。トゥーンの塗り（影の色・輪郭線）は歯の材質で変える。
    /// </summary>
    [Serializable]
    public sealed class CoreToothLookSettings
    {
        [Tooltip("OFFにすると、汚れ・ふちの光・ひびを出さない（トゥーンの塗りだけ）。")]
        public bool enabled = true;

        [Header("汚れ（黄ばみのしみ）")]
        [ColorUsage(false), Tooltip("しみの色（汚れ始め）。")]
        public Color stainColor = new Color(0.93f, 0.8f, 0.48f);

        [ColorUsage(false), Tooltip("HP が少ないときのしみの色（だんだんこの色になる）。")]
        public Color stainDeepColor = new Color(0.62f, 0.45f, 0.26f);

        [Range(0.5f, 10f), Tooltip("しみの細かさ。大きいほど小さなしみがたくさん。")]
        public float stainScale = 3.5f;

        [Range(0f, 1f), Tooltip("根元（下）から汚れていく量。0=場所に関係なく、1=下から順に。")]
        public float stainFromRoot = 0.3f;

        [Header("ふちの光")]
        [Range(0f, 2f), Tooltip("ふちの光の強さ。0なら出さない。")]
        public float rimStrength = 1f;

        [Range(0f, 1f), Tooltip("ふちの光の帯の幅。")]
        public float rimWidth = 0.47f;

        [Range(0f, 1f), Tooltip("ふちの光の帯の境目のぼかし。")]
        public float rimSoftness = 0.07f;

        [Header("ひび（描いた線＋中の光）")]
        [ColorUsage(false), Tooltip("ひびの線の色。光はこの線の中から漏れる（光の色は宝石の設定の「ひびから漏れる光の色」）。")]
        public Color inkColor = new Color(0.19f, 0.165f, 0.21f);

        [Range(1f, 12f), Tooltip("ひびの細かさ。大きいほど細かく割れる。")]
        public float crackScale = 5f;

        [Range(0.02f, 0.3f), Tooltip("ひびの線の太さ。")]
        public float crackWidth = 0.1f;
    }
}
