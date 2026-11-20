using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージ関係のインスペクターを日本語の項目名で並べるための共通部品。
    /// 開いた瞬間に見える項目は少なくし、残りは折りたたみ（1段奥）に入れる。
    /// </summary>
    internal static class StageInspectorUi
    {
        /// <summary>スタジオと同じ見た目のスタイルを読み込んだ、空の入れ物。</summary>
        public static VisualElement Root()
        {
            var root = new VisualElement();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioShell.StylePath);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            return root;
        }

        /// <summary>項目を日本語の名前で並べる（説明はスクリプトの Tooltip をそのまま使う）。</summary>
        public static void AddFields(VisualElement parent, SerializedObject serializedObject, params (string path, string label)[] fields)
        {
            foreach (var (path, label) in fields)
            {
                var property = serializedObject.FindProperty(path);
                if (property != null)
                {
                    parent.Add(new PropertyField(property, label) { tooltip = property.tooltip });
                }
            }
        }

        /// <summary>折りたたみ。開いたか閉じたかは覚えておく（key ごと）。</summary>
        public static Foldout Folded(string title, string key, bool open = false)
        {
            var foldout = new Foldout { text = title, value = open, viewDataKey = "MS2026.Stage.Inspector." + key };
            foldout.style.marginTop = 6;
            return foldout;
        }
    }
}
