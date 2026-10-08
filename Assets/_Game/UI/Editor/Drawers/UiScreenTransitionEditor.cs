using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>画面切り替えの幕（UiScreenTransition）を日本語の項目名で並べる。スタジオとインスペクターで共通。小さな再生見本付き。</summary>
    [CustomEditor(typeof(UiScreenTransition))]
    public sealed class UiScreenTransitionEditor : Editor
    {
        private static readonly (string path, string label)[] Fields =
        {
            ("displayName", "名前"),
            ("pattern", "模様"),
            ("color", "幕の色"),
            ("coverSeconds", "閉じる秒数"),
            ("holdSeconds", "覆ったまま待つ秒数"),
            ("revealSeconds", "開く秒数"),
            ("ease", "緩急"),
            ("angle", "向き（度）"),
            ("revealPassThrough", "開くとき同じ向きへ抜ける"),
            ("count", "帯の数・細かさ"),
            ("softness", "境目のぼかし"),
            ("center", "中心（画面の割合）"),
            ("ruleTexture", "ルール画像"),
            ("invert", "順番を逆にする"),
            ("edgeColor", "灼けるふちの色"),
            ("edgeWidth", "灼けるふちの太さ"),
            ("seCover", "閉じ始めの効果音"),
            ("seReveal", "開き始めの効果音")
        };

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioShell.StylePath);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            root.Add(new TransitionPreviewElement((UiScreenTransition)target, 280f, 158f));
            foreach (var (path, label) in Fields)
            {
                var property = serializedObject.FindProperty(path);
                if (property != null)
                {
                    root.Add(new PropertyField(property, label) { tooltip = property.tooltip });
                }
            }

            root.Bind(serializedObject);
            return root;
        }
    }
}
