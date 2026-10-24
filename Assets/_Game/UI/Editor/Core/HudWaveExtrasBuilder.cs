using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;
using H = MS2026.UI.EditorTools.HudKillCounterBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ウェーブの表示の続き（画面の外から来る敵の矢印・歯みがきタイム・初めての人向けのヒント）を HUD に入れる。<see cref="HudWaveBuilder"/> から呼ぶ。
    /// </summary>
    internal static class HudWaveExtrasBuilder
    {
        internal const string EdgeName = "WaveEdgeMarkers";

        /// <summary>このビルダーが作る部品の名前（作り直すときに消す）。</summary>
        internal static readonly string[] OwnedNames = { EdgeName };

        public static void Build(Transform screen)
        {
            HudWaveBuilder.Remove(screen, OwnedNames);
            BuildEdgeMarkers(screen);
        }

        // 画面の外から来る敵の矢印（赤い線と同じ間だけ、画面のふちに出る）。
        private static void BuildEdgeMarkers(Transform screen)
        {
            var root = B.Rect(screen, EdgeName);
            B.Stretch(root);
            var markers = root.gameObject.AddComponent<WaveEdgeMarkers>();
            markers.arrow = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Arrow.png");
            root.SetAsFirstSibling(); // ほかの表示より奥
        }
    }
}
