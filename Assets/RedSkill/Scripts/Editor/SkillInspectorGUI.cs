using System;
using System.Collections.Generic;
using System.Reflection;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// スキル関係のインスペクターの共通の描き方。項目名を <see cref="SkillLabelAttribute"/> の日本語にし、
    /// 設定のまとまり（タイミング・形…）は折りたたみにする。[Range] [Min] [ColorUsage] [GradientUsage] [TextArea] [Header] は自分で扱う。
    /// </summary>
    internal static class SkillInspectorGUI
    {
        // 他のフォルダの型（カメラの揺れの設定）には日本語名を付けられないので、ここで名前を決める。
        private static readonly Dictionary<string, string> ExternalLabels = new Dictionary<string, string>
        {
            { "amplitude", "揺れ幅（m）" },
            { "rollAmplitude", "画面の回転の揺れ幅（度）" },
            { "frequency", "揺れの細かさ（Hz）" },
            { "duration", "揺れる時間（秒）" }
        };

        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>対象の全項目を描く（m_Script は灰色で一番上）。</summary>
        public static void DrawAll(SerializedObject serializedObject)
        {
            serializedObject.Update();
            var targetType = serializedObject.targetObject.GetType();
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }

                    continue;
                }

                DrawProperty(iterator.Copy(), FindField(targetType, iterator.name));
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawProperty(SerializedProperty property, FieldInfo field)
        {
            DrawHeaders(field);
            var label = new GUIContent(LabelFor(property, field), property.tooltip);

            if (field != null && IsGroup(field.FieldType) && property.propertyType == SerializedPropertyType.Generic)
            {
                DrawGroup(property, field.FieldType, label);
                return;
            }

            var range = field?.GetCustomAttribute<RangeAttribute>();
            var colorUsage = field?.GetCustomAttribute<ColorUsageAttribute>();
            var gradientUsage = field?.GetCustomAttribute<GradientUsageAttribute>();
            var textArea = field?.GetCustomAttribute<TextAreaAttribute>();

            if (range != null && property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUILayout.Slider(property, range.min, range.max, label);
            }
            else if (range != null && property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUILayout.IntSlider(property, (int)range.min, (int)range.max, label);
            }
            else if (colorUsage != null && property.propertyType == SerializedPropertyType.Color)
            {
                EditorGUI.BeginChangeCheck();
                var color = EditorGUILayout.ColorField(label, property.colorValue, true, colorUsage.showAlpha, colorUsage.hdr);
                if (EditorGUI.EndChangeCheck())
                {
                    property.colorValue = color;
                }
            }
            else if (gradientUsage != null && property.propertyType == SerializedPropertyType.Gradient)
            {
                EditorGUI.BeginChangeCheck();
                var gradient = EditorGUILayout.GradientField(label, property.gradientValue, gradientUsage.hdr);
                if (EditorGUI.EndChangeCheck())
                {
                    property.gradientValue = gradient;
                }
            }
            else if (textArea != null && property.propertyType == SerializedPropertyType.String)
            {
                EditorGUILayout.LabelField(label);
                EditorGUI.BeginChangeCheck();
                var text = EditorGUILayout.TextArea(property.stringValue, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * textArea.minLines));
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = text;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(property, label, true);
            }

            var min = field?.GetCustomAttribute<MinAttribute>();
            if (min != null)
            {
                if (property.propertyType == SerializedPropertyType.Float && property.floatValue < min.min)
                {
                    property.floatValue = min.min;
                }
                else if (property.propertyType == SerializedPropertyType.Integer && property.intValue < min.min)
                {
                    property.intValue = (int)min.min;
                }
            }
        }

        private static void DrawGroup(SerializedProperty property, Type groupType, GUIContent label)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (child.NextVisible(true))
            {
                while (!SerializedProperty.EqualContents(child, end))
                {
                    DrawProperty(child.Copy(), FindField(groupType, child.name));
                    if (!child.NextVisible(false))
                    {
                        break;
                    }
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2f);
        }

        private static void DrawHeaders(FieldInfo field)
        {
            if (field == null)
            {
                return;
            }

            foreach (var header in field.GetCustomAttributes<HeaderAttribute>())
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
            }
        }

        private static string LabelFor(SerializedProperty property, FieldInfo field)
        {
            var attribute = field?.GetCustomAttribute<SkillLabelAttribute>();
            if (attribute != null)
            {
                return attribute.Label;
            }

            return ExternalLabels.TryGetValue(property.name, out var label) ? label : property.displayName;
        }

        /// <summary>折りたたみで中身を並べる「設定のまとまり」か（スキルの設定の型と、カメラの揺れの設定）。</summary>
        private static bool IsGroup(Type type)
        {
            if (type == null || !type.IsClass || type.IsArray || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return false;
            }

            if (type == typeof(SimpleShakeSettings))
            {
                return true;
            }

            return type.Namespace != null && type.Namespace.StartsWith("MS2026.Skills", StringComparison.Ordinal)
                   && type.IsSerializable;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                var field = t.GetField(name, FieldFlags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }
    }
}
