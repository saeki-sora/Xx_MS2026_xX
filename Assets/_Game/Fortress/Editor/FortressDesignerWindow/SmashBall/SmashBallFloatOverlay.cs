using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>選択中のスマッシュボールが漂う範囲をSceneに枠で表示する。「浮遊する範囲」の設定を目で確認できるようにする。</summary>
    [InitializeOnLoad]
    public static class SmashBallFloatOverlay
    {
        private static readonly Color BoundsColor = new Color(1f, 0.55f, 0.9f, 0.9f);

        static SmashBallFloatOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.color = BoundsColor;
            foreach (var module in SmashBallSelection.Get())
            {
                var floater = module.GetComponent<SmashBallFloater>();
                if (floater == null || !module.settings.floating.enabled)
                {
                    continue;
                }

                var (center, size) = floater.GetWanderBounds();
                DrawRect(center, size);
                Handles.Label(center + new Vector2(0f, size.y * 0.5f + 0.3f), "浮遊範囲", EditorStyles.miniBoldLabel);
            }
        }

        private static void DrawRect(Vector2 center, Vector2 size)
        {
            var half = size * 0.5f;
            var a = new Vector3(center.x - half.x, center.y - half.y);
            var b = new Vector3(center.x + half.x, center.y - half.y);
            var c = new Vector3(center.x + half.x, center.y + half.y);
            var d = new Vector3(center.x - half.x, center.y + half.y);

            // DrawDottedLinesは2点1組で1本の線分。4辺ぶんのペアを渡す。
            Handles.DrawDottedLines(new[] { a, b, b, c, c, d, d, a }, 4f);
        }
    }
}
