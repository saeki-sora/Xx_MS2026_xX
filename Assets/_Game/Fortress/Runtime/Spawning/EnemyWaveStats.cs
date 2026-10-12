using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの集計（Unity に頼らない計算。テストあり）。合計の数・最後に湧く時刻・1行が湧き終わる時刻・
    /// ある時刻までに湧いた数・タイムラインの目盛りの間隔など、要塞デザイナーの表示に使う。
    /// </summary>
    public static class EnemyWaveStats
    {
        /// <summary>1行が湧き始める時刻。</summary>
        public static float StartTime(EnemySpawnEntry entry) => Mathf.Max(0f, entry.triggerTime);

        /// <summary>1行の最後の1体が湧く時刻（数 −1 回ぶん間隔を空ける）。</summary>
        public static float EndTime(EnemySpawnEntry entry) =>
            StartTime(entry) + Mathf.Max(0, entry.count - 1) * Mathf.Max(0f, entry.intervalBetweenSpawns);

        /// <summary>全部の行の合計の数。</summary>
        public static int TotalEnemies(IReadOnlyList<EnemySpawnEntry> entries)
        {
            var total = 0;
            if (entries == null)
            {
                return total;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    total += Mathf.Max(0, entry.count);
                }
            }

            return total;
        }

        /// <summary>最後の1体が湧く時刻（行が無ければ 0）。</summary>
        public static float LastSpawnTime(IReadOnlyList<EnemySpawnEntry> entries)
        {
            var last = 0f;
            if (entries == null)
            {
                return last;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    last = Mathf.Max(last, EndTime(entry));
                }
            }

            return last;
        }

        /// <summary>time 秒までに湧いた数（再生中の「出た数」の目安）。</summary>
        public static int SpawnedBy(IReadOnlyList<EnemySpawnEntry> entries, float time)
        {
            var total = 0;
            if (entries == null)
            {
                return total;
            }

            foreach (var entry in entries)
            {
                if (entry == null || entry.count <= 0 || time < StartTime(entry))
                {
                    continue;
                }

                var interval = Mathf.Max(0f, entry.intervalBetweenSpawns);
                total += interval <= 0f
                    ? entry.count
                    : Mathf.Min(entry.count, 1 + Mathf.FloorToInt((time - StartTime(entry)) / interval + 1e-4f));
            }

            return total;
        }

        /// <summary>行で使っている湧き位置の名前（重なりなし。Spawn_2 が Spawn_10 より前の自然な順）。空の名前は "" として入る。</summary>
        public static List<string> Lanes(IReadOnlyList<EnemySpawnEntry> entries)
        {
            var lanes = new List<string>();
            if (entries == null)
            {
                return lanes;
            }

            foreach (var entry in entries)
            {
                var id = entry?.spawnPointId ?? "";
                if (entry != null && !lanes.Contains(id))
                {
                    lanes.Add(id);
                }
            }

            lanes.Sort(CompareNatural);
            return lanes;
        }

        /// <summary>タイムラインの目盛りの間隔（1・2・5 の倍数のうち、目盛りが maxTicks 本以下になる最小の物）。</summary>
        public static float TickStep(float duration, int maxTicks = 10)
        {
            duration = Mathf.Max(0.001f, duration);
            maxTicks = Mathf.Max(1, maxTicks);
            var step = 0.1f;
            var multipliers = new[] { 1f, 2f, 5f };
            for (var power = 0; power < 8; power++)
            {
                foreach (var m in multipliers)
                {
                    var candidate = step * m;
                    if (duration / candidate <= maxTicks)
                    {
                        return candidate;
                    }
                }

                step *= 10f;
            }

            return step;
        }

        /// <summary>「名前_番号」の番号を数として比べる（Spawn_2 &lt; Spawn_10）。</summary>
        public static int CompareNatural(string a, string b)
        {
            var (nameA, numA) = Split(a ?? "");
            var (nameB, numB) = Split(b ?? "");
            var byName = string.CompareOrdinal(nameA, nameB);
            return byName != 0 ? byName : numA.CompareTo(numB);
        }

        private static (string name, int number) Split(string value)
        {
            var index = value.LastIndexOf('_');
            if (index >= 0 && int.TryParse(value.Substring(index + 1), out var number))
            {
                return (value.Substring(0, index), number);
            }

            return (value, -1);
        }
    }
}
