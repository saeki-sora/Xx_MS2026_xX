using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// P1〜P4と全体視点の画面を並べて表示する。本番の縦横比で描き、安全域などのガイドと映り込みの警告数を重ねる。
    /// カメラタブの上に固定して表示され(下の設定をスクロールしても見えたまま)、渡された領域にちょうど収まる大きさで描く。
    /// 「全員」と「編集中だけ(大きく1枚)」を切り替えられる。クリックでその視点を編集対象にする。Play中はゲームの様子をそのまま確認できる。
    /// </summary>
    public sealed class CameraPreviewPanel : IDisposable
    {
        private const float LabelHeight = 18f;
        private const float Spacing = 6f;

        private static readonly Color SafeAreaColor = new Color(1f, 0.85f, 0.2f, 0.7f);
        private static readonly Color GuideLineColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly int[] ColumnCandidates = { 2, 3, 5 };

        private static readonly GUIContent[] ModeLabels =
        {
            new GUIContent("編集中だけ", "編集中の視点を1枚だけ大きく映します(描くのも1枚なので軽い)。"),
            new GUIContent("全員", "P1〜P4と全体視点の5画面を並べます。画面をクリックすると、その視点を編集します。")
        };

        private readonly CameraPreviewRenderer _renderer = new CameraPreviewRenderer();
        private readonly int[] _single = new int[1];
        private float _cellWidth = 240f;
        private bool _renderRequested = true;
        private int _lastRenderedViewer = int.MinValue;
        private bool _lastRenderedShowAll;
        private int _lastRenderedWidth;
        private SerializedObject _rigSerialized;

        public bool IsOpen => CameraToolState.GetFoldout("preview");

        /// <summary>定期更新から呼ぶ(OnGUIの外で描画するため)。「編集中だけ」のときは編集中の1枚だけ描く。</summary>
        public void RenderIfNeeded(CameraTabContext context)
        {
            if (!IsOpen || context.Preset == null)
            {
                return;
            }

            var showAll = CameraToolState.PreviewShowAll;
            var width = Mathf.RoundToInt(_cellWidth);

            // 自動更新がOFFでも、表示の切り替え・大きさ・編集する視点が変わったときは描き直す(古い絵や空の枠を出さない)。
            var layoutChanged = showAll != _lastRenderedShowAll || width != _lastRenderedWidth
                                || (!showAll && context.SelectedViewer != _lastRenderedViewer);
            if (!CameraToolState.AutoRefreshPreview && !_renderRequested && !layoutChanged)
            {
                return;
            }

            if (showAll)
            {
                _renderer.RenderAll(context, width);
            }
            else
            {
                _single[0] = context.SelectedViewer;
                _renderer.Render(context, width, _single);
            }

            _renderRequested = false;
            _lastRenderedShowAll = showAll;
            _lastRenderedWidth = width;
            _lastRenderedViewer = context.SelectedViewer;
        }

        public void Dispose()
        {
            _renderer.Dispose();
            _lastRenderedViewer = int.MinValue;
        }

        /// <summary>見出し(たたむ)と、表示の切り替え・更新の操作。カメラタブの上の固定部分に描く。たたまれていなければtrue。</summary>
        public bool DrawHeader()
        {
            if (!CameraGui.Section("preview", "画面プレビュー"))
            {
                return false;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var mode = GUILayout.Toolbar(CameraToolState.PreviewShowAll ? 1 : 0, ModeLabels, GUILayout.Width(150));
                CameraToolState.PreviewShowAll = mode == 1;
                GUILayout.Space(8);

                CameraToolState.AutoRefreshPreview = EditorGUILayout.ToggleLeft(
                    new GUIContent("自動更新", "OFFにすると「今すぐ更新」を押したときだけ描き直します(重いシーン向け)。"),
                    CameraToolState.AutoRefreshPreview, GUILayout.Width(76));
                CameraToolState.ShowPreviewGuides = EditorGUILayout.ToggleLeft(
                    new GUIContent("ガイド", "安全域・中心の十字・三分割線を重ねます。設定は下の「構図ガイドの設定」。"),
                    CameraToolState.ShowPreviewGuides, GUILayout.Width(56));

                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    CameraToolState.SyncGameView = EditorGUILayout.ToggleLeft(
                        new GUIContent("Gameビューにも表示", "Play Mode外で、編集中の視点を本物のカメラ(Gameビュー)にも反映します。"),
                        CameraToolState.SyncGameView, GUILayout.Width(130));
                }

                GUILayout.FlexibleSpace();
                if (!CameraToolState.AutoRefreshPreview && GUILayout.Button("今すぐ更新", GUILayout.Width(80)))
                {
                    _renderRequested = true;
                }
            }

            return true;
        }

        /// <summary>
        /// 渡された領域いっぱいに、縦横比を保って画面を並べる。「全員」のときは、領域の形に合わせていちばん大きく映る列の数(2・3・5列)を選ぶ。
        /// 画面がクリックされて編集する視点が変わったらtrue。
        /// </summary>
        public bool DrawCells(Rect area, CameraTabContext context, List<CameraVisibilityIssue> issues)
        {
            var aspect = Mathf.Max(0.01f, context.Aspect);
            var showAll = CameraToolState.PreviewShowAll;
            var count = showAll ? ViewerIndex.All.Length : 1;
            var columns = showAll ? BestColumns(area, aspect, count) : 1;
            var rows = Mathf.CeilToInt(count / (float)columns);
            var cellWidth = CellWidthFor(area, aspect, columns, rows);
            if (cellWidth < 16f)
            {
                return false;
            }

            if (Event.current.type == EventType.Repaint)
            {
                _cellWidth = cellWidth;
            }

            var cellHeight = cellWidth / aspect + LabelHeight;
            var gridWidth = cellWidth * columns + Spacing * (columns - 1);
            var gridHeight = cellHeight * rows + Spacing * (rows - 1);
            var originX = area.x + Mathf.Max(0f, (area.width - gridWidth) * 0.5f);
            var originY = area.y + Mathf.Max(0f, (area.height - gridHeight) * 0.5f);

            var shownViewer = CameraGameViewSync.ShownViewer(context);
            var before = CameraToolState.SelectedViewer;
            for (var i = 0; i < count; i++)
            {
                // 全員のときは P1〜P4 を先に、全体視点を最後に並べる(今までの並びと同じ)。
                var viewer = showAll ? (i < 4 ? i : ViewerIndex.Overview) : context.SelectedViewer;
                var x = originX + i % columns * (cellWidth + Spacing);
                var y = originY + i / columns * (cellHeight + Spacing);
                DrawCell(new Rect(x, y, cellWidth, cellHeight), context, viewer, issues, shownViewer == viewer, showAll);
            }

            return CameraToolState.SelectedViewer != before;
        }

        /// <summary>構図ガイドの設定(本番の解像度・安全域)。毎回は触らないので、下のスクロールする側に置く。</summary>
        public void DrawGuideSettings(CameraTabContext context)
        {
            if (context.Rig == null || !CameraGui.Section("guides", "構図ガイドの設定（本番の解像度・安全域）", false))
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

        private static int BestColumns(Rect area, float aspect, int count)
        {
            var best = ColumnCandidates[0];
            var bestWidth = 0f;
            foreach (var columns in ColumnCandidates)
            {
                var width = CellWidthFor(area, aspect, columns, Mathf.CeilToInt(count / (float)columns));
                if (width > bestWidth + 0.5f)
                {
                    best = columns;
                    bestWidth = width;
                }
            }

            return best;
        }

        private static float CellWidthFor(Rect area, float aspect, int columns, int rows)
        {
            var byWidth = (area.width - Spacing * (columns - 1)) / columns;
            var byHeight = ((area.height - Spacing * (rows - 1)) / rows - LabelHeight) * aspect;
            return Mathf.Floor(Mathf.Min(byWidth, byHeight));
        }

        private void DrawCell(Rect rect, CameraTabContext context, int viewer, List<CameraVisibilityIssue> issues, bool shownInGameView, bool clickToSelect)
        {
            var selected = viewer == context.SelectedViewer;
            var color = ViewerIndex.Color(viewer);
            var labelRect = new Rect(rect.x, rect.y, rect.width, LabelHeight);
            var imageRect = new Rect(rect.x, rect.y + LabelHeight, rect.width, rect.height - LabelHeight);

            EditorGUI.DrawRect(labelRect, new Color(color.r, color.g, color.b, selected ? 0.9f : 0.45f));
            EditorGUI.LabelField(labelRect, " " + BuildLabel(viewer, issues, selected && clickToSelect, shownInGameView), EditorStyles.whiteBoldLabel);

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

            if (selected && clickToSelect)
            {
                DrawBorder(rect, color, 3f);
            }

            var current = Event.current;
            if (clickToSelect && current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                CameraToolState.SelectedViewer = viewer;
                SceneView.RepaintAll();
                current.Use();
            }
        }

        private static string BuildLabel(int viewer, List<CameraVisibilityIssue> issues, bool selected, bool shownInGameView)
        {
            var label = ViewerIndex.LongLabel(viewer);
            var problems = issues != null ? CameraVisibilityChecker.CountFor(issues, viewer, MessageType.Warning) : 0;
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
