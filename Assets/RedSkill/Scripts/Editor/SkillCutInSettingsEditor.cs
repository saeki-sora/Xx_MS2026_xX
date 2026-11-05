using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// カットインの設定（<see cref="SkillCutInSettings"/>）の Inspector。
    /// 上にキャラごとの行（技名・絵・顔の位置…）。絵の縮小版をクリックすると「顔の位置」をそこに合わせられる。
    /// 再生中は行ごとに「大きい窓で試す」「小さい窓で試す」で、スキルを撃たずにカットインだけ出せる。
    /// その下に全員共通の設定（動き・形・稲妻・文字・音）。メニュー「Tools/赤スキル/カットインの設定を開く」。
    /// </summary>
    [CustomEditor(typeof(SkillCutInSettings))]
    internal sealed class SkillCutInSettingsEditor : Editor
    {
        private const string AssetPath = "Assets/RedSkill/Data/SkillCutIn.asset";
        private const float ThumbnailWidth = 240f;

        [MenuItem("Tools/赤スキル/カットインの設定を開く")]
        private static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SkillCutInSettings>(AssetPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("カットイン", $"{AssetPath} が見つかりません。", "OK");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "スキルのカットインの設定です。自分が撃つと画面の左に大きく、ほかの人が撃つとその人の HUD の欄のそばに小さく出ます。\n" +
                "・技名を変える: 下のキャラの「技名」を書き換える（空ならスキルの表示名）。\n" +
                "・絵を差し替える: Assets/RedSkill/Art/CutIn の PNG を同じ名前で上書きする。構図が変わったら、縮小版の顔をクリックして「顔の位置」を合わせ直す。\n" +
                "・表情を途中で変える: 「途中から切り替える絵」に同じ構図の表情違いの絵を入れる。\n" +
                "・溜まったときに出すようにする: 「カットインを出すとき」を切り替える。\n" +
                "使うには、スキルを配る係（[RedSkill] Skill Installer）の「カットインの設定」にこのファイルを入れます（プレハブには入れてあります）。再生中に変えた値もすぐ反映されます。",
                MessageType.None);

            DrawEntries();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("全員共通", EditorStyles.boldLabel);
            SkillInspectorGUI.DrawAll(serializedObject, "entries");
        }

        private void DrawEntries()
        {
            serializedObject.Update();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("キャラごとの絵・技名・声", EditorStyles.boldLabel);
            if (Application.isPlaying && SkillCutInDirector.Instance == null)
            {
                EditorGUILayout.HelpBox("このシーンにはカットインを出す係がありません（スキルを配る係の「カットインの設定」が空か、係がいない）。", MessageType.Info);
            }

            var entries = serializedObject.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var label = entry.FindPropertyRelative("label").stringValue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, string.IsNullOrEmpty(label) ? $"キャラ {i + 1}" : label, true);
                    if (GUILayout.Button("消す", GUILayout.Width(48)))
                    {
                        entries.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                if (!entry.isExpanded)
                {
                    continue;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawEntryFields(entry);
                    DrawFacePicker(entry);
                    DrawTryButtons(i);
                }

                EditorGUILayout.Space(4f);
            }

            if (GUILayout.Button("＋ キャラを増やす（最後の行を複製）"))
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                entries.GetArrayElementAtIndex(entries.arraySize - 1).isExpanded = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawEntryFields(SerializedProperty entry)
        {
            var end = entry.GetEndProperty();
            var child = entry.Copy();
            if (!child.NextVisible(true))
            {
                return;
            }

            while (!SerializedProperty.EqualContents(child, end))
            {
                var field = typeof(SkillCutInEntry).GetField(child.name, BindingFlags.Instance | BindingFlags.Public);
                var skillLabel = field?.GetCustomAttribute<SkillLabelAttribute>();
                var content = new GUIContent(skillLabel != null ? skillLabel.Label : child.displayName, child.tooltip);
                var range = field?.GetCustomAttribute<RangeAttribute>();
                if (range != null && child.propertyType == SerializedPropertyType.Float)
                {
                    EditorGUILayout.Slider(child, range.min, range.max, content);
                }
                else
                {
                    EditorGUILayout.PropertyField(child, content, true);
                }

                if (child.name == "skillName" && string.IsNullOrWhiteSpace(child.stringValue))
                {
                    var skill = entry.FindPropertyRelative("skill").objectReferenceValue as CharacterSkill;
                    if (skill != null)
                    {
                        EditorGUILayout.LabelField(" ", $"（空なので「{skill.DisplayName}」と出ます）", EditorStyles.miniLabel);
                    }
                }

                if (!child.NextVisible(false))
                {
                    break;
                }
            }
        }

        // 絵の縮小版に「顔の位置」の印を描く。クリック（ドラッグ）した所を顔の位置にする。
        private static void DrawFacePicker(SerializedProperty entry)
        {
            var sprite = entry.FindPropertyRelative("picture").objectReferenceValue as Sprite;
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var faceProp = entry.FindPropertyRelative("face");
            var aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            EditorGUILayout.LabelField(" ", "縮小版の顔をクリックすると「顔の位置」になります", EditorStyles.miniLabel);
            var outer = GUILayoutUtility.GetRect(ThumbnailWidth, ThumbnailWidth / aspect, GUILayout.ExpandWidth(false));
            var rect = new Rect(outer.x + EditorGUIUtility.labelWidth, outer.y, ThumbnailWidth, ThumbnailWidth / aspect);
            var tex = sprite.texture;
            var r = sprite.textureRect;
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);

            var face = faceProp.vector2Value;
            var p = new Vector2(rect.x + face.x * rect.width, rect.y + face.y * rect.height);
            var cross = new Color(1f, 0.2f, 0.6f, 1f);
            EditorGUI.DrawRect(new Rect(p.x - 9f, p.y - 1f, 18f, 2f), cross);
            EditorGUI.DrawRect(new Rect(p.x - 1f, p.y - 9f, 2f, 18f), cross);

            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && rect.Contains(e.mousePosition))
            {
                faceProp.vector2Value = new Vector2(
                    Mathf.Round((e.mousePosition.x - rect.x) / rect.width * 1000f) / 1000f,
                    Mathf.Round((e.mousePosition.y - rect.y) / rect.height * 1000f) / 1000f);
                e.Use();
            }
        }

        private void DrawTryButtons(int index)
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || SkillCutInDirector.Instance == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);
                var settings = (SkillCutInSettings)target;
                var entry = settings.entries != null && index < settings.entries.Length ? settings.entries[index] : null;
                if (GUILayout.Button(new GUIContent("大きい窓で試す", "再生中だけ。スキルは撃たずにカットインだけ出す（自分が撃ったときの見え方）。")))
                {
                    SkillCutInDirector.Instance.Show(SkillCutInDirector.LocalPlayer(), entry, true);
                }

                if (GUILayout.Button(new GUIContent("小さい窓で試す", "再生中だけ。ほかの人（自分の次の番号の人）が撃ったときの見え方。")))
                {
                    SkillCutInDirector.Instance.Show((SkillCutInDirector.LocalPlayer() + 1) % 4, entry, false);
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField(" ", "「試す」ボタンは再生中に使えます", EditorStyles.miniLabel);
            }
        }
    }
}
