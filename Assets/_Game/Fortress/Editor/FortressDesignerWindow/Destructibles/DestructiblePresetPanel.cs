using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>プリセット（破壊可能物の種類）の管理。選択中への適用・選択中の設定を新規保存・サンプル作成。</summary>
    public sealed class DestructiblePresetPanel
    {
        private bool _foldout;

        public void Draw()
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, "プリセット（種類の保存・適用）");
            if (_foldout)
            {
                EditorGUILayout.HelpBox(
                    "プリセットは「耐久・見た目・演出・ドロップ・当たり判定」の設定一式です。" +
                    "位置・大きさ・連動・無敵など、その場所に固有の設定は含まれません。" +
                    "プリセットのアセットを直接編集して、「選択中に再適用」で反映もできます。",
                    MessageType.None);

                var presets = DestructiblePresetTools.FindAll();
                var selected = DestructibleSelection.Get();

                foreach (var preset in presets)
                {
                    DrawPresetRow(preset, selected);
                }

                if (presets.Count == 0)
                {
                    EditorGUILayout.HelpBox("プリセットがありません。サンプルを作るか、選択中の設定を保存してください。", MessageType.Info);
                }

                DrawCreateButtons(selected);
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawPresetRow(DestructiblePreset preset, System.Collections.Generic.List<DestructibleObstacle> selected)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(preset.displayName, GUILayout.MinWidth(90));
                EditorGUILayout.LabelField($"HP {preset.settings.durability.maxHealth:0.#}", GUILayout.Width(64));

                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("選択中に適用", "選択中の破壊可能物へ、この種類の設定をコピーします（位置・大きさは変わりません）。"), GUILayout.Width(90)))
                    {
                        DestructiblePresetTools.Apply(preset, selected);
                    }
                }

                if (GUILayout.Button(new GUIContent("使う", "新しく配置するときの種類にします。"), GUILayout.Width(44)))
                {
                    DestructibleEditorSettings.ActivePreset = preset;
                }

                if (GUILayout.Button(new GUIContent("編集", "プリセットのアセットを選択します。"), GUILayout.Width(44)))
                {
                    Selection.activeObject = preset;
                    EditorGUIUtility.PingObject(preset);
                }
            }
        }

        private static void DrawCreateButtons(System.Collections.Generic.List<DestructibleObstacle> selected)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected.Count != 1))
                {
                    if (GUILayout.Button(new GUIContent("選択中を新しいプリセットとして保存", "1つだけ選んでいるときに使えます。")))
                    {
                        var preset = DestructiblePresetTools.SaveAsPreset(selected[0]);
                        if (preset != null)
                        {
                            DestructibleEditorSettings.ActivePreset = preset;
                        }
                    }
                }

                if (GUILayout.Button(new GUIContent("サンプルを作る（木箱・岩・ガラス）", "すぐ試せる種類を3つ作ります。既にあれば作りません。")))
                {
                    DestructiblePresetTools.CreateSamples();
                }
            }
        }
    }
}
