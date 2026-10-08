using UnityEditor;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>UiScreen のインスペクター: 要点の説明と、UIスタジオで開く・動きを試すボタン。</summary>
    [CustomEditor(typeof(UiScreen))]
    [CanEditMultipleObjects]
    public sealed class UiScreenEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var screen = (UiScreen)target;
            EditorGUILayout.HelpBox(
                $"画面「{screen.Label}」（名前: {screen.screenId}）\n" +
                "開け閉めは UIの置き場所（[UI]）が名前で行います。レイヤー・動き・値のつなぎは UIスタジオで整理できます。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("UIスタジオで開く", GUILayout.Height(24)))
                {
                    UiStudioWindow.OpenPage<UiScreensPage>();
                }

                using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(screen)))
                {
                    if (GUILayout.Button("出る動きを試す", GUILayout.Height(24)))
                    {
                        UiMotionPreview.PlayScreen(screen, true);
                    }

                    if (GUILayout.Button("消える動きを試す", GUILayout.Height(24)))
                    {
                        UiMotionPreview.PlayScreen(screen, false);
                    }
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
