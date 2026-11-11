using System.Collections.Generic;
using MS2026.Skills.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 「よく触る項目」の一覧。ページを開いた瞬間に見えるのはこれだけ（使いやすさの資料の T2: 最初は少なく）。
    /// 枠で囲まず、1列に並べる（T9）。項目は設定ファイル側の [SkillQuick] で決まる。
    /// </summary>
    public sealed class SkillQuickList : VisualElement
    {
        private readonly SerializedObject _serializedObject;
        private readonly List<SkillTabs.QuickItem> _items;

        public SkillQuickList(SerializedObject serializedObject, List<SkillTabs.QuickItem> items)
        {
            _serializedObject = serializedObject;
            _items = items;
            style.marginBottom = 14;
            var imgui = new IMGUIContainer(Draw);
            Add(imgui);
        }

        private void Draw()
        {
            if (_serializedObject.targetObject == null)
            {
                return;
            }

            _serializedObject.Update();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(contentRect.width * 0.42f, 180f, 300f);
            SkillDefaults.DrawWithMarks(() =>
            {
                foreach (var item in _items)
                {
                    var property = _serializedObject.FindProperty(item.Path);
                    if (property == null)
                    {
                        continue;
                    }

                    SkillInspectorGUI.DrawField(property, item.Field, item.Label);
                    EditorGUILayout.Space(2f);
                }
            });

            EditorGUIUtility.labelWidth = labelWidth;
            _serializedObject.ApplyModifiedProperties();
        }
    }
}
