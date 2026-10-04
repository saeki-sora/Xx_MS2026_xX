using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 選択中の視点1つを編集する。見る場所・画面の回転・ズーム・投影方式・傾きを、スライダーとワンクリックのボタンで調整できる。
    /// Sceneの枠をドラッグしても同じ値が変わる(<see cref="CameraSceneOverlay"/>)。
    /// </summary>
    public sealed class CameraViewEditPanel
    {
        private static readonly string[] ProjectionLabels = { "正投影 (2D)", "透視投影 (FOV)" };

        public void Draw(CameraTabContext context)
        {
            var viewer = context.SelectedViewer;
            if (!CameraGui.Section("edit", $"{ViewerIndex.LongLabel(viewer)}の視点を編集"))
            {
                return;
            }

            var preset = context.Preset;
            var view = preset.GetView(viewer);
            var edited = view;

            using (new EditorGUILayout.HorizontalScope())
            {
                CameraGui.ColorChip(ViewerIndex.Color(viewer));
                EditorGUILayout.LabelField(DescribeFraming(view, context.Aspect), EditorStyles.miniLabel);
            }

            edited = DrawProjection(edited);
            edited = DrawCenter(edited, viewer, context.Points);
            edited = DrawRoll(edited, viewer, context.Points);
            edited = DrawZoom(edited);
            edited = DrawTilt(edited);

            if (!edited.Approximately(view))
            {
                CameraPresetEditing.SetView(preset, viewer, edited, "Edit Camera View");
            }

            DrawUtilityButtons(context, viewer, view);
        }

        private static CameraViewSettings DrawProjection(CameraViewSettings view)
        {
            var index = GUILayout.Toolbar((int)view.projection, ProjectionLabels);
            return view.WithProjection((CameraProjection)index);
        }

        private static CameraViewSettings DrawCenter(CameraViewSettings view, int viewer, CameraScenePoints points)
        {
            view.center = EditorGUILayout.Vector2Field(new GUIContent("見る場所(画面の中心)", "画面の中心に来るワールド座標。Sceneの枠の中央の四角をドラッグしても動かせます。"), view.center);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);

                if (GUILayout.Button(new GUIContent("マップ中心へ", "コア(無ければ経路フィールドの中心)を画面の中心にします。")))
                {
                    view.center = points.MapCenter;
                }

                using (new EditorGUI.DisabledScope(!ViewerIndex.IsPlayer(viewer) || !points.TryGetTurret(viewer, out _)))
                {
                    if (GUILayout.Button(new GUIContent("自分の砲台へ", "このプレイヤーの砲台を画面の中心にします。")) && points.TryGetTurret(viewer, out var turret))
                    {
                        view.center = turret;
                    }
                }

                if (GUILayout.Button("原点へ"))
                {
                    view.center = Vector2.zero;
                }
            }

            return view;
        }

        private static CameraViewSettings DrawRoll(CameraViewSettings view, int viewer, CameraScenePoints points)
        {
            view.rollDegrees = EditorGUILayout.Slider(
                new GUIContent("画面の回転(度)", "0で+Yが画面の上。Sceneの枠の円をドラッグしても回せます。"), view.rollDegrees, -180f, 180f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);

                if (GUILayout.Button("-90°"))
                {
                    view.rollDegrees -= 90f;
                }

                if (GUILayout.Button("0°"))
                {
                    view.rollDegrees = 0f;
                }

                if (GUILayout.Button("+90°"))
                {
                    view.rollDegrees += 90f;
                }

                if (GUILayout.Button("180°"))
                {
                    view.rollDegrees += 180f;
                }

                using (new EditorGUI.DisabledScope(!ViewerIndex.IsPlayer(viewer) || !points.TryGetTurret(viewer, out _)))
                {
                    if (GUILayout.Button(new GUIContent("自分の砲台を下に", "画面の中心から見て、このプレイヤーの砲台が画面の真下(手前)に来るよう回転します。"))
                        && points.TryGetTurret(viewer, out var turret))
                    {
                        view.rollDegrees = CameraViewMath.RollToPlaceAtBottom(view.center, turret, CameraToolState.SnapRollTo90 ? 90f : 0f);
                    }
                }
            }

            return view;
        }

        private static CameraViewSettings DrawZoom(CameraViewSettings view)
        {
            if (view.IsOrthographic)
            {
                view.orthographicSize = EditorGUILayout.Slider(
                    new GUIContent("映す広さ(Orthographic Size)", "画面の縦半分に映るワールドの長さ。小さいほどズームイン。Sceneの枠の上辺の四角をドラッグしても変えられます。"),
                    view.orthographicSize, 1f, 80f);
            }
            else
            {
                view.fieldOfView = EditorGUILayout.Slider(
                    new GUIContent("視野角(FOV)", "縦の視野角(度)。小さいほどズームイン(望遠)、大きいほど広角。"), view.fieldOfView, 5f, 120f);
            }

            view.distance = EditorGUILayout.Slider(
                new GUIContent("カメラの距離", view.IsOrthographic
                    ? "正投影では見た目の大きさは変わりません(描画距離の範囲にだけ影響)。"
                    : "透視投影では遠いほど映る範囲が広がり、パース(遠近感)が弱まります。"),
                view.distance, 1f, 200f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);

                if (GUILayout.Button("寄る (-10%)"))
                {
                    view = view.WithVisibleHalfHeight(view.VisibleHalfHeight * 0.9f);
                }

                if (GUILayout.Button("引く (+10%)"))
                {
                    view = view.WithVisibleHalfHeight(view.VisibleHalfHeight / 0.9f);
                }
            }

            return view;
        }

        private static CameraViewSettings DrawTilt(CameraViewSettings view)
        {
            view.tiltDegrees = EditorGUILayout.Slider(
                new GUIContent("見下ろしの傾き(度)", "0で真上から。増やすと画面の手前側から奥を覗き込む角度になります(透視投影と組み合わせると奥行きが出ます)。" +
                    "「絵を立たせる」をONにすると、傾けた分だけ砲台・コア・敵の絵が起き上がります。"),
                view.tiltDegrees, 0f, CameraViewSettings.MaxTiltDegrees);
            return view;
        }

        private static void DrawUtilityButtons(CameraTabContext context, int viewer, CameraViewSettings view)
        {
            var preset = context.Preset;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("コピー", "この視点を覚えておき、別の視点に「貼り付け」できます。")))
                {
                    CameraToolState.Clipboard = view;
                }

                using (new EditorGUI.DisabledScope(!CameraToolState.Clipboard.HasValue))
                {
                    if (GUILayout.Button("貼り付け") && CameraToolState.Clipboard.HasValue)
                    {
                        CameraPresetEditing.SetView(preset, viewer, CameraToolState.Clipboard.Value, "Paste Camera View");
                    }
                }

                using (new EditorGUI.DisabledScope(!ViewerIndex.IsPlayer(viewer)))
                {
                    if (GUILayout.Button(new GUIContent("全体視点に戻す", "この視点を全体視点と同じにします。")))
                    {
                        CameraPresetEditing.SetView(preset, viewer, preset.overview, "Reset Camera View");
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Sceneの見え方を取り込む", "最後に操作したSceneビューの中心・回転・広さを、この視点に取り込みます。")))
                {
                    if (CameraSceneViewBridge.TryCaptureFromSceneView(view, out var captured))
                    {
                        CameraPresetEditing.SetView(preset, viewer, captured, "Capture Scene View");
                    }
                }

                if (GUILayout.Button(new GUIContent("Sceneで見る", "この視点に映る範囲が収まるようSceneビューを移動します(2Dモードでは回転は反映されません)。")))
                {
                    CameraSceneViewBridge.FrameInSceneView(view, context.Aspect);
                }
            }
        }

        private static string DescribeFraming(in CameraViewSettings view, float aspect)
        {
            var height = view.VisibleHalfHeight * 2f;
            return $"中心の高さで 横 {height * aspect:0.0} × 縦 {height:0.0} の範囲が映ります（{(view.IsOrthographic ? "正投影" : "透視投影")}・回転 {view.rollDegrees:0}°）";
        }
    }
}
