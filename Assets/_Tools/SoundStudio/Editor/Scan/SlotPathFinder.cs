using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.SoundStudio
{
    /// <summary>見つけた音の欄1つ（どこに・どの型の・どの欄か、と今の中身）。</summary>
    public sealed class FoundSlot
    {
        public FoundSlot(string path, Type declaringType, FieldInfo field, bool isBgm, object value, object container, string elementName)
        {
            Path = path;
            DeclaringType = declaringType;
            Field = field;
            IsBgm = isBgm;
            Value = value;
            Container = container;
            ElementName = elementName;
        }

        /// <summary>SerializedProperty.propertyPath と同じ書き方の道筋（配列は <c>name.Array.data[i]</c>）。</summary>
        public string Path { get; }

        /// <summary>欄を書いている型（入れ子の設定クラスならその型）。</summary>
        public Type DeclaringType { get; }

        public FieldInfo Field { get; }
        public bool IsBgm { get; }

        /// <summary>欄の今の中身（FortressEffect は実物そのもの。BGM は AssetId の値）。</summary>
        public object Value { get; }

        /// <summary>この欄を持っている物（入れ子の設定クラスならその物）。「この仕組みを使っているか」のスイッチを見るのに使う。</summary>
        public object Container { get; }

        /// <summary>配列の中の欄なら、その要素の名前（label などの文字の欄。無ければ何番目か）。配列の外なら null。</summary>
        public string ElementName { get; }
    }

    /// <summary>
    /// 1つの物（設定ファイル・部品）の中から、音の欄（<see cref="FortressEffect"/> と BGM の番号札）を探す。
    /// Unity が保存する欄だけをたどる（public か [SerializeField]、[NonSerialized] と static は除く）。Unity の物（別のアセット）の中へは入らない。
    /// </summary>
    public static class SlotPathFinder
    {
        private const int MaxDepth = 8;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, bool> MayHoldCache = new Dictionary<Type, bool>();

        /// <summary>物の中の音の欄を全部返す。</summary>
        public static List<FoundSlot> FindSlots(object root)
        {
            var result = new List<FoundSlot>();
            if (root != null)
            {
                Walk(root, root.GetType(), string.Empty, 0, result, null);
            }

            return result;
        }

        /// <summary>この型が（中身を見なくても）音の欄を持ちうるか。持たない型の物は読み込まずに飛ばす。</summary>
        public static bool MayHoldSlots(Type type)
        {
            return type != null && MayHold(type, 0, new HashSet<Type>());
        }

        /// <summary>Unity が保存する欄（基底クラスの欄も含む）。</summary>
        public static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject); t = t.BaseType)
            {
                chain.Add(t);
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                foreach (var field in chain[i].GetFields(Flags))
                {
                    if (field.IsStatic || field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute), false))
                    {
                        continue;
                    }

                    if (field.IsPublic || field.IsDefined(typeof(SerializeField), false))
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool IsEffect(Type t) => t == typeof(FortressEffect);
        private static bool IsBgm(Type t) => t == typeof(AssetId<BgmMarker>);

        private static Type ElementType(Type t)
        {
            if (t.IsArray)
            {
                return t.GetElementType();
            }

            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) ? t.GetGenericArguments()[0] : null;
        }

        private static bool IsSerializableContainer(Type t)
        {
            return !typeof(UnityEngine.Object).IsAssignableFrom(t) && !t.IsPrimitive && !t.IsEnum && t != typeof(string)
                   && t.IsDefined(typeof(SerializableAttribute), false);
        }

        private static void Walk(object obj, Type type, string prefix, int depth, List<FoundSlot> result, string elementName)
        {
            if (depth > MaxDepth)
            {
                return;
            }

            foreach (var field in SerializedFields(type))
            {
                var path = prefix.Length == 0 ? field.Name : prefix + "." + field.Name;
                var value = field.GetValue(obj);
                Visit(obj, field, field.FieldType, value, path, depth, result, elementName);
            }
        }

        private static void Visit(object container, FieldInfo field, Type fieldType, object value, string path, int depth, List<FoundSlot> result, string elementName)
        {
            if (IsEffect(fieldType))
            {
                result.Add(new FoundSlot(path, field.DeclaringType, field, false, value, container, elementName));
                return;
            }

            if (IsBgm(fieldType))
            {
                result.Add(new FoundSlot(path, field.DeclaringType, field, true, value, container, elementName));
                return;
            }

            var element = ElementType(fieldType);
            if (element != null)
            {
                if (!(value is IList list) || !(IsEffect(element) || IsBgm(element) || IsSerializableContainer(element)))
                {
                    return;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    Visit(container, field, element, list[i], path + ".Array.data[" + i + "]", depth + 1, result, NameOfElement(list[i], i));
                }

                return;
            }

            if (value != null && IsSerializableContainer(fieldType))
            {
                Walk(value, value.GetType(), path, depth + 1, result, elementName);
            }
        }

        private static readonly string[] ElementNameFields = { "label", "displayName", "name", "title" };

        // 配列の要素の名前: 要素に label などの文字の欄があればそれ、無ければ何番目か（1 から）。
        private static string NameOfElement(object element, int index)
        {
            if (element != null && !(element is UnityEngine.Object))
            {
                foreach (var name in ElementNameFields)
                {
                    var field = element.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(string) && field.GetValue(element) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }

            return (index + 1).ToString();
        }

        private static bool MayHold(Type type, int depth, HashSet<Type> visiting)
        {
            lock (MayHoldCache)
            {
                if (MayHoldCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }
            }

            if (depth > MaxDepth || !visiting.Add(type))
            {
                return false;
            }

            var found = false;
            foreach (var field in SerializedFields(type))
            {
                var t = ElementType(field.FieldType) ?? field.FieldType;
                if (IsEffect(t) || IsBgm(t) || (IsSerializableContainer(t) && MayHold(t, depth + 1, visiting)))
                {
                    found = true;
                    break;
                }
            }

            visiting.Remove(type);
            lock (MayHoldCache)
            {
                MayHoldCache[type] = found;
            }

            return found;
        }
    }
}
