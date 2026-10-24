using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの表の下の便利ボタン: 時刻順に並べる・選んだ行を複製・全ての湧き位置に同じ行を足す・
    /// 選んだ行から後ろの時刻をまとめてずらす。どれも Ctrl+Z で戻せる。
    /// あわせて、表の各行の上に出す1行の説明（<see cref="Summary"/>）もここに置く。
    /// </summary>
    public sealed class WaveEntryToolbar
    {
        private const string PrefShift = "MS2026.FortressDesigner.Wave.ShiftSeconds";

        public void Draw(EnemyWaveConfig wave, SerializedObject serializedWave, ReorderableList list)
        {
            var selected = list.index >= 0 && list.index < wave.spawnEntries.Count ? list.index : -1;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(wave.spawnEntries.Count < 2))
                {
                    if (GUILayout.Button(new GUIContent("時刻順に並べる", "表の行を、発生時刻の早い順に並べ替えます（再生の順番は元から時刻順なので、見やすくするだけ）。")))
                    {
                        Modify(wave, serializedWave, "時刻順に並べる", () => SortByTime(wave));
                    }
                }

                using (new EditorGUI.DisabledScope(selected < 0))
                {
                    if (GUILayout.Button(new GUIContent("選んだ行を複製", "表で選んでいる行をコピーして、すぐ下に足します。")))
                    {
                        Modify(wave, serializedWave, "行を複製", () => wave.spawnEntries.Insert(selected + 1, Copy(wave.spawnEntries[selected])));
                        list.index = selected + 1;
                    }
                }

                if (GUILayout.Button(new GUIContent("全ての湧き位置に足す",
                        "選んでいる行（無ければ新しい行）と同じ時刻・敵・数で、シーンの全ての湧き位置から湧く行をまとめて足します。四方から一斉に攻めてくる場面に。")))
                {
                    AddForAllSpawnPoints(wave, serializedWave, selected);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var shift = EditorPrefs.GetFloat(PrefShift, 5f);
                EditorGUI.BeginChangeCheck();
                shift = EditorGUILayout.FloatField(new GUIContent("ずらす秒数", "下のボタンで、時刻をこの秒数だけ遅らせます（マイナスなら早めます。0秒より前にはなりません）。"), shift);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetFloat(PrefShift, shift);
                }

                var from = selected >= 0 ? wave.spawnEntries[selected].triggerTime : 0f;
                var label = selected >= 0 ? $"{from:0.#}秒以降の行をずらす" : "全部の行をずらす";
                if (GUILayout.Button(new GUIContent(label, "途中に新しい場面を差し込みたいときに、選んだ行と、それより後の行の時刻をまとめてずらします（行を選んでいなければ全部）。"), GUILayout.Width(180)))
                {
                    Modify(wave, serializedWave, "時刻をずらす", () => ShiftFrom(wave, from, shift));
                }
            }
        }

        /// <summary>表の各行の上に出す1行の説明（「12.0秒〜｜Spawn_0｜スライム ×20（0.3秒おき・6.0秒かけて）」）。</summary>
        public static string Summary(EnemySpawnEntry entry)
        {
            var place = string.IsNullOrEmpty(entry.spawnPointId) ? "湧き位置なし" : entry.spawnPointId;
            var type = entry.enemyType == null ? "（種類なし）" : string.IsNullOrEmpty(entry.enemyType.displayName) ? entry.enemyType.name : entry.enemyType.displayName;
            var span = Mathf.Max(0, entry.count - 1) * Mathf.Max(0f, entry.intervalBetweenSpawns);
            var pace = entry.count <= 1 ? "" : entry.intervalBetweenSpawns > 0f ? $"（{entry.intervalBetweenSpawns:0.##}秒おき・{span:0.#}秒かけて）" : "（同時に）";
            return $"{entry.triggerTime:0.0}秒｜{place}｜{type} ×{entry.count}{pace}";
        }

        private static void Modify(EnemyWaveConfig wave, SerializedObject serializedWave, string undoName, System.Action change)
        {
            // 表の欄で変えた分を先に書き込んでから、直接書き換えて、表を読み直す。
            serializedWave.ApplyModifiedProperties();
            Undo.RecordObject(wave, undoName);
            change();
            EditorUtility.SetDirty(wave);
            serializedWave.Update();
            GUI.FocusControl(null);
        }

        private static void SortByTime(EnemyWaveConfig wave)
        {
            // 同じ時刻の行は元の順番のまま（安定な並べ替え）。
            var sorted = wave.spawnEntries.Select((e, i) => (e, i)).OrderBy(x => x.e?.triggerTime ?? 0f).ThenBy(x => x.i).Select(x => x.e).ToList();
            wave.spawnEntries.Clear();
            wave.spawnEntries.AddRange(sorted);
        }

        private static void ShiftFrom(EnemyWaveConfig wave, float from, float seconds)
        {
            foreach (var entry in wave.spawnEntries)
            {
                if (entry != null && entry.triggerTime >= from - 1e-4f)
                {
                    entry.triggerTime = Mathf.Max(0f, entry.triggerTime + seconds);
                }
            }
        }

        private static void AddForAllSpawnPoints(EnemyWaveConfig wave, SerializedObject serializedWave, int selected)
        {
            var ids = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .Select(p => p.ResolvedId)
                .Distinct()
                .OrderBy(id => id, Comparer<string>.Create(NaturalCompare))
                .ToList();
            if (ids.Count == 0)
            {
                EditorUtility.DisplayDialog("湧き位置がありません", "シーンに湧き位置がありません。上の「湧き位置の編集」で作ってください。", "OK");
                return;
            }

            var template = selected >= 0 ? wave.spawnEntries[selected] : new EnemySpawnEntry
            {
                enemyType = wave.spawnEntries.Select(e => e?.enemyType).FirstOrDefault(t => t != null)
            };
            var targets = ids.Where(id => selected < 0 || id != template.spawnPointId).ToList();
            var message = selected >= 0
                ? $"選んだ行（{Summary(template)}）と同じ内容で、ほかの {targets.Count} か所の湧き位置から湧く行を足します。"
                : $"新しい行（0秒・{targets.Count} か所の湧き位置それぞれから1体）を足します。足したあとで時刻・数を変えてください。";
            if (targets.Count == 0 || !EditorUtility.DisplayDialog("全ての湧き位置に足す", message, "足す", "やめる"))
            {
                return;
            }

            Modify(wave, serializedWave, "全ての湧き位置に足す", () =>
            {
                var insertAt = selected >= 0 ? selected + 1 : wave.spawnEntries.Count;
                foreach (var id in targets)
                {
                    var copy = Copy(template);
                    copy.spawnPointId = id;
                    wave.spawnEntries.Insert(insertAt++, copy);
                }
            });
        }

        private static EnemySpawnEntry Copy(EnemySpawnEntry source) => new EnemySpawnEntry
        {
            triggerTime = source.triggerTime,
            spawnPointId = source.spawnPointId,
            enemyType = source.enemyType,
            count = source.count,
            intervalBetweenSpawns = source.intervalBetweenSpawns
        };

        // Spawn_2 が Spawn_10 より前に来る並び。
        private static int NaturalCompare(string a, string b)
        {
            static (string, int) Split(string v)
            {
                var i = v.LastIndexOf('_');
                return i >= 0 && int.TryParse(v.Substring(i + 1), out var n) ? (v.Substring(0, i), n) : (v, -1);
            }

            var (na, ia) = Split(a ?? "");
            var (nb, ib) = Split(b ?? "");
            var c = string.CompareOrdinal(na, nb);
            return c != 0 ? c : ia.CompareTo(ib);
        }
    }
}
