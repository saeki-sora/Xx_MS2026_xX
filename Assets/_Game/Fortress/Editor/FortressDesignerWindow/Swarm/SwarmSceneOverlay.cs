using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Play中のSceneビューに、群衆の密度ヒートマップと空間ハッシュの格子を重ねる。
    /// 「どこで詰まっているか」「押し合いが起きているか」を目で確認するためのデバッグ表示。
    /// シミュレーションは非同期で動いているため、ネイティブ配列には触れず、
    /// SwarmSystemが毎フレームコピーしたセルごとの敵数（スナップショット）だけを読む。
    /// </summary>
    [InitializeOnLoad]
    public static class SwarmSceneOverlay
    {
        public static bool ShowDensity;
        public static bool ShowHashGrid;

        /// <summary>この数が1セルにいると最も濃く（赤く）表示される。</summary>
        public static float DensityReference = 6f;

        private static readonly Vector3[] RectBuffer = new Vector3[4];

        static SwarmSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            var system = SwarmSystem.Current;
            if (system != null)
            {
                system.captureCellCounts = Application.isPlaying && (ShowDensity || ShowHashGrid);
            }

            if ((!ShowDensity && !ShowHashGrid) || !Application.isPlaying || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (system == null || system.Stats.alive == 0)
            {
                return;
            }

            var counts = system.CellCounts;
            var width = system.CellGridWidth;
            var height = system.CellGridHeight;
            if (counts == null || counts.Length != width * height)
            {
                return;
            }

            var origin = system.CellGridOrigin;
            var size = system.CellGridSize;
            var reference = Mathf.Max(1f, DensityReference);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var count = counts[y * width + x];
                    var min = new Vector2(origin.x + x * size, origin.y + y * size);
                    var max = min + Vector2.one * size;

                    var fill = Color.clear;
                    if (ShowDensity && count > 0)
                    {
                        var t = Mathf.Clamp01(count / reference);
                        fill = Color.HSVToRGB(0.6f * (1f - t), 0.85f, 1f);
                        fill.a = 0.15f + 0.4f * t;
                    }

                    var outline = ShowHashGrid ? new Color(1f, 1f, 1f, 0.12f) : Color.clear;
                    if (fill.a > 0f || outline.a > 0f)
                    {
                        DrawRect(min, max, fill, outline);
                    }
                }
            }
        }

        private static void DrawRect(Vector2 min, Vector2 max, Color fill, Color outline)
        {
            RectBuffer[0] = new Vector3(min.x, min.y, 0f);
            RectBuffer[1] = new Vector3(min.x, max.y, 0f);
            RectBuffer[2] = new Vector3(max.x, max.y, 0f);
            RectBuffer[3] = new Vector3(max.x, min.y, 0f);
            Handles.DrawSolidRectangleWithOutline(RectBuffer, fill, outline);
        }
    }
}
