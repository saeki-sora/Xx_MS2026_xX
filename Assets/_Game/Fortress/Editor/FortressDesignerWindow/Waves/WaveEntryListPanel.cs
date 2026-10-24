using MS2026.Fortress;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 湧きの行の表。行は1段（「12.0秒｜Spawn_0｜スピキ ×40（4.0秒かけて＝0.10秒おき）」）だけを出し、
    /// 選んだ行だけが開いて編集できる（以前は全部の行が6段ずつあり、10行で画面が埋まった）。
    /// 出し方は「かける秒数で決める」と「間隔で決める」を選べ、どちらを打っても、もう片方を横に自動で出す。
    /// </summary>
    public sealed class WaveEntryListPanel
    {
        private const int OpenLines = 5;

        private WaveEntriesTarget _target;
        private ReorderableList _list;

        /// <summary>選んでいる行（無ければ −1）。</summary>
        public int SelectedIndex
        {
            get => _list != null ? _list.index : -1;
            set
            {
                if (_list != null)
                {
                    _list.index = value;
                }
            }
        }

        public ReorderableList List => _list;

        public void Draw(WaveEntriesTarget target)
        {
            if (_list == null || !target.SameAs(_target))
            {
                var keep = _target != null && _target.Owner == target.Owner && _target.PropertyPath == target.PropertyPath ? SelectedIndex : -1;
                _target = target;
                Build(target);
                SelectedIndex = keep;
            }

            _list.serializedProperty = target.Property;
            _list.DoLayoutList();
        }

        private void Build(WaveEntriesTarget target)
        {
            _list = new ReorderableList(target.Serialized, target.Property, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect,
                    new GUIContent("湧きの行（クリックで開いて編集・発生時刻の順に自動で出ます）", "行を選ぶと、その行だけが開いて編集できます。並び順は自由です（出る順番は発生時刻で決まります）。")),
                elementHeightCallback = index => Line * (index == _list.index ? OpenLines : 1) + 4f,
                drawElementCallback = DrawRow,
                onAddCallback = list => Add(),
            };
        }

        private static float Line => EditorGUIUtility.singleLineHeight + 2f;

        private void Add()
        {
            var entries = _target.List;
            var last = entries.Count > 0 ? entries[entries.Count - 1] : null;
            _target.Modify("行を足す", () =>
            {
                var entry = last != null
                    ? last.Clone()
                    : new EnemySpawnEntry { count = 10, timing = EnemySpawnTiming.Duration, durationSeconds = 3f };
                if (last != null)
                {
                    entry.triggerTime = EnemyWaveStats.EndTime(last) + 1f;
                }

                entries.Add(entry);
            });
            _list.index = entries.Count - 1;
        }

        private void DrawRow(Rect rect, int index, bool active, bool focused)
        {
            var property = _list.serializedProperty;
            if (index >= property.arraySize)
            {
                return;
            }

            var element = property.GetArrayElementAtIndex(index);
            var entry = index < _target.List.Count ? _target.List[index] : null;
            rect.y += 2f;
            var h = EditorGUIUtility.singleLineHeight;
            var row = new Rect(rect.x, rect.y, rect.width, h);
            var open = index == _list.index;

            if (entry != null)
            {
                EditorGUI.LabelField(row, (open ? "▾ " : "▸ ") + WaveEntryToolbar.Summary(entry), open ? EditorStyles.boldLabel : EditorStyles.label);
            }

            if (!open)
            {
                return;
            }

            var half = rect.width * 0.5f - 4f;
            var old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 84f;
            try
            {
                row.y += Line;
                EditorGUI.PropertyField(new Rect(row.x, row.y, half, h), element.FindPropertyRelative("triggerTime"),
                    new GUIContent("発生時刻(秒)", "ウェーブが始まってから何秒後に、この行の1体目を出すか。"));
                EditorGUI.PropertyField(new Rect(row.x + half + 8f, row.y, half, h), element.FindPropertyRelative("spawnPointId"),
                    new GUIContent("湧き位置"));

                row.y += Line;
                EditorGUI.PropertyField(row, element.FindPropertyRelative("enemyType"), new GUIContent("敵の種類"));

                row.y += Line;
                var count = element.FindPropertyRelative("count");
                var timing = element.FindPropertyRelative("timing");
                EditorGUI.PropertyField(new Rect(row.x, row.y, half, h), count, new GUIContent("数", "この行で出す数。"));
                EditorGUI.BeginChangeCheck();
                var mode = (EnemySpawnTiming)EditorGUI.EnumPopup(new Rect(row.x + half + 8f, row.y, half, h),
                    new GUIContent("出し方", "「かける秒数」: 全部を出し終えるまでの秒数を決めると、間隔は自動。「間隔」: 1体ずつの間隔を決める（今までの決め方）。"),
                    (EnemySpawnTiming)timing.enumValueIndex);
                if (EditorGUI.EndChangeCheck())
                {
                    SwitchTiming(element, mode);
                }

                row.y += Line;
                var valueRect = new Rect(row.x, row.y, half, h);
                var noteRect = new Rect(row.x + half + 8f, row.y, half, h);
                if (mode == EnemySpawnTiming.Duration)
                {
                    EditorGUI.PropertyField(valueRect, element.FindPropertyRelative("durationSeconds"),
                        new GUIContent("かける秒数", "1体目から最後の1体までの秒数。数を変えても、この秒数のまま間隔が変わります。0なら全部同時。"));
                    EditorGUI.LabelField(noteRect, entry == null ? "" : IntervalNote(entry), EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUI.PropertyField(valueRect, element.FindPropertyRelative("intervalBetweenSpawns"),
                        new GUIContent("間隔(秒)", "1体ずつの間隔。0なら全部同時。"));
                    EditorGUI.LabelField(noteRect, entry == null ? "" : DurationNote(entry), EditorStyles.miniLabel);
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = old;
            }
        }

        // 出し方を切り替えても、今の出方（かかる秒数）が変わらないように、もう片方の値を写す。
        private static void SwitchTiming(SerializedProperty element, EnemySpawnTiming mode)
        {
            var count = Mathf.Max(1, element.FindPropertyRelative("count").intValue);
            var interval = element.FindPropertyRelative("intervalBetweenSpawns");
            var duration = element.FindPropertyRelative("durationSeconds");
            if (mode == EnemySpawnTiming.Duration)
            {
                duration.floatValue = Mathf.Max(0f, interval.floatValue) * (count - 1);
            }
            else if (count > 1)
            {
                interval.floatValue = Mathf.Max(0f, duration.floatValue) / (count - 1);
            }

            element.FindPropertyRelative("timing").enumValueIndex = (int)mode;
        }

        private static string IntervalNote(EnemySpawnEntry entry)
        {
            if (entry.count <= 1)
            {
                return "＝1体だけ";
            }

            var interval = EnemyWaveStats.Interval(entry);
            return interval <= 0f ? "＝全部同時" : $"＝{interval:0.###}秒おき（1秒に{1f / interval:0.#}体）";
        }

        private static string DurationNote(EnemySpawnEntry entry)
        {
            if (entry.count <= 1)
            {
                return "＝1体だけ";
            }

            var duration = EnemyWaveStats.Duration(entry);
            return duration <= 0f ? "＝全部同時" : $"＝{duration:0.0#}秒かけて";
        }
    }
}
