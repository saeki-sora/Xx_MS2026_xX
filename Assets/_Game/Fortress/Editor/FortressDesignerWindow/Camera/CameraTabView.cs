using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「カメラ」タブ。4人それぞれの視点(見る場所・回転・ズーム・投影)の調整、4人分の画面プレビュー、映り込みチェック、
    /// 絵を立たせる(ビルボード)設定、演出(揺れ・寄り)の調整、指定した視点でのPlay開始、Play中の視点切り替えをまとめる。各パネルは独立していて、このクラスは並べるだけ(他のタブと同じ作り)。
    /// 画面は上下2段: 上に「編集する視点のボタン＋画面プレビュー」を固定し、下の設定だけがスクロールする(値をいじりながらプレビューを見られる)。
    /// 境目をドラッグするとプレビューの高さを変えられる。
    /// </summary>
    public sealed class CameraTabView : VisualElement, IFillsDesignerWindow
    {
        private const float MinPreviewHeight = 140f;
        private const float MinSettingsHeight = 120f;
        private const float SplitterOuterHeight = 10f;

        private readonly VisualElement _pinned;
        private readonly IMGUIContainer _pinnedHeader;
        private readonly IMGUIContainer _previewCells;
        private readonly CameraPaneSplitter _splitter;
        private readonly IMGUIContainer _settings;

        private readonly CameraSetupPanel _setup = new CameraSetupPanel();
        private readonly CameraPreviewPanel _preview = new CameraPreviewPanel();
        private readonly CameraVisibilityPanel _visibility = new CameraVisibilityPanel();
        private readonly CameraViewEditPanel _edit = new CameraViewEditPanel();
        private readonly CameraBillboardPanel _billboard = new CameraBillboardPanel();
        private readonly CameraEnemyFacingPanel _enemyFacing = new CameraEnemyFacingPanel();
        private readonly CameraViewGeneratorPanel _generator = new CameraViewGeneratorPanel();
        private readonly CameraPresetPanel _presets = new CameraPresetPanel();
        private readonly CameraFeedbackPanel _feedback = new CameraFeedbackPanel();
        private readonly CameraLivePanel _live = new CameraLivePanel();
        private readonly CameraPlayAsPanel _playAs = new CameraPlayAsPanel();

        // 上の固定部分は定期更新(150ms毎)で取った状況を使う(描画のたびにシーンを探し直さない)。
        private CameraTabContext _context;
        private List<CameraVisibilityIssue> _issues;

        private bool? _appliedReady;
        private bool? _appliedOpen;
        private float _dragStartHeight;
        private float _dragHeight;

        public CameraTabView()
        {
            AddToClassList("fd-tab-content");
            AddToClassList("fd-camera-tab");

            _pinnedHeader = new IMGUIContainer(OnPinnedHeaderGUI);
            _previewCells = new IMGUIContainer(OnPreviewCellsGUI);
            _previewCells.AddToClassList("fd-camera-preview-cells");
            _pinned = new VisualElement();
            _pinned.AddToClassList("fd-camera-pinned");
            _pinned.Add(_pinnedHeader);
            _pinned.Add(_previewCells);

            _splitter = new CameraPaneSplitter("ドラッグでプレビューの高さを変えます。ダブルクリックで元の高さに戻します。");
            _splitter.DragStarted += () => _dragHeight = _dragStartHeight = _pinned.resolvedStyle.height;
            _splitter.Dragging += delta =>
            {
                _dragHeight = ClampPreviewHeight(_dragStartHeight + delta);
                _pinned.style.height = _dragHeight;
            };
            _splitter.DragFinished += () => CameraToolState.PreviewPaneHeight = _dragHeight;
            _splitter.DoubleClicked += () =>
            {
                CameraToolState.PreviewPaneHeight = CameraToolState.DefaultPreviewPaneHeight;
                ApplyLayout(true);
            };

            _settings = new IMGUIContainer(OnSettingsGUI);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("fd-camera-settings");
            scroll.Add(_settings);

            Add(_pinned);
            Add(_splitter);
            Add(scroll);

            RegisterCallback<AttachToPanelEvent>(_ => SetVisible(true));
            RegisterCallback<DetachFromPanelEvent>(_ => SetVisible(false));

            // ウィンドウの大きさが変わったら、プレビューの高さを収まる範囲に直す。
            RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout(true));
        }

        /// <summary>ウィンドウの定期更新(150ms毎)から呼ばれる。描画以外の重い処理(プレビューの描画・Gameビューへの反映)はここで行う。</summary>
        public void Refresh()
        {
            _context = CameraTabContext.Capture();
            CameraGameViewSync.Tick(_context);
            _issues = _context.Preset != null ? CameraVisibilityChecker.Check(_context) : null;
            _preview.RenderIfNeeded(_context);
            ApplyLayout();

            _pinnedHeader.MarkDirtyRepaint();
            _previewCells.MarkDirtyRepaint();
            _settings.MarkDirtyRepaint();
        }

        private void SetVisible(bool visible)
        {
            CameraToolState.TabVisible = visible;
            if (!visible)
            {
                // タブを離れたらプレビュー用のRenderTexture等を解放する(次に開いたとき作り直す)。
                _preview.Dispose();
            }

            SceneView.RepaintAll();
        }

        /// <summary>リグが無いときは上の固定部分を隠し、プレビューをたたんだときは見出しの高さだけにする。</summary>
        private void ApplyLayout(bool force = false)
        {
            var ready = _context != null && _context.Preset != null;
            var open = ready && _preview.IsOpen;
            if (!force && _appliedReady == ready && _appliedOpen == open)
            {
                return;
            }

            _appliedReady = ready;
            _appliedOpen = open;

            _pinned.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            _previewCells.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _splitter.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _pinned.style.height = open
                ? new StyleLength(ClampPreviewHeight(CameraToolState.PreviewPaneHeight))
                : new StyleLength(StyleKeyword.Auto);
        }

        private float ClampPreviewHeight(float height)
        {
            var total = layout.height;
            var max = float.IsNaN(total) || total <= 0f ? float.MaxValue : total - SplitterOuterHeight - MinSettingsHeight;
            return Mathf.Max(MinPreviewHeight, Mathf.Min(height, max));
        }

        private void OnPinnedHeaderGUI()
        {
            var context = _context;
            if (context == null || context.Preset == null)
            {
                return;
            }

            var before = CameraToolState.SelectedViewer;
            EditorGUILayout.Space(2);
            CameraToolState.SelectedViewer = CameraGui.ViewerButtons(context.SelectedViewer, 26f);

            var wasOpen = _preview.IsOpen;
            _preview.DrawHeader();
            if (wasOpen != _preview.IsOpen)
            {
                schedule.Execute(() => ApplyLayout());
            }

            if (GUI.changed || before != CameraToolState.SelectedViewer)
            {
                _previewCells.MarkDirtyRepaint();
                _settings.MarkDirtyRepaint();
                SceneView.RepaintAll();
            }
        }

        private void OnPreviewCellsGUI()
        {
            var context = _context;
            if (context == null || context.Preset == null)
            {
                return;
            }

            if (_preview.DrawCells(_previewCells.contentRect, context, _issues))
            {
                _pinnedHeader.MarkDirtyRepaint();
                _settings.MarkDirtyRepaint();
            }
        }

        private void OnSettingsGUI()
        {
            EditorGUILayout.LabelField("カメラ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "4人それぞれの画面に映る範囲(見る場所・画面の回転・ズーム・正投影/透視投影)を調整します。上のボタンかプレビューをクリックして" +
                "編集する視点を選び、スライダーかSceneの枠(中央=移動 / 円=回転 / 上辺=ズーム)で直感的に動かせます。" +
                "上のプレビューはスクロールしても見えたままです(境目のドラッグで高さを変えられます)。" +
                "設定は「視点プリセット」アセットに保存され、Play中にいじっても即反映されます。",
                MessageType.None);

            var context = CameraTabContext.Capture();
            if (!_setup.Draw(context))
            {
                return;
            }

            DrawSceneToggles();

            var issues = CameraVisibilityChecker.Check(context);
            if (Application.isPlaying)
            {
                _live.Draw(context);
            }
            else
            {
                _playAs.Draw(context);
            }

            _edit.Draw(context);
            _enemyFacing.Draw(context);
            _billboard.Draw(context);
            _visibility.Draw(context, issues);
            _preview.DrawGuideSettings(context);
            _generator.Draw(context);
            _presets.Draw(context);
            _feedback.Draw(context);

            if (!Application.isPlaying)
            {
                _live.Draw(context);
            }

            if (GUI.changed)
            {
                _pinnedHeader.MarkDirtyRepaint();
                _previewCells.MarkDirtyRepaint();
                SceneView.RepaintAll();
            }
        }

        private static void DrawSceneToggles()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                CameraToolState.ShowSceneFrames = EditorGUILayout.ToggleLeft("Sceneに枠を表示", CameraToolState.ShowSceneFrames, GUILayout.Width(120));

                using (new EditorGUI.DisabledScope(!CameraToolState.ShowSceneFrames))
                {
                    CameraToolState.ShowOnlySelectedFrame = EditorGUILayout.ToggleLeft("選択中だけ", CameraToolState.ShowOnlySelectedFrame, GUILayout.Width(90));
                    CameraToolState.ShowSceneHandles = EditorGUILayout.ToggleLeft("ドラッグ操作", CameraToolState.ShowSceneHandles, GUILayout.Width(100));
                    CameraToolState.ShowSafeAreaInScene = EditorGUILayout.ToggleLeft("安全域", CameraToolState.ShowSafeAreaInScene, GUILayout.Width(70));
                }
            }
        }
    }
}
