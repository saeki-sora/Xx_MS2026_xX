using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// カメラタブ表示中、各視点に映る範囲をSceneにプレイヤー色の枠で描き、選択中の視点はハンドルで直接操作できるようにする。
    /// 中央の四角=見る場所をドラッグ / 円=画面の回転 / 上辺の四角=ズーム。▲の付いた辺が「画面の上」。
    /// </summary>
    [InitializeOnLoad]
    public static class CameraSceneOverlay
    {
        private static readonly Vector3[] Quad = new Vector3[4];
        private static readonly Vector3[] Outline = new Vector3[5];
        private static readonly Vector3[] SafeQuad = new Vector3[4];
        private static GUIStyle _labelStyle;

        static CameraSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!CameraToolState.TabVisible || !CameraToolState.ShowSceneFrames)
            {
                return;
            }

            var rig = FortressCameraRig.Active != null ? FortressCameraRig.Active : Object.FindFirstObjectByType<FortressCameraRig>();
            var preset = rig != null ? rig.preset : null;
            if (preset == null)
            {
                return;
            }

            var aspect = rig.guides.ReferenceAspect;
            var selected = CameraToolState.SelectedViewer;

            if (Event.current.type == EventType.Repaint)
            {
                foreach (var viewer in ViewerIndex.All)
                {
                    if (viewer != selected && !CameraToolState.ShowOnlySelectedFrame)
                    {
                        DrawFrame(preset.GetView(viewer), aspect, viewer, false, rig.guides);
                    }
                }

                // 選択中の枠は最後に描いて一番上に重ねる。
                DrawFrame(preset.GetView(selected), aspect, selected, true, rig.guides);
            }

            if (CameraToolState.ShowSceneHandles)
            {
                DrawHandles(preset, selected, aspect);
            }
        }

        private static void DrawFrame(in CameraViewSettings view, float aspect, int viewer, bool selected, CameraGuideSettings guides)
        {
            var color = ViewerIndex.Color(viewer);
            color.a = selected ? 1f : 0.45f;

            CameraViewMath.GetGroundQuad(view, aspect, Quad);
            for (var i = 0; i < 4; i++)
            {
                Outline[i] = Quad[i];
            }

            Outline[4] = Quad[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(selected ? 4f : 2f, Outline);

            // 上辺(左上→右上)の中央に▲を付けて「画面の上」を示す。
            var topMid = (Quad[1] + Quad[2]) * 0.5f;
            _labelStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.LowerCenter };
            _labelStyle.normal.textColor = color;
            Handles.Label(topMid, $"▲ {ViewerIndex.LongLabel(viewer)}", _labelStyle);

            if (selected && CameraToolState.ShowSafeAreaInScene)
            {
                CameraViewMath.GetGroundQuad(view, aspect, guides.GetSafeRect(new Rect(0f, 0f, 1f, 1f)), SafeQuad);
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                Handles.DrawDottedLines(new[] { SafeQuad[0], SafeQuad[1], SafeQuad[1], SafeQuad[2], SafeQuad[2], SafeQuad[3], SafeQuad[3], SafeQuad[0] }, 4f);
            }
        }

        private static void DrawHandles(CameraViewPreset preset, int viewer, float aspect)
        {
            var view = preset.GetView(viewer);
            var edited = view;
            var center = (Vector3)view.center;
            var handleSize = HandleUtility.GetHandleSize(center);
            Handles.color = ViewerIndex.Color(viewer);

            EditorGUI.BeginChangeCheck();
            var movedCenter = Handles.FreeMoveHandle(center, handleSize * 0.12f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                edited.center = movedCenter;
            }

            EditorGUI.BeginChangeCheck();
            var radius = view.VisibleHalfHeight * 0.5f;
            var rotation = Handles.Disc(Quaternion.Euler(0f, 0f, view.rollDegrees), center, Vector3.forward, radius, false, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                edited.rollDegrees = rotation.eulerAngles.z;
            }

            edited = DrawZoomHandle(view, edited, aspect, handleSize);

            if (!edited.Approximately(view))
            {
                CameraPresetEditing.SetView(preset, viewer, edited, "Edit Camera View In Scene");
            }
        }

        /// <summary>上辺の中央のハンドルを画面の上下方向に引くとズームする(辺が中心から離れるほど広く映る)。</summary>
        private static CameraViewSettings DrawZoomHandle(in CameraViewSettings view, CameraViewSettings edited, float aspect, float handleSize)
        {
            CameraViewMath.GetGroundQuad(view, aspect, Quad);
            var topMid = (Quad[1] + Quad[2]) * 0.5f;
            var up = (Vector3)CameraViewMath.ScreenUp(view);
            var center = (Vector3)view.center;
            var before = Vector3.Dot(topMid - center, up);
            if (before <= 1e-3f)
            {
                return edited;
            }

            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(topMid, up, handleSize * 0.1f, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
            {
                return edited;
            }

            var after = Mathf.Max(0.1f, Vector3.Dot(moved - center, up));
            return edited.WithVisibleHalfHeight(view.VisibleHalfHeight * after / before);
        }
    }
}
