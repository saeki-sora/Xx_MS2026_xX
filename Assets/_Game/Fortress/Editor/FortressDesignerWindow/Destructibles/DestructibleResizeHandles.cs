using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 選択中の破壊可能物の角・辺に、ドラッグで大きさを変えられるハンドルを出す。
    /// 反対側の角/辺を固定したまま伸縮し、グリッドスナップにも従う。回転していても動く。
    /// </summary>
    public static class DestructibleResizeHandles
    {
        private const float MinSize = 0.1f;
        private static readonly Vector2[] Anchors =
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f)
        };

        public static void Draw(DestructibleObstacle obstacle)
        {
            var transform = obstacle.transform;
            var center = (Vector2)transform.position;
            var rotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z);
            var size = new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));

            DrawOutline(center, rotation, size);

            foreach (var anchor in Anchors)
            {
                var half = new Vector2(anchor.x * size.x * 0.5f, anchor.y * size.y * 0.5f);
                var handlePosition = center + (Vector2)(rotation * half);
                var handleSize = HandleUtility.GetHandleSize(handlePosition) * 0.06f;

                Handles.color = Color.white;
                EditorGUI.BeginChangeCheck();
                var moved = Handles.FreeMoveHandle(handlePosition, handleSize, Vector3.zero, Handles.RectangleHandleCap);
                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Resize(transform, center, rotation, size, anchor, DestructibleEditorSettings.Snap((Vector2)moved));
            }

            Handles.Label(center + (Vector2)(rotation * new Vector2(0f, size.y * 0.5f + 0.1f)) + Vector2.up * 0.35f,
                $"{size.x:0.##} × {size.y:0.##}", EditorStyles.miniBoldLabel);
        }

        private static void Resize(Transform transform, Vector2 center, Quaternion rotation, Vector2 size, Vector2 anchor, Vector2 dragged)
        {
            // 動かさない側（反対側の角/辺の中点）を基準に、新しい大きさと中心を求める。
            var fixedPoint = center - (Vector2)(rotation * new Vector2(anchor.x * size.x * 0.5f, anchor.y * size.y * 0.5f));
            var local = (Vector2)(Quaternion.Inverse(rotation) * (dragged - fixedPoint));

            var newSize = new Vector2(
                anchor.x != 0f ? Mathf.Max(MinSize, local.x * anchor.x) : size.x,
                anchor.y != 0f ? Mathf.Max(MinSize, local.y * anchor.y) : size.y);
            var newCenter = fixedPoint + (Vector2)(rotation * new Vector2(anchor.x * newSize.x * 0.5f, anchor.y * newSize.y * 0.5f));

            Undo.RecordObject(transform, "Resize Destructible");
            transform.position = new Vector3(newCenter.x, newCenter.y, transform.position.z);
            transform.localScale = new Vector3(newSize.x, newSize.y, transform.localScale.z);
            NavigationObstacle.NotifyChanged();
        }

        private static void DrawOutline(Vector2 center, Quaternion rotation, Vector2 size)
        {
            var corners = new Vector3[5];
            for (var i = 0; i < 4; i++)
            {
                var sx = i == 0 || i == 3 ? -1f : 1f;
                var sy = i < 2 ? -1f : 1f;
                corners[i] = center + (Vector2)(rotation * new Vector2(sx * size.x * 0.5f, sy * size.y * 0.5f));
            }

            // 左下→右下→右上→左上の順で、最後に左下へ戻して閉じる。
            corners[4] = corners[0];

            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f, corners);
        }
    }
}
