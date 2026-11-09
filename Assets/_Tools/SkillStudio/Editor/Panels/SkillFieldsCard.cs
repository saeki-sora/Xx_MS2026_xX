using System.Collections.Generic;
using System.Reflection;
using MS2026.Skills.EditorTools;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 設定ファイルの一番上の階層にある項目を、名前で選んで1枚のカードに並べる（共通部分の「溜まり方」など）。
    /// 描き方はスキルのインスペクターと同じ。
    /// </summary>
    public sealed class SkillFieldsCard : VisualElement
    {
        private readonly SerializedObject _serializedObject;
        private readonly IReadOnlyList<string> _names;

        public SkillFieldsCard(SerializedObject serializedObject, string title, string body, IReadOnlyList<string> names, string tooltip = null)
        {
            _serializedObject = serializedObject;
            _names = names;
            var card = StudioUi.Card(title, body);
            card.tooltip = tooltip;
            card.style.marginBottom = 8;
            var imgui = new IMGUIContainer(Draw);
            imgui.style.marginTop = 6;
            card.Add(imgui);
            Add(card);
        }

        private void Draw()
        {
            if (_serializedObject.targetObject == null)
            {
                return;
            }

            _serializedObject.Update();
            var type = _serializedObject.targetObject.GetType();
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 260f;
            foreach (var name in _names)
            {
                var property = _serializedObject.FindProperty(name);
                var field = SkillInspectorGUI.Field(type, name);
                if (property != null)
                {
                    SkillInspectorGUI.DrawField(property, field);
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
            _serializedObject.ApplyModifiedProperties();
        }
    }
}
