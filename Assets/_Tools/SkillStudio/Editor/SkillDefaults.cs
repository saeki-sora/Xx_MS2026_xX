using System;
using System.Collections.Generic;
using System.Globalization;
using MS2026.Skills.EditorTools;
using UnityEditor;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 初期値（プログラムに書いてある値）との比べ方と戻し方（使いやすさの資料の T4: 変えた所に印、押せば初期値に戻る）。
    /// 初期値は、スキルの型を新しく1つ作ったときの値。設定ファイルの値と違えば「変えた所」。
    /// </summary>
    internal static class SkillDefaults
    {
        private const float FloatTolerance = 1e-5f;
        private static readonly Dictionary<Type, SerializedObject> Cache = new Dictionary<Type, SerializedObject>();

        /// <summary>この項目の初期値（同じ場所の項目）。初期値が作れない物なら null。</summary>
        public static SerializedProperty DefaultOf(SerializedProperty property)
        {
            var target = property?.serializedObject.targetObject;
            if (!(target is ScriptableObject))
            {
                return null;
            }

            var type = target.GetType();
            if (!Cache.TryGetValue(type, out var defaults) || defaults.targetObject == null)
            {
                var instance = ScriptableObject.CreateInstance(type);
                instance.hideFlags = HideFlags.HideAndDontSave;
                defaults = new SerializedObject(instance);
                Cache[type] = defaults;
            }

            return defaults.FindProperty(property.propertyPath);
        }

        public static bool IsChanged(SerializedProperty property)
        {
            var defaults = DefaultOf(property);
            return defaults != null && !Same(property, defaults);
        }

        /// <summary>初期値に戻す（呼んだ側で ApplyModifiedProperties すると Ctrl+Z で取り消せる）。</summary>
        public static void Reset(SerializedProperty property)
        {
            var defaults = DefaultOf(property);
            if (defaults != null)
            {
                property.serializedObject.CopyFromSerializedProperty(defaults);
            }
        }

        /// <summary>まとまりの中で、初期値から変えた項目の数（入れ子のまとまりは中まで数える。付け外し・強さは1つと数える）。</summary>
        public static int CountChanged(SerializedProperty root)
        {
            if (root == null)
            {
                return 0;
            }

            if (!IsGroupLike(root))
            {
                return IsChanged(root) ? 1 : 0;
            }

            var count = 0;
            var child = root.Copy();
            var end = root.GetEndProperty();
            if (!child.NextVisible(true))
            {
                return 0;
            }

            while (!SerializedProperty.EqualContents(child, end))
            {
                count += CountChanged(child.Copy());
                if (!child.NextVisible(false))
                {
                    break;
                }
            }

            return count;
        }

        /// <summary>初期値を短い文字にする（「↺」のボタンの説明に出す）。</summary>
        public static string Describe(SerializedProperty property)
        {
            var defaults = DefaultOf(property);
            if (defaults == null)
            {
                return "";
            }

            switch (defaults.propertyType)
            {
                case SerializedPropertyType.Float:
                    return defaults.floatValue.ToString("0.###", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Integer:
                    return defaults.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return defaults.boolValue ? "ON" : "OFF";
                case SerializedPropertyType.Enum:
                    var names = defaults.enumDisplayNames;
                    return defaults.enumValueIndex >= 0 && defaults.enumValueIndex < names.Length ? names[defaults.enumValueIndex] : "";
                case SerializedPropertyType.Color:
                    return "#" + ColorUtility.ToHtmlStringRGBA(defaults.colorValue);
                case SerializedPropertyType.Vector2:
                    return defaults.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return defaults.vector3Value.ToString();
                case SerializedPropertyType.ObjectReference:
                    return defaults.objectReferenceValue != null ? defaults.objectReferenceValue.name : "なし";
                case SerializedPropertyType.Generic when defaults.type == "SkillEffectDial":
                    var enabled = defaults.FindPropertyRelative("enabled");
                    var amount = defaults.FindPropertyRelative("amount");
                    return enabled != null && enabled.boolValue && amount != null
                        ? "出す・強さ " + amount.floatValue.ToString("0.##", CultureInfo.InvariantCulture)
                        : "出さない";
                default:
                    return "";
            }
        }

        /// <summary>スキル調整スタジオの中で描く間だけ、項目の横に「変えた印」と「↺ 戻す」を出す。</summary>
        public static void DrawWithMarks(Action draw)
        {
            SkillInspectorGUI.IsChangedFromDefault = IsChanged;
            SkillInspectorGUI.ResetToDefault = Reset;
            SkillInspectorGUI.DefaultValueText = Describe;
            try
            {
                draw();
            }
            finally
            {
                SkillInspectorGUI.IsChangedFromDefault = null;
                SkillInspectorGUI.ResetToDefault = null;
                SkillInspectorGUI.DefaultValueText = null;
            }
        }

        private static bool IsGroupLike(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic && !property.isArray && property.type != "SkillEffectDial"
                   && property.hasVisibleChildren;
        }

        private static bool Same(SerializedProperty a, SerializedProperty b)
        {
            if (a.propertyType != b.propertyType)
            {
                return false;
            }

            switch (a.propertyType)
            {
                case SerializedPropertyType.Float:
                    return Mathf.Abs(a.floatValue - b.floatValue) <= FloatTolerance * Mathf.Max(1f, Mathf.Abs(b.floatValue));
                case SerializedPropertyType.Color:
                    var ca = a.colorValue;
                    var cb = b.colorValue;
                    return Mathf.Abs(ca.r - cb.r) <= 1e-4f && Mathf.Abs(ca.g - cb.g) <= 1e-4f && Mathf.Abs(ca.b - cb.b) <= 1e-4f
                           && Mathf.Abs(ca.a - cb.a) <= 1e-4f;
                case SerializedPropertyType.Generic when !a.isArray:
                    var child = a.Copy();
                    var end = a.GetEndProperty();
                    if (!child.Next(true))
                    {
                        return true;
                    }

                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        var other = b.serializedObject.FindProperty(child.propertyPath);
                        if (other == null || !Same(child.Copy(), other))
                        {
                            return false;
                        }

                        if (!child.Next(false))
                        {
                            break;
                        }
                    }

                    return true;
                default:
                    return SerializedProperty.DataEquals(a, b);
            }
        }
    }
}
