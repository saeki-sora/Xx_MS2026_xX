using System;
using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブのタイムライン。湧き位置ごとに1段、1行（エントリ）を「湧き始め〜湧き終わり」の棒で描く。
    /// 棒の色＝敵の種類、棒の中の数字＝数。棒をクリックすると下の表のその行を選ぶ。Play 中は今の時刻に赤い線。
    /// 上に合計の数・最後に湧く時刻・種類ごとの数をまとめて出す。
    /// </summary>
    public sealed class WaveTimelinePanel
    {
        private const float LabelWidth = 96f;
        private const float LaneHeight = 18f;
        private const float RulerHeight = 16f;
        private const float RightPadding = 10f;

        private static readonly Color Background = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color LaneStripe = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color TickColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color PlayheadColor = new Color(1f, 0.3f, 0.3f, 0.95f);
        private static readonly Color MissingColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color NoTypeColor = new Color(0.6f, 0.6f, 0.6f);

        private GUIStyle _barLabel;

        /// <param name="entries">湧きの行。</param>
        /// <param name="repeatText">1行目の最後に付ける、くり返しの説明（「・1回で終わり」など）。</param>
        /// <param name="playhead">再生中なら今の秒数（この行の並びが再生されていなければ null）。</param>
        /// <param name="selectedIndex">下の表で選んでいる行（無ければ −1）。</param>
        /// <param name="onSelect">棒をクリックしたときに、その行の番号で呼ばれる。</param>
        public void Draw(IReadOnlyList<EnemySpawnEntry> entries, string repeatText, float? playhead, HashSet<string> knownIds, int selectedIndex, Action<int> onSelect)
        {
            DrawSummary(entries, repeatText);
            if (entries == null || entries.Count == 0)
            {
                EditorGUILayout.HelpBox("まだ湧きの行がありません。下の表の「＋」で足すと、ここに時間の流れが出ます。", MessageType.Info);
                return;
            }

            var playing = playhead.HasValue;
            var now = playhead ?? -1f;
            var lanes = EnemyWaveStats.Lanes(entries);
            var last = EnemyWaveStats.LastSpawnTime(entries);
            var duration = Mathf.Max(5f, Mathf.Max(last, now) * 1.08f + 0.5f);

            var rect = GUILayoutUtility.GetRect(10f, RulerHeight + lanes.Count * LaneHeight + 4f, GUILayout.ExpandWidth(true));
            var timeLeft = rect.x + LabelWidth;
            var timeWidth = Mathf.Max(20f, rect.width - LabelWidth - RightPadding);
            float X(float t) => timeLeft + Mathf.Clamp01(t / duration) * timeWidth;

            var e = Event.current;
            string hover = null;
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Background);
                DrawRuler(rect, duration, timeWidth, X);
            }

            for (var lane = 0; lane < lanes.Count; lane++)
            {
                var y = rect.y + RulerHeight + lane * LaneHeight;
                var id = lanes[lane];
                var missing = string.IsNullOrEmpty(id) || !knownIds.Contains(id);
                if (e.type == EventType.Repaint)
                {
                    if (lane % 2 == 0)
                    {
                        EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, LaneHeight), LaneStripe);
                    }

                    var labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = missing ? MissingColor : EditorStyles.miniLabel.normal.textColor } };
                    var name = string.IsNullOrEmpty(id) ? "（未設定）" : missing ? $"{id}（無い）" : id;
                    GUI.Label(new Rect(rect.x + 4f, y + 1f, LabelWidth - 6f, LaneHeight - 2f),
                        new GUIContent(name, missing ? "この湧き位置はシーンにありません。この段の敵は湧きません。" : null), labelStyle);
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var lane = lanes.IndexOf(entry.spawnPointId ?? "");
                var y = rect.y + RulerHeight + lane * LaneHeight + 2f;
                var x0 = X(EnemyWaveStats.StartTime(entry));
                var x1 = Mathf.Max(x0 + 5f, X(EnemyWaveStats.EndTime(entry)));
                var bar = new Rect(x0, y, x1 - x0, LaneHeight - 4f);

                if (bar.Contains(e.mousePosition))
                {
                    hover = Describe(entry);
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        onSelect?.Invoke(i);
                        e.Use();
                    }
                }

                if (e.type != EventType.Repaint)
                {
                    continue;
                }

                var color = entry.enemyType != null ? entry.enemyType.placeholderColor : NoTypeColor;
                color.a = 0.85f;
                EditorGUI.DrawRect(bar, color);
                if (i == selectedIndex)
                {
                    DrawOutline(bar, Color.white);
                }

                if (bar.width >= 24f)
                {
                    _barLabel ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.black } };
                    GUI.Label(new Rect(bar.x + 3f, bar.y - 1f, bar.width - 3f, bar.height + 2f), $"×{entry.count}", _barLabel);
                }
            }

            if (playing && e.type == EventType.Repaint)
            {
                var x = X(now);
                EditorGUI.DrawRect(new Rect(x - 1f, rect.y, 2f, rect.height), PlayheadColor);
            }

            var footer = hover ?? (playing
                ? $"再生中 {now:0.0}秒 ・ この周で約 {EnemyWaveStats.SpawnedBy(entries, now)}体（赤い線が今）"
                : "棒をクリックすると下の表のその行を選びます。棒の長さ＝湧き始めから湧き終わりまで、色＝敵の種類、数字＝数。");
            EditorGUILayout.LabelField(footer, EditorStyles.miniLabel);
        }

        private static void DrawSummary(IReadOnlyList<EnemySpawnEntry> entries, string repeatText)
        {
            var total = EnemyWaveStats.TotalEnemies(entries);
            var last = EnemyWaveStats.LastSpawnTime(entries);
            EditorGUILayout.LabelField($"合計 {total}体 ・ {entries?.Count ?? 0}行 ・ 最後の湧き {last:0.0}秒{repeatText}", EditorStyles.miniBoldLabel);

            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var byType = entries
                .Where(x => x != null)
                .GroupBy(x => x.enemyType)
                .Select(g => $"{TypeName(g.Key)} {g.Sum(x => Mathf.Max(0, x.count))}体")
                .ToArray();
            EditorGUILayout.LabelField("種類ごと: " + string.Join(" ／ ", byType), EditorStyles.miniLabel);
        }

        private static void DrawRuler(Rect rect, float duration, float timeWidth, Func<float, float> x)
        {
            var step = EnemyWaveStats.TickStep(duration, Mathf.Max(2, Mathf.FloorToInt(timeWidth / 56f)));
            for (var t = 0f; t <= duration + 1e-3f; t += step)
            {
                var px = x(t);
                EditorGUI.DrawRect(new Rect(px, rect.y + RulerHeight - 4f, 1f, rect.height - RulerHeight + 4f), TickColor);
                GUI.Label(new Rect(px + 2f, rect.y, 60f, RulerHeight), step < 1f ? $"{t:0.0}秒" : $"{t:0}秒", EditorStyles.miniLabel);
            }
        }

        private static void DrawOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, 2f), color);
            EditorGUI.DrawRect(new Rect(r.x - 1f, r.yMax - 1f, r.width + 2f, 2f), color);
            EditorGUI.DrawRect(new Rect(r.x - 1f, r.y - 1f, 2f, r.height + 2f), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y - 1f, 2f, r.height + 2f), color);
        }

        /// <summary>1行の説明（「12.0〜18.0秒｜Spawn_0｜スライム ×20（6.0秒かけて＝0.3秒おき）」）。</summary>
        public static string Describe(EnemySpawnEntry entry)
        {
            var start = EnemyWaveStats.StartTime(entry);
            var end = EnemyWaveStats.EndTime(entry);
            var time = end - start < 0.05f ? $"{start:0.0}秒" : $"{start:0.0}〜{end:0.0}秒";
            var place = string.IsNullOrEmpty(entry.spawnPointId) ? "湧き位置なし" : entry.spawnPointId;
            return $"{time}｜{place}｜{TypeName(entry.enemyType)} ×{entry.count}{WaveEntryToolbar.Pace(entry)}";
        }

        public static string TypeName(EnemyTypeDefinition type)
        {
            if (type == null)
            {
                return "（種類なし）";
            }

            return string.IsNullOrEmpty(type.displayName) ? type.name : type.displayName;
        }
    }
}
