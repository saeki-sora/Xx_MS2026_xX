using System;
using System.Reflection;
using MS2026.Skills.EditorTools;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 設定のまとまり1つぶんのカード。題名を押すと開く／閉じる。中身はスキルのインスペクターと同じ描き方（日本語の項目名・範囲・色）。
    /// 値を変えると設定ファイルにすぐ入る（Play 中でもその場で見た目が変わる。元に戻す Ctrl+Z もできる）。
    /// </summary>
    public sealed class SkillGroupCard : VisualElement
    {
        private readonly SerializedObject _serializedObject;
        private readonly string _propertyName;
        private readonly Type _groupType;
        private readonly VisualElement _body;
        private readonly Label _arrow;

        public SkillGroupCard(SerializedObject serializedObject, FieldInfo group, bool open)
        {
            _serializedObject = serializedObject;
            _propertyName = group.Name;
            _groupType = group.FieldType;

            var card = StudioUi.Card();
            Add(card);
            card.style.marginBottom = 8;

            var header = StudioUi.Row();
            header.tooltip = SkillTabs.TooltipOf(group);
            _arrow = new Label(open ? "▼" : "▶");
            _arrow.style.width = 16;
            _arrow.style.unityTextAlign = TextAnchor.MiddleLeft;
            var title = StudioUi.Styled(new Label(SkillTabs.LabelOf(group)), "sk-card-title");
            title.style.marginBottom = 0;
            header.Add(_arrow);
            header.Add(title);
            header.RegisterCallback<ClickEvent>(_ => SetOpen(_body.style.display == DisplayStyle.None));
            card.Add(header);

            _body = new IMGUIContainer(DrawBody);
            _body.style.marginTop = 6;
            card.Add(_body);
            SetOpen(open);
        }

        private void SetOpen(bool open)
        {
            _body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _arrow.text = open ? "▼" : "▶";
        }

        private void DrawBody()
        {
            if (_serializedObject.targetObject == null)
            {
                return;
            }

            _serializedObject.Update();
            var property = _serializedObject.FindProperty(_propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox("この欄が見つかりません。", MessageType.Warning);
                return;
            }

            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(contentRect.width * 0.45f, 160f, 300f);
            SkillInspectorGUI.DrawGroupContents(property, _groupType);
            EditorGUIUtility.labelWidth = labelWidth;
            _serializedObject.ApplyModifiedProperties();
        }
    }
}
