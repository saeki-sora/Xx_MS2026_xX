using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの表の下の便利ボタン: 時刻順に並べる・選んだ行を複製・全ての湧き位置に同じ行を足す・
    /// 選んだ行から後ろの時刻をまとめてずらす・数をまとめて倍にする。どれも Ctrl+Z で戻せる。
    /// あわせて、表の各行に出す1行の説明（<see cref="Summary"/>）もここに置く。
    /// </summary>
    public sealed class WaveEntryToolbar
    {
        private const string PrefShift = "MS2026.FortressDesigner.Wave.ShiftSeconds";
        private const string PrefScale = "MS2026.FortressDesigner.Wave.CountScale";

        public void Draw(WaveEntriesTarget target, WaveEntryListPanel list)
        {
            var entries = target.List;
            var selected = list.SelectedIndex >= 0 && list.SelectedIndex < entries.Count ? list.SelectedIndex : -1;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(entries.Count < 2))
                {
                    if (GUILayout.Button(new GUIContent("時刻順に並べる", "表の行を、発生時刻の早い順に並べ替えます（出る順番は元から時刻順なので、見やすくするだけ）。")))
                    {
                        target.Modify("時刻順に並べる", () => SortByTime(entries));
                    }
                }

                using (new EditorGUI.DisabledScope(selected < 0))
                {
                    if (GUILayout.Button(new GUIContent("選んだ行を複製", "表で選んでいる行をコピーして、すぐ下に足します。")))
                    {
                        target.Modify("行を複製", () => entries.Insert(selected + 1, entries[selected].Clone()));
                        list.SelectedIndex = selected + 1;
                    }
                }

                if (GUILayout.Button(new GUIContent("全ての湧き位置に足す",
                        "選んでいる行（無ければ新しい行）と同じ時刻・敵・数で、シーンの全ての湧き位置から湧く行をまとめて足します。四方から一斉に攻めてくる場面に。")))
                {
                    AddForAllSpawnPoints(target, selected);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var shift = EditorPrefs.GetFloat(PrefShift, 5f);
                EditorGUI.BeginChangeCheck();
                shift = EditorGUILayout.FloatField(new GUIContent("ずらす秒数", "右のボタンで、時刻をこの秒数だけ遅らせます（マイナスなら早めます。0秒より前にはなりません）。"), shift);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetFloat(PrefShift, shift);
                }

                var from = selected >= 0 ? entries[selected].triggerTime : 0f;
                var label = selected >= 0 ? $"{from:0.#}秒以降の行をずらす" : "全部の行をずらす";
                if (GUILayout.Button(new GUIContent(label, "途中に新しい場面を差し込みたいときに、選んだ行と、それより後の行の時刻をまとめてずらします（行を選んでいなければ全部）。"), GUILayout.Width(180)))
                {
                    target.Modify("時刻をずらす", () => ShiftFrom(entries, from, shift));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var scale = EditorPrefs.GetFloat(PrefScale, 1.2f);
                EditorGUI.BeginChangeCheck();
                scale = EditorGUILayout.FloatField(new GUIContent("数の倍率", "右のボタンで、全部の行の数をこの倍率にします（1体以上・四捨五入）。「秒数で決める」行は、かける秒数はそのままで密度が上がります。"), scale);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetFloat(PrefScale, Mathf.Max(0.01f, scale));
                }

                using (new EditorGUI.DisabledScope(entries.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent($"全部の数を ×{scale:0.##}", "難しさを全体的に上げ下げしたいときに。"), GUILayout.Width(180)))
                    {
                        target.Modify("数を倍にする", () => ScaleCounts(entries, scale));
                    }
                }
            }
        }

        /// <summary>表の各行に出す1行の説明（「12.0秒｜Spawn_0｜スピキ ×40（4.0秒かけて＝0.10秒おき）」）。</summary>
        public static string Summary(EnemySpawnEntry entry)
        {
            var place = string.IsNullOrEmpty(entry.spawnPointId) ? "湧き位置なし" : entry.spawnPointId;
            return $"{entry.triggerTime:0.0}秒｜{place}｜{WaveTimelinePanel.TypeName(entry.enemyType)} ×{entry.count}{Pace(entry)}";
        }

        /// <summary>出方の説明（「（4.0秒かけて＝0.10秒おき）」「（同時に）」、1体なら空）。</summary>
        public static string Pace(EnemySpawnEntry entry)
        {
            if (entry.count <= 1)
            {
                return "";
            }

            var interval = EnemyWaveStats.Interval(entry);
            return interval > 0f ? $"（{EnemyWaveStats.Duration(entry):0.0#}秒かけて＝{interval:0.###}秒おき）" : "（同時に）";
        }

        private static void SortByTime(List<EnemySpawnEntry> entries)
        {
            // 同じ時刻の行は元の順番のまま（安定な並べ替え）。
            var sorted = entries.Select((e, i) => (e, i)).OrderBy(x => x.e?.triggerTime ?? 0f).ThenBy(x => x.i).Select(x => x.e).ToList();
            entries.Clear();
            entries.AddRange(sorted);
        }

        private static void ShiftFrom(List<EnemySpawnEntry> entries, float from, float seconds)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.triggerTime >= from - 1e-4f)
                {
                    entry.triggerTime = Mathf.Max(0f, entry.triggerTime + seconds);
                }
            }
        }

        /// <summary>全部の行の数を倍率で変える（1体以上）。「間隔」の行は間隔のまま（＝かかる秒数がのびる）。</summary>
        public static void ScaleCounts(List<EnemySpawnEntry> entries, float scale)
        {
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    entry.count = Mathf.Max(1, Mathf.RoundToInt(entry.count * scale));
                }
            }
        }

        private static void AddForAllSpawnPoints(WaveEntriesTarget target, int selected)
        {
            var entries = target.List;
            var ids = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .Select(p => p.ResolvedId)
                .Distinct()
                .OrderBy(id => id, Comparer<string>.Create(EnemyWaveStats.CompareNatural))
                .ToList();
            if (ids.Count == 0)
            {
                EditorUtility.DisplayDialog("湧き位置がありません", "シーンに湧き位置がありません。「湧き位置」の欄で作ってください。", "OK");
                return;
            }

            var template = selected >= 0 ? entries[selected] : new EnemySpawnEntry
            {
                enemyType = entries.Select(e => e?.enemyType).FirstOrDefault(t => t != null)
            };
            var targets = ids.Where(id => selected < 0 || id != template.spawnPointId).ToList();
            var message = selected >= 0
                ? $"選んだ行（{Summary(template)}）と同じ内容で、ほかの {targets.Count} か所の湧き位置から湧く行を足します。"
                : $"新しい行（0秒・{targets.Count} か所の湧き位置それぞれから1体）を足します。足したあとで時刻・数を変えてください。";
            if (targets.Count == 0 || !EditorUtility.DisplayDialog("全ての湧き位置に足す", message, "足す", "やめる"))
            {
                return;
            }

            target.Modify("全ての湧き位置に足す", () =>
            {
                var insertAt = selected >= 0 ? selected + 1 : entries.Count;
                foreach (var id in targets)
                {
                    var copy = template.Clone();
                    copy.spawnPointId = id;
                    entries.Insert(insertAt++, copy);
                }
            });
        }
    }
}
