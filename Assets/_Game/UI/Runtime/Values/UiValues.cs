using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>
    /// ゲーム → 画面へ値を渡す「掲示板」。ゲーム側は名前（キー）を付けて値を書くだけ、画面の部品はその名前を見て表示を変える。
    /// 例: ゲームが "core.hp01" に 0.7 を書く → HPゲージ（UiBindFill）が7割になる。
    /// 書いた値が前と同じなら何もしない（毎フレーム書いても軽い）。数・ON/OFFはメモリ確保なしで受け渡せる。
    /// 編集中も使えるので、UIスタジオの「サンプル値」で Play せずに見た目を確かめられる。
    /// </summary>
    public static class UiValues
    {
        private sealed class Entry
        {
            public UiValue Value;
            public Action<UiValue> Changed;
            public int Version;
        }

        private sealed class Subscription : IDisposable
        {
            private Entry _entry;
            private Action<UiValue> _callback;

            public Subscription(Entry entry, Action<UiValue> callback)
            {
                _entry = entry;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_entry != null)
                {
                    _entry.Changed -= _callback;
                    _entry = null;
                    _callback = null;
                }
            }
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        /// <summary>どれかの値が変わるたびに増える数（ツールの表示の更新判定用）。</summary>
        public static int Version { get; private set; }

        public static IEnumerable<string> Keys => Entries.Keys;

        public static void Set(string key, float value) => Set(key, UiValue.Of(value));
        public static void Set(string key, int value) => Set(key, UiValue.Of(value));
        public static void Set(string key, bool value) => Set(key, UiValue.Of(value));
        public static void Set(string key, string value) => Set(key, UiValue.Of(value));
        public static void Set(string key, Color value) => Set(key, UiValue.Of(value));

        public static void Set(string key, UiValue value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var entry = GetOrCreate(key);
            if (entry.Value.Equals(value))
            {
                return;
            }

            entry.Value = value;
            entry.Version++;
            Version++;
            entry.Changed?.Invoke(value);
        }

        public static bool TryGet(string key, out UiValue value)
        {
            if (!string.IsNullOrEmpty(key) && Entries.TryGetValue(key, out var entry) && entry.Value.HasValue)
            {
                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }

        public static float GetNumber(string key, float fallback = 0f) => TryGet(key, out var v) ? v.AsNumber : fallback;

        public static bool GetBool(string key, bool fallback = false) => TryGet(key, out var v) ? v.AsBool : fallback;

        /// <summary>値が変わったら呼ばれるようにする。invokeNow なら今の値ですぐ1回呼ぶ。戻り値を Dispose すると止まる。</summary>
        public static IDisposable Subscribe(string key, Action<UiValue> onChanged, bool invokeNow = true)
        {
            if (string.IsNullOrEmpty(key) || onChanged == null)
            {
                return null;
            }

            var entry = GetOrCreate(key);
            entry.Changed += onChanged;
            if (invokeNow && entry.Value.HasValue)
            {
                onChanged(entry.Value);
            }

            return new Subscription(entry, onChanged);
        }

        /// <summary>値を全部消す（Play開始時に自動で呼ばれる）。購読は残る。</summary>
        public static void ClearValues()
        {
            foreach (var entry in Entries.Values)
            {
                entry.Value = default;
                entry.Version++;
            }

            Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Entries.Clear();
            Version = 0;
        }

        private static Entry GetOrCreate(string key)
        {
            if (!Entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                Entries[key] = entry;
            }

            return entry;
        }
    }
}
