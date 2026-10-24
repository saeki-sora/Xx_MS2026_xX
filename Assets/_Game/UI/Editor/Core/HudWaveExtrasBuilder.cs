using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ウェーブの表示の続き（歯みがきタイム・初めての人向けのヒント）を HUD に入れる。<see cref="HudWaveBuilder"/> から呼ぶ。
    /// </summary>
    internal static class HudWaveExtrasBuilder
    {
        /// <summary>このビルダーが作る部品の名前（作り直すときに消す）。</summary>
        internal static readonly string[] OwnedNames = { };

        public static void Build(Transform screen)
        {
            HudWaveBuilder.Remove(screen, OwnedNames);
        }
    }
}
