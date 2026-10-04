using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// P1〜P4と全体視点の画面を並べて表示する。本番の縦横比で描き、安全域などのガイドと映り込みの警告数を重ねる。
    /// クリックでその視点を編集対象にする。Play中はゲームの様子をそのまま4人分同時に確認できる。
    /// </summary>
    public sealed class CameraPreviewPanel : IDisposable
    {
        private const float LabelHeight = 18f;
        private const float Spacing = 6f;

        private static readonly int[][] Rows = { new[] { 0, 1 }, new[] { 2, 3 }, new[] { ViewerIndex.Overview } };
        private static readonly Color SafeAreaColor = new Color(1f, 0.85f, 0.2f, 0.7f);
        private static readonly Color GuideLineColor = new Color(1f, 1f, 1f, 0.3f);

        private readonly CameraPreviewRenderer _renderer = new CameraPreviewRenderer();
        private float _cellWidth = 240f;
        private bool _renderRequested = true;
        private SerializedObject _rigSerialized;

        public bool IsOpen => CameraToolState.GetFoldout("preview");

        /// <summary>定期更新から呼ぶ(OnGUIの外で描画するため)。</summary>
        public void RenderIfNeeded(CameraTabContext context)
        {
            if (!IsOpen || context.Preset == null || (!CameraToolState.AutoRefreshPreview && !_renderRequested))
            {
                return;
            }

            _renderer.RenderAll(context, Mathf.RoundToInt(_cellWidth));
            _renderRequested = false;
        }

        public void Dispose() => _renderer.Dispose();

        public void Draw(CameraTabContext context, List<CameraVisibilityIssue> issues)
        {
            if (!CameraGui.Section("preview", "4人の画面プレビュー"))
            {
                return;
            }

            DrawToolbar(context);
            DrawGuideSettings(context);

            var shownViewer = CameraGameViewSync.ShownViewer(context);
            var availableWidth = EditorGUIUtility.currentViewWidth - 36f;
            _cellWidth = Mathf.Max(80f, (availableWidth - Spacing) * 0.5f);
            var cellHeight = _cellWidth / context.Aspect;

            foreach (var row in Rows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var viewer in row)
                    {
                        var rect = GUILayoutUtility.GetRect(_cellWidth, cellHeight + LabelHeight, GUILayout.Width(_cellWidth));
                        DrawCell(rect, context, viewer, issues, shownViewer == viewer);
                        GUILayout.Space(Spacing);
                    }
                }

                GUILayout.Space(Spacing);
            }
        }

        private void DrawToolbar(CameraTabContext context)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                CameraToolState.AutoRefreshPreview = EditorGUILayout.ToggleLeft(
                    new GUIContent("自動更新", "OFFにすると「今すぐ更新」を押したときだけ描き直します(重いシーン向け)。"), CameraToolState.AutoRefreshPreview, GUILayout.Width(80));
                CameraToolState.ShowPreviewGuides = EditorGUILayout.ToggleLeft("ガイド", CameraToolState.ShowPreviewGuides, GUILayout.Width(60));

                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    CameraToolState.SyncGameView = EditorGUILayout.ToggleLeft(
                        new GUIContent("編集中の視点をGameビューにも表示", "Play Mode外で、選択中の視点を本物のカメラにも反映します。"), CameraToolState.SyncGameView);
                }

                if (!CameraToolState.AutoRefreshPreview && GUILayout.Button("今すぐ更新", GUILayout.Width(80)))
                {
                    _renderRequested = true;
                }
            }
        }

        private void DrawGuideSettings(CameraTabContext context)
        {
            if (!CameraGui.Section("guides", "構図ガイドの設定（本番の解像度・安全域）", false))
            {
                return;
            }

            if (_rigSerialized == null || _rigSerialized.targetObject != context.Rig)
            {
                _rigSerialized = new SerializedObject(context.Rig);
            }

            _rigSerialized.Update();
            var guides = _rigSerialized.FindProperty(nameof(FortressCameraRig.guides));
            EditorGUILayout.PropertyField(guides.FindPropertyRelative(nameof(CameraGuideSettings.referenceResolution)), new GUIContent("本番の解像度"));
            EditorGUILayout.PropertyField(guides.FindPropertyRelative(nameof(CameraGuideSettings.safeAreaRatio)), new GUIContent("安全域の大きさ"));
            EditorGUILayout.PropertyField(guides.FindPropertyRelative(nameof(CameraGuideSettings.showCenterCross)), new GUIContent("中心の十字"));
            EditorGUILayout.PropertyField(guides.FindPropertyRelative(nameof(CameraGuideSettings.showThirds)), new GUIContent("三分割線"));
            _rigSerialized.ApplyModifiedProperties();

            EditorGUILayout.HelpBox("同じガイドを、Play中(エディタ/Development Build)に F7 キーでゲーム画面にも重ねられます。", MessageType.None);
        }

        private void DrawCell(Rect rect, CameraTabContext context, int viewer, List<CameraVisibilityIssue> issues, bool shownInGameView)
        {
            var selected = viewer == context.SelectedViewer;
            var color = ViewerIndex.Color(viewer);
            var labelRect = new Rect(rect.x, rect.y, rect.width, LabelHeight);
            var imageRect = new Rect(rect.x, rect.y + LabelHeight, rect.width, rect.height - LabelHeight);

            EditorGUI.DrawRect(labelRect, new Color(color.r, color.g, color.b, selected ? 0.9f : 0.45f));
            EditorGUI.LabelField(labelRect, " " + BuildLabel(viewer, issues, selected, shownInGameView), EditorStyles.whiteBoldLabel);

            var texture = _renderer.Get(viewer);
            if (texture != null)
            {
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill, false);
            }
            else
            {
                EditorGUI.DrawRect(imageRect, new Color(0.1f, 0.1f, 0.1f));
            }

            if (CameraToolState.ShowPreviewGuides)
            {
                DrawGuides(imageRect, context.Guides);
            }

            if (selected)
            {
                DrawBorder(rect, color, 3f);
            }

            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                CameraToolState.SelectedViewer = viewer;
                SceneView.RepaintAll();
                current.Use();
            }
        }

        private static string BuildLabel(int viewer, List<CameraVisibilityIssue> issues, bool selected, bool shownInGameView)
        {
            var label = ViewerIndex.LongLabel(viewer);
            var problems = CameraVisibilityChecker.CountFor(issues, viewer, MessageType.Warning);
            if (problems > 0)
            {
                label += $"  ⚠{problems}";
            }

            if (selected)
            {
                label += "  [編集中]";
            }

            if (shownInGameView)
            {
                label += "  [Gameビュー]";
            }

            return label;
        }

        private static void DrawGuides(Rect imageRect, CameraGuideSettings guides)
        {
            // GUIは左上原点・ビューポートは左下原点だが、安全域・中心・三分割は上下対称なのでそのまま使える。
            DrawBorder(guides.GetSafeRect(imageRect), SafeAreaColor, 1f);

            if (guides.showCenterCross)
            {
                var center = imageRect.center;
                EditorGUI.DrawRect(new Rect(center.x - 8f, center.y, 16f, 1f), GuideLineColor);
                EditorGUI.DrawRect(new Rect(center.x, center.y - 8f, 1f, 16f), GuideLineColor);
            }

            if (guides.showThirds)
            {
                for (var i = 1; i <= 2; i++)
                {
                    EditorGUI.DrawRect(new Rect(imageRect.x + imageRect.width * i / 3f, imageRect.y, 1f, imageRect.height), GuideLineColor);
                    EditorGUI.DrawRect(new Rect(imageRect.x, imageRect.y + imageRect.height * i / 3f, imageRect.width, 1f), GuideLineColor);
                }
            }
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }
    }
}
