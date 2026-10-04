using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「カメラ」タブ。4人それぞれの視点(見る場所・回転・ズーム・投影)の調整、4人分の画面プレビュー、映り込みチェック、
    /// 絵を立たせる(ビルボード)設定、演出(揺れ・寄り)の調整、Play中の視点切り替えをまとめる。各パネルは独立していて、このクラスは並べるだけ(他のタブと同じ作り)。
    /// </summary>
    public sealed class CameraTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly CameraSetupPanel _setup = new CameraSetupPanel();
        private readonly CameraPreviewPanel _preview = new CameraPreviewPanel();
        private readonly CameraVisibilityPanel _visibility = new CameraVisibilityPanel();
        private readonly CameraViewEditPanel _edit = new CameraViewEditPanel();
        private readonly CameraBillboardPanel _billboard = new CameraBillboardPanel();
        private readonly CameraViewGeneratorPanel _generator = new CameraViewGeneratorPanel();
        private readonly CameraPresetPanel _presets = new CameraPresetPanel();
        private readonly CameraFeedbackPanel _feedback = new CameraFeedbackPanel();
        private readonly CameraLivePanel _live = new CameraLivePanel();

        public CameraTabView()
        {
            AddToClassList("fd-tab-content");
            _imgui = new IMGUIContainer(OnIMGUI);
            Add(_imgui);

            RegisterCallback<AttachToPanelEvent>(_ => SetVisible(true));
            RegisterCallback<DetachFromPanelEvent>(_ => SetVisible(false));
        }

        /// <summary>ウィンドウの定期更新(150ms毎)から呼ばれる。描画以外の重い処理(プレビューの描画・Gameビューへの反映)はここで行う。</summary>
        public void Refresh()
        {
            var context = CameraTabContext.Capture();
            CameraGameViewSync.Tick(context);
            _preview.RenderIfNeeded(context);
            _imgui.MarkDirtyRepaint();
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

        private void OnIMGUI()
        {
            EditorGUILayout.LabelField("カメラ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "4人それぞれの画面に映る範囲(見る場所・画面の回転・ズーム・正投影/透視投影)を調整します。上のボタンかプレビューをクリックして" +
                "編集する視点を選び、スライダーかSceneの枠(中央=移動 / 円=回転 / 上辺=ズーム)で直感的に動かせます。" +
                "設定は「視点プリセット」アセットに保存され、Play中にいじっても即反映されます。",
                MessageType.None);

            var context = CameraTabContext.Capture();
            if (!_setup.Draw(context))
            {
                return;
            }

            DrawViewerSelector(context);

            var issues = CameraVisibilityChecker.Check(context);
            if (Application.isPlaying)
            {
                _live.Draw(context);
            }

            _preview.Draw(context, issues);
            _edit.Draw(context);
            _billboard.Draw(context);
            _visibility.Draw(context, issues);
            _generator.Draw(context);
            _presets.Draw(context);
            _feedback.Draw(context);

            if (!Application.isPlaying)
            {
                _live.Draw(context);
            }

            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        private static void DrawViewerSelector(CameraTabContext context)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("編集する視点", EditorStyles.miniBoldLabel);
            CameraToolState.SelectedViewer = CameraGui.ViewerButtons(context.SelectedViewer, 28f);

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
