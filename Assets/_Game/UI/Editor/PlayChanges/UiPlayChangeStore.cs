using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play 中の変更の置き場所（エディタを閉じるまで。スクリプトの読み直しでも消えない）。
    /// ファイルには保存しない: 残す／捨てるを決めるまでの一時的な一覧。
    /// </summary>
    public sealed class UiPlayChangeStore : ScriptableSingleton<UiPlayChangeStore>
    {
        [SerializeField] private List<UiPlayChange> changes = new List<UiPlayChange>();
        [SerializeField] private int version;

        /// <summary>一覧が変わったとき。</summary>
        public static event Action Changed;

        public static IReadOnlyList<UiPlayChange> Items => instance.changes;

        /// <summary>一覧が変わるたびに増える数（画面の作り直しの判定に使う）。</summary>
        public static int Version => instance.version;

        public static int Count => instance.changes.Count;

        /// <summary>残せる（問題の無い）変更の数。</summary>
        public static int ApplicableCount
        {
            get
            {
                var count = 0;
                foreach (var change in instance.changes)
                {
                    if (change.CanApply)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static bool Contains(string key) => instance.List.Contains(key);

        /// <summary>足す（同じ物の同じ欄なら値を新しくする）。</summary>
        public static void Merge(UiPlayChange change)
        {
            instance.List.Merge(change);
            Touch();
        }

        public static void Remove(UiPlayChange change)
        {
            instance.List.Remove(change);
            Touch();
        }

        public static void RemoveAll(Predicate<UiPlayChange> match)
        {
            instance.List.RemoveAll(match);
            Touch();
        }

        public static void Clear()
        {
            instance.List.Clear();
            Touch();
        }

        /// <summary>チェックや問題の書き換えなど、行の中身を直接変えたあとに呼ぶ。</summary>
        public static void Touch()
        {
            instance.version++;
            Changed?.Invoke();
        }

        private UiPlayChangeList List => new UiPlayChangeList(changes);
    }
}
