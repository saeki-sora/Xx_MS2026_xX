using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアが危ないほど画面全体が赤く熱っぽくなる演出の設定（見た目だけ）。<see cref="CoreCrystalController.dangerLook"/> が持ち、
    /// <see cref="CoreDangerLook"/> が使う。ネット対戦でも通信せず、各PCが自分の画面で描く（Clientにもコアの HP は届いている）。
    /// </summary>
    [Serializable]
    public sealed class CoreDangerLookSettings
    {
        [Tooltip("OFFにすると、コアの HP が減っても画面の色は変わらない。")]
        public bool enabled = true;

        [Range(0.05f, 1f), Tooltip("コアの HP がこの割合を下回ると、画面が赤くなり始める（0〜1。0.6なら残り60%から）。")]
        public float startHealth01 = 0.6f;

        [Tooltip("一番危ないときの画面の色（白に近いほど控えめ）。")]
        public Color tint = new Color(1f, 0.74f, 0.66f, 1f);

        [Range(-50f, 50f), Tooltip("一番危ないときの鮮やかさの変化（＋で鮮やか、−でくすむ）。")]
        public float saturation = 12f;

        [Tooltip("一番危ないときに画面のふちを染める色。")]
        public Color edgeColor = new Color(0.55f, 0f, 0.02f, 1f);

        [Range(0f, 1f), Tooltip("一番危ないときの画面のふちの暗さ（0で出さない）。")]
        public float edgeStrength = 0.38f;

        [Range(0f, 1f), Tooltip("コアの HP がこの割合を下回ると、ふちが心臓の鼓動のようにドクンドクンと脈打つ（0で脈打たない）。")]
        public float pulseBelowHealth01 = 0.25f;

        [Min(0f), Tooltip("鼓動の速さ（回/秒）。")]
        public float pulsePerSecond = 1.4f;

        [Min(0.1f), Tooltip("HP が変わったとき、色が追いつく速さ。大きいほどすぐ変わる。")]
        public float response = 1.5f;
    }
}
