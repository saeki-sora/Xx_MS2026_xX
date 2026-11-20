using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>「画面いっぱいの背景」（StageScreenBackground）を日本語の項目名で並べる。</summary>
    [CustomEditor(typeof(StageScreenBackground))]
    public sealed class StageScreenBackgroundEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = StageInspectorUi.Root();

            root.Add(StudioUi.Section("背景"));
            StageInspectorUi.AddFields(root, serializedObject,
                ("texture", "背景の画像"),
                ("tint", "色の掛け算（白でそのまま）"),
                ("fit", "画面と縦横比が違うとき"),
                ("showInSceneView", "シーンビューにも出す"));

            root.Add(StudioUi.Note("ゲームのカメラの画面いっぱいに貼ります。床の板が画面を覆っていると隠れるので、床の見た目は消しておきます。"));

            root.Bind(serializedObject);
            return root;
        }
    }
}
