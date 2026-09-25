using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Sceneビューでの配置。クリックで既定の大きさ、ドラッグで好きな範囲の大きさで破壊可能物を置く。
    /// 位置・大きさはグリッドスナップに従い、Escで配置モードを終了する。
    /// </summary>
    [InitializeOnLoad]
    public static class DestructiblePlacementTool
    {
        private const float DragThreshold = 0.2f;

        private static bool _dragging;
        private static Vector2 _dragStart;
        private static Vector2 _dragEnd;

        static DestructiblePlacementTool()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!DestructibleEditorSettings.PlacementMode || Application.isPlaying)
            {
                _dragging = false;
                return;
            }

            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseMove:
                    sceneView.Repaint();
                    break;

                case EventType.MouseDown when e.button == 0 && !e.alt:
                    _dragStart = _dragEnd = Snapped(MouseWorld(e));
                    _dragging = true;
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    _dragEnd = Snapped(MouseWorld(e));
                    e.Use();
                    sceneView.Repaint();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id && e.button == 0:
                    GUIUtility.hotControl = 0;
                    _dragging = false;
                    PlaceFromDrag();
                    e.Use();
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Escape:
                    DestructibleEditorSettings.PlacementMode = false;
                    _dragging = false;
                    e.Use();
                    break;

                case EventType.Repaint:
                    DrawPreview(e);
                    break;
            }
        }

        private static void PlaceFromDrag()
        {
            Vector2 center;
            Vector2 size;

            if (Vector2.Distance(_dragStart, _dragEnd) < DragThreshold)
            {
                center = _dragStart;
                size = DestructibleEditorSettings.CurrentPlaceSize();
            }
            else
            {
                center = (_dragStart + _dragEnd) * 0.5f;
                size = new Vector2(Mathf.Abs(_dragEnd.x - _dragStart.x), Mathf.Abs(_dragEnd.y - _dragStart.y));
            }

            var obstacle = DestructibleFactory.Create(DestructibleEditorSettings.ActivePreset, center, size);
            Selection.activeGameObject = obstacle.gameObject;
        }

        private static void DrawPreview(Event e)
        {
            Vector2 center;
            Vector2 size;

            if (_dragging && Vector2.Distance(_dragStart, _dragEnd) >= DragThreshold)
            {
                center = (_dragStart + _dragEnd) * 0.5f;
                size = new Vector2(Mathf.Abs(_dragEnd.x - _dragStart.x), Mathf.Abs(_dragEnd.y - _dragStart.y));
            }
            else
            {
                center = _dragging ? _dragStart : Snapped(MouseWorld(e));
                size = DestructibleEditorSettings.CurrentPlaceSize();
            }

            var half = size * 0.5f;
            var corners = new[]
            {
                new Vector3(center.x - half.x, center.y - half.y),
                new Vector3(center.x - half.x, center.y + half.y),
                new Vector3(center.x + half.x, center.y + half.y),
                new Vector3(center.x + half.x, center.y - half.y)
            };
            Handles.DrawSolidRectangleWithOutline(corners, new Color(0.4f, 0.8f, 1f, 0.25f), new Color(0.4f, 0.8f, 1f, 1f));
            Handles.Label(center + Vector2.up * (half.y + 0.35f), $"{size.x:0.##} × {size.y:0.##}", EditorStyles.miniBoldLabel);
        }

        private static Vector2 Snapped(Vector2 world)
        {
            return DestructibleEditorSettings.Snap(world);
        }

        /// <summary>マウス位置を、z=0の平面上のワールド座標に変換する。</summary>
        private static Vector2 MouseWorld(Event e)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var plane = new Plane(Vector3.back, Vector3.zero);
            return plane.Raycast(ray, out var distance) ? (Vector2)ray.GetPoint(distance) : (Vector2)ray.origin;
        }
    }
}
