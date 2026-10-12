using System;
using System.Collections.Generic;
using System.Globalization;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play 中の変更の一覧（Unity に頼らない部分。テストあり）。
    /// 同じ物の同じ欄は1行にまとめ（変える前の値と並び順は最初のまま、値は最新に）、書き戻すときは配列の数を先に直す。
    /// </summary>
    public sealed class UiPlayChangeList
    {
        private const string ArraySizeSuffix = ".Array.size";

        private readonly List<UiPlayChange> _items;

        public UiPlayChangeList(List<UiPlayChange> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<UiPlayChange> Items => _items;

        public int Count => _items.Count;

        public static bool IsArraySize(string propertyPath) =>
            propertyPath != null && propertyPath.EndsWith(ArraySizeSuffix, StringComparison.Ordinal);

        /// <summary>足す。同じ物の同じ欄がすでにあれば、値だけ新しくする。新しい行なら true。</summary>
        public bool Merge(UiPlayChange change)
        {
            var key = change.Key;
            foreach (var item in _items)
            {
                if (item.Key != key)
                {
                    continue;
                }

                item.value = change.value;
                item.objectValue = change.objectValue;
                item.referencesLocal = change.referencesLocal;
                item.reference = change.reference;
                item.after = change.after;
                item.problem = change.problem;
                if (!string.IsNullOrEmpty(change.label))
                {
                    item.label = change.label;
                }

                return false;
            }

            _items.Add(change);
            return true;
        }

        public bool Contains(string key)
        {
            foreach (var item in _items)
            {
                if (item.Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        public void Remove(UiPlayChange change) => _items.Remove(change);

        public void RemoveAll(Predicate<UiPlayChange> match) => _items.RemoveAll(match);

        public void Clear() => _items.Clear();

        /// <summary>書き戻す順番: 配列の数（増えた・減った）を先に、そのあと中身（記録した順）。</summary>
        public static List<UiPlayChange> ApplyOrder(IEnumerable<UiPlayChange> changes)
        {
            var sizes = new List<UiPlayChange>();
            var others = new List<UiPlayChange>();
            foreach (var change in changes)
            {
                (change.IsArraySize ? sizes : others).Add(change);
            }

            sizes.AddRange(others);
            return sizes;
        }

        /// <summary>書き戻し先（Prefab ファイル・シーン）ごとにまとめる（最初に出てきた順）。</summary>
        public static List<List<UiPlayChange>> GroupByDestination(IEnumerable<UiPlayChange> changes)
        {
            var groups = new List<List<UiPlayChange>>();
            var index = new Dictionary<string, List<UiPlayChange>>();
            foreach (var change in changes)
            {
                var key = change.target.DestinationKey;
                if (!index.TryGetValue(key, out var group))
                {
                    index[key] = group = new List<UiPlayChange>();
                    groups.Add(group);
                }

                group.Add(change);
            }

            return groups;
        }
    }

    /// <summary>名前の道のりの計算（Unity に頼らない部分。テストあり）。</summary>
    public static class UiPlayPath
    {
        /// <summary>兄弟の名前の並び（siblingNames）の position 番目が、同じ名前の中で何番目か。</summary>
        public static int IndexAmongSameName(IReadOnlyList<string> siblingNames, int position)
        {
            var name = siblingNames[position];
            var index = 0;
            for (var i = 0; i < position; i++)
            {
                if (siblingNames[i] == name)
                {
                    index++;
                }
            }

            return index;
        }

        /// <summary>兄弟の名前の並びから、同じ名前の index 番目の位置を探す。無ければ -1。</summary>
        public static int FindPosition(IReadOnlyList<string> siblingNames, string name, int index)
        {
            var seen = 0;
            for (var i = 0; i < siblingNames.Count; i++)
            {
                if (siblingNames[i] != name)
                {
                    continue;
                }

                if (seen == index)
                {
                    return i;
                }

                seen++;
            }

            return -1;
        }

        /// <summary>表示用の「A › B › C」。</summary>
        public static string Describe(string head, IReadOnlyList<string> names)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(head))
            {
                parts.Add(head);
            }

            parts.AddRange(names);
            return string.Join(" › ", parts);
        }
    }

    /// <summary>Unity が記録する値の文字（PropertyModification.value）を読む（テストあり）。</summary>
    public static class UiPlayValueText
    {
        public static bool TryBool(string text, out bool value)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                    value = true;
                    return true;
                case "0":
                case "false":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        public static bool TryLong(string text, out long value) =>
            long.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public static bool TryDouble(string text, out double value)
        {
            var trimmed = text?.Trim();
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            switch (trimmed)
            {
                case "Infinity":
                    value = double.PositiveInfinity;
                    return true;
                case "-Infinity":
                    value = double.NegativeInfinity;
                    return true;
                case "NaN":
                    value = double.NaN;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>2つの値の文字が同じ値か（数は少しの誤差を同じとみなす）。</summary>
        public static bool Same(string a, string b)
        {
            if (string.Equals(a ?? "", b ?? "", StringComparison.Ordinal))
            {
                return true;
            }

            return TryDouble(a, out var x) && TryDouble(b, out var y) &&
                   (x.Equals(y) || Math.Abs(x - y) <= 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y))));
        }

        /// <summary>一覧に出すために短くする。</summary>
        public static string Short(string text, int max = 40)
        {
            if (text == null)
            {
                return "";
            }

            text = text.Replace("\r", "").Replace("\n", "⏎");
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }
    }
}
