using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// シーンビューの「ステージ配置」ツール（背景オブジェクトを選んでいるときに使える）。
    /// つかんで床の上を動かす（StageBodyDrag）＋回す・大きさ・高さの取っ手（StagePropHandles）。
    /// ステージ背景スタジオを開いている間は、背景を選ぶと自動でこのツールになる（ステージ配置パネルで切り替え可）。
    /// </summary>
    [EditorTool("ステージ配置", typeof(StageProp))]
    public sealed class StagePlaceTool : EditorTool
    {
        private readonly StageBodyDrag _drag = new StageBodyDrag();
        private readonly StagePropHandles _handles = new StagePropHandles();
        private readonly List<StageProp> _selection = new List<StageProp>();
        private GUIContent _icon;

        public override GUIContent toolbarIcon => _icon ??= new GUIContent(
            EditorGUIUtility.IconContent("MoveTool").image,
            "ステージ配置: 背景をつかんで床の上を動かす／輪で回す／四角で大きさ／「↕」で高さ。Ctrl を押している間は吸着が逆になります。");

        /// <summary>今このツールを使っているか。</summary>
        public static bool IsActive => ToolManager.activeToolType == typeof(StagePlaceTool);

        public static void Activate() => ToolManager.SetActiveTool<StagePlaceTool>();

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView view))
            {
                return;
            }

            _selection.Clear();
            foreach (var target in targets)
            {
                if (target is StageProp prop && prop != null)
                {
                    _selection.Add(prop);
                }
            }

            if (_selection.Count == 0)
            {
                return;
            }

            // 本体をクリックしたら（大きさの四角・高さの札の上でなければ）動かすのが優先。輪は何も無い所でつかんだときだけ回す。
            // 呼ぶ順番は毎イベント同じにする（コントロールIDがずれないように）。
            _handles.Prepare(_selection);
            _drag.OnSceneGui(view, _selection, _handles.IsOverHandle);
            _handles.OnSceneGui(view, _selection);
        }
    }

    /// <summary>ステージ背景スタジオを開いている間、背景を選んだら自動でステージ配置ツールに切り替える。</summary>
    [InitializeOnLoad]
    internal static class StagePlaceToolAutoSwitch
    {
        static StagePlaceToolAutoSwitch()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            if (!StagePlaceSettings.AutoTool || StageSceneOverlay.Context == null || StagePlaceTool.IsActive)
            {
                return;
            }

            var go = Selection.activeGameObject;
            if (go != null && go.GetComponent<StageProp>() != null)
            {
                // 選択が変わった直後はツールの対象がまだ古いので、少し後で切り替える。
                EditorApplication.delayCall += () =>
                {
                    if (Selection.activeGameObject == go && !StagePlaceTool.IsActive)
                    {
                        StagePlaceTool.Activate();
                    }
                };
            }
        }
    }
}
