using System.Collections.Generic;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// Play 中に、欄ごとに「鳴った回数」と「1秒に一番多く鳴った回数」を数える（うるさい所を見つけるため）。
    /// Unity に頼らないのでテストできる。時刻は秒（何を使ってもよいが、増えていくこと）。
    /// </summary>
    public sealed class SoundPlayStats
    {
        private sealed class Entry
        {
            public readonly Queue<double> Recent = new Queue<double>();
            public int Total;
            public int MaxPerSecond;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        /// <summary>数えている欄（鳴った物だけ）。</summary>
        public IEnumerable<string> Keys => _entries.Keys;

        /// <summary>記録を全部消した・足したときに増える（画面の描き直しの合図）。</summary>
        public int Version { get; private set; }

        public void Record(string key, double time)
        {
            if (key == null)
            {
                return;
            }

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            entry.Total++;
            entry.Recent.Enqueue(time);
            while (entry.Recent.Count > 0 && time - entry.Recent.Peek() >= 1.0)
            {
                entry.Recent.Dequeue();
            }

            if (entry.Recent.Count > entry.MaxPerSecond)
            {
                entry.MaxPerSecond = entry.Recent.Count;
            }

            Version++;
        }

        public int Total(string key) => key != null && _entries.TryGetValue(key, out var e) ? e.Total : 0;

        public int MaxPerSecond(string key) => key != null && _entries.TryGetValue(key, out var e) ? e.MaxPerSecond : 0;

        public void Clear()
        {
            _entries.Clear();
            Version++;
        }
    }
}
