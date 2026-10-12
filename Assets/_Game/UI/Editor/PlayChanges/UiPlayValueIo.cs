using System.Globalization;
using UnityEditor;
using Object = UnityEngine.Object;

namespace MS2026.UI.EditorTools
{
    /// <summary>欄（SerializedProperty）の値を、Play 中の変更と同じ文字の形で読む・書く。</summary>
    public static class UiPlayValueIo
    {
        /// <summary>欄の今の値を読む。読めない種類なら false。</summary>
        public static bool TryRead(SerializedProperty property, out string text, out Object reference)
        {
            text = "";
            reference = null;
            var inv = CultureInfo.InvariantCulture;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    text = property.longValue.ToString(inv);
                    return true;
                case SerializedPropertyType.Boolean:
                    text = property.boolValue ? "1" : "0";
                    return true;
                case SerializedPropertyType.Float:
                    text = property.type == "double" ? property.doubleValue.ToString("R", inv) : property.floatValue.ToString("R", inv);
                    return true;
                case SerializedPropertyType.String:
                    text = property.stringValue;
                    return true;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    text = property.intValue.ToString(inv);
                    return true;
                case SerializedPropertyType.ObjectReference:
                    reference = property.objectReferenceValue;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>欄に書く。reference は参照の欄のときに入れる物（解決済み）。書けなければ problem に理由。</summary>
        public static bool TryWrite(SerializedProperty property, string text, Object reference, out string problem)
        {
            problem = "";
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (UiPlayValueText.TryLong(text, out var l))
                    {
                        property.longValue = l;
                        return true;
                    }

                    break;
                case SerializedPropertyType.Boolean:
                    if (UiPlayValueText.TryBool(text, out var b))
                    {
                        property.boolValue = b;
                        return true;
                    }

                    break;
                case SerializedPropertyType.Float:
                    if (UiPlayValueText.TryDouble(text, out var d))
                    {
                        if (property.type == "double")
                        {
                            property.doubleValue = d;
                        }
                        else
                        {
                            property.floatValue = (float)d;
                        }

                        return true;
                    }

                    break;
                case SerializedPropertyType.String:
                    property.stringValue = text ?? "";
                    return true;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    if (UiPlayValueText.TryLong(text, out var i))
                    {
                        property.intValue = (int)i;
                        return true;
                    }

                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = reference;
                    return true;
                default:
                    problem = $"この種類の欄（{property.propertyType}）は書き戻せません。";
                    return false;
            }

            problem = $"値「{UiPlayValueText.Short(text)}」を読めませんでした。";
            return false;
        }
    }
}
