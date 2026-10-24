using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// 砲台のキャラの絵の一覧（<see cref="TurretPortraitTable"/>）の Inspector。キャラ1人ずつ折りたたみ、項目名は日本語（[SkillLabel]）。
    /// メニュー「Tools/赤スキル/砲台のキャラの絵の一覧を開く」。
    /// </summary>
    [CustomEditor(typeof(TurretPortraitTable))]
    internal sealed class TurretPortraitTableEditor : Editor
    {
        private const string AssetPath = "Assets/RedSkill/Data/TurretPortraits.asset";

        [MenuItem("Tools/赤スキル/砲台のキャラの絵の一覧を開く")]
        private static void SelectTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<TurretPortraitTable>(AssetPath);
            if (table == null)
            {
                EditorUtility.DisplayDialog("砲台のキャラの絵", $"{AssetPath} が見つかりません。", "OK");
                return;
            }

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "砲台に出すキャラの絵（SD）の一覧です。1行 = キャラ1人。合う砲台では、元の砲台の見た目の代わりにその絵が出て、狙う向きへ一緒に回ります。\n" +
                "どの砲台に出すか: キャラ選択を通ったときは「キャラのid」を選んだ人、通らなかったときは「キャラ選択なしのときに出す人」の席。\n" +
                "使うには、スキルを配る係（[RedSkill] Skill Installer）の「砲台のキャラの絵の一覧」にこのファイルを入れます（プレハブには入れてあります）。\n" +
                "再生中に変えた値もすぐ反映され、止めても残ります。",
                MessageType.None);

            serializedObject.Update();
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
                    DrawEntry(entry);
                }
            }

            if (GUILayout.Button("＋ キャラを増やす（最後の行を複製）"))
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                entries.GetArrayElementAtIndex(entries.arraySize - 1).isExpanded = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawEntry(SerializedProperty entry)
        {
            var end = entry.GetEndProperty();
            var child = entry.Copy();
            if (!child.NextVisible(true))
            {
                return;
            }

            while (!SerializedProperty.EqualContents(child, end))
            {
                var field = typeof(SkillTurretLook).GetField(child.name, BindingFlags.Instance | BindingFlags.Public);
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

                if (!child.NextVisible(false))
                {
                    break;
                }
            }
        }
    }
}
