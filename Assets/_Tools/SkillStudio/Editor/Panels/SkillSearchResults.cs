using System.Collections.Generic;
using MS2026.Skills.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 検索で見つかった項目を、その場で触れる形で並べる（場所ごとに小さな見出し）。
    /// 枠で囲まず1列（T9）。変えた所の印と「↺」もふだんと同じに出す。
    /// </summary>
    public sealed class SkillSearchResults : VisualElement
    {
        private const int MaxHits = 80;
        private readonly SerializedObject _serializedObject;
        private readonly List<SkillSearch.Hit> _hits;

        public SkillSearchResults(SerializedObject serializedObject, List<SkillSearch.Hit> hits)
        {
            _serializedObject = serializedObject;
            _hits = hits;
            style.marginBottom = 14;
            Add(new IMGUIContainer(Draw));
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
                string place = null;
                var shown = 0;
                foreach (var hit in _hits)
                {
                    if (shown >= MaxHits)
                    {
                        break;
                    }

                    var property = _serializedObject.FindProperty(hit.Path);
                    if (property == null)
                    {
                        continue;
                    }

                    if (hit.Place != place)
                    {
                        place = hit.Place;
                        EditorGUILayout.Space(shown == 0 ? 0f : 8f);
                        var color = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.6f);
                        EditorGUILayout.LabelField(place, EditorStyles.miniBoldLabel);
                        GUI.color = color;
                    }

                    SkillInspectorGUI.DrawField(property, hit.Field, null);
                    EditorGUILayout.Space(2f);
                    shown++;
                }

                if (_hits.Count > MaxHits)
                {
                    EditorGUILayout.HelpBox($"ほかに {_hits.Count - MaxHits} 個あります。言葉を足して絞り込んでください。", MessageType.Info);
                }
            });
            EditorGUIUtility.labelWidth = labelWidth;
            _serializedObject.ApplyModifiedProperties();
        }
    }
}
