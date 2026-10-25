using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの Wave n のページ。上から: きまり（目標・くり返し・名前）→ 見積もり（1周の数・出そろうまでの秒数）
    /// → タイムライン → 湧きの行の表 → 便利ボタン → このウェーブの後の合間。
    /// </summary>
    public sealed class WaveDefinitionPanel
    {
        private const string PrefCopyScale = "MS2026.FortressDesigner.Wave.CopyScale";

        private readonly WaveTimelinePanel _timeline = new WaveTimelinePanel();
        private readonly WaveEntryListPanel _entryList = new WaveEntryListPanel();
        private readonly WaveEntryToolbar _toolbar = new WaveEntryToolbar();

        /// <summary>表で選んでいる行の発生時刻（Play のテストボタン用。無ければ null）。</summary>
        public float? SelectedTime { get; private set; }

        public void Draw(WavePlan plan, SerializedObject serialized, int index, EnemySpawnDirector director, HashSet<string> knownIds, List<WavePlanIssue> issues)
        {
            var wave = plan.Get(index);
            if (wave == null)
            {
                return;
            }

            var final = index == plan.Count - 1;
            var element = serialized.FindProperty($"waves.Array.data[{index}]");
            EditorGUILayout.LabelField($"{wave.DisplayLabel(index + 1)}{(final ? "（最終ウェーブ：目標を倒したらクリア）" : "")}", EditorStyles.boldLabel);
            foreach (var issue in issues.Where(i => i.Wave == index))
            {
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Level));
            }

            DrawRules(element, wave);
            EditorGUILayout.Space(4);
            DrawEstimate(wave);
            WaveRouteSceneOverlay.Target(plan, index, director);
            var routes = EditorGUILayout.ToggleLeft(
                new GUIContent("Scene ビューに、このウェーブの敵のルートを表示（赤い線）", "ゲームの発表で見せる赤い線と同じ計算です。湧き位置や障害物を動かすと線も変わります。オレンジの線はコアまで行けない道。"),
                WaveRouteSceneOverlay.Enabled);
            if (routes != WaveRouteSceneOverlay.Enabled)
            {
                WaveRouteSceneOverlay.Enabled = routes;
            }
            EditorGUILayout.Space(6);

            var target = new WaveEntriesTarget(plan, serialized, element.propertyPath + ".spawnEntries", wave.spawnEntries, wave.DisplayLabel(index + 1));
            var repeat = wave.repeatUntilCleared ? $" ・ 目標まで {wave.repeatGapSeconds:0.#}秒 待ってくり返す" : " ・ くり返さない";
            _timeline.Draw(wave.spawnEntries, repeat, Playhead(director, index), knownIds, _entryList.SelectedIndex, i => _entryList.SelectedIndex = i);
            EditorGUILayout.Space(6);
            _entryList.Draw(target);
            _toolbar.Draw(target, _entryList);
            if (index > 0)
            {
                DrawCopyFromPrevious(plan, serialized, index);
            }

            var selected = _entryList.SelectedIndex;
            SelectedTime = selected >= 0 && selected < wave.spawnEntries.Count ? wave.spawnEntries[selected]?.triggerTime : null;

            EditorGUILayout.Space(8);
            DrawAfter(element, wave, final);
        }

        private static void DrawRules(SerializedProperty element, WaveDefinition wave)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("きまり", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("targetKills"), new GUIContent("目標撃破数", "このウェーブで、この数だけ倒したら次へ進みます（最終ウェーブならクリア）。"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    var repeat = element.FindPropertyRelative("repeatUntilCleared");
                    EditorGUILayout.PropertyField(repeat, new GUIContent("目標までくり返す", "最後の1体を出し終えたら、目標に届くまで最初からくり返します。OFF だと1回だけ。"));
                    using (new EditorGUI.DisabledScope(!repeat.boolValue))
                    {
                        EditorGUILayout.PropertyField(element.FindPropertyRelative("repeatGapSeconds"), new GUIContent("くり返すまで(秒)"));
                    }
                }

                EditorGUILayout.PropertyField(element.FindPropertyRelative("label"), new GUIContent("名前（空なら WAVE n）"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("announceText"), new GUIContent("発表の一言（任意）", "「WAVE 3！目標撃破数 60！」の下に出す一言。例: 「ボス出現！」「横からも来るぞ！」"));
            }
        }

        private static void DrawEstimate(WaveDefinition wave)
        {
            var total = WavePlanChecks.CycleEnemies(wave);
            var seconds = WavePlanChecks.CycleSeconds(wave);
            var rate = WavePlanChecks.EnemiesPerSecond(wave);
            var until = WavePlanChecks.SecondsUntilTargetSpawned(wave);
            var cycles = total > 0 ? (float)wave.targetKills / total : 0f;
            var untilText = until < 0f ? "目標の数が出ません" : $"目標 {wave.targetKills}体 が出そろうまで 約{until:0.#}秒（1周の {cycles:0.##}倍）";
            EditorGUILayout.LabelField(
                new GUIContent($"見積もり: 1周 {total}体 / {seconds:0.#}秒 ・ 1秒に約{rate:0.#}体 ・ {untilText}",
                    "全部すぐ倒せたとしたときの最短です。実際は倒すのにかかる時間とコアに着く敵の分、長くなります。"),
                EditorStyles.wordWrappedMiniLabel);
        }

        private static float? Playhead(EnemySpawnDirector director, int index)
        {
            if (!Application.isPlaying || director == null || !director.IsPlaying)
            {
                return null;
            }

            return FortressWaves.IsActive && FortressWaves.Phase == WavePhase.Combat && FortressWaves.WaveIndex == index
                ? director.ElapsedTime
                : (float?)null;
        }

        private static void DrawCopyFromPrevious(WavePlan plan, SerializedObject serialized, int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var scale = EditorPrefs.GetFloat(PrefCopyScale, 1.2f);
                EditorGUI.BeginChangeCheck();
                scale = EditorGUILayout.FloatField(new GUIContent("写すときの倍率", "右のボタンで前のウェーブの行を写すとき、数をこの倍率にします。"), scale);
                // 0 やマイナスを打つと全部の行が1体になってしまうので、0.1 倍より小さくはしない。
                scale = Mathf.Max(0.1f, scale);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetFloat(PrefCopyScale, scale);
                }

                if (GUILayout.Button(new GUIContent($"前のウェーブの行を写す（×{scale:0.##}）", "このウェーブの行を、前のウェーブの行の写しに置きかえます（目標・名前・合間はそのまま）。"), GUILayout.Width(240)))
                {
                    if (EditorUtility.DisplayDialog("前のウェーブの行を写す", $"このウェーブの行（{plan.waves[index].spawnEntries.Count}行）を消して、前のウェーブの行を数 ×{scale:0.##} で写します。", "写す", "やめる"))
                    {
                        serialized.ApplyModifiedProperties();
                        WavePlanEditing.CopyFromPrevious(plan, index, scale);
                        serialized.Update();
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private static void DrawAfter(SerializedProperty element, WaveDefinition wave, bool final)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("このウェーブの後", EditorStyles.miniBoldLabel);
                if (final)
                {
                    EditorGUILayout.LabelField("最終ウェーブなので、目標を倒したらクリア（合間はありません）。", EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                EditorGUILayout.LabelField("目標を倒すと「WAVE CLEAR!」→ 残りの敵が画面の外へ逃げる → 合間 → 次の発表。", EditorStyles.wordWrappedMiniLabel);
                var ev = element.FindPropertyRelative("intervalEvent");
                EditorGUILayout.PropertyField(ev, new GUIContent("合間のイベント", "歯みがきタイムなど。空なら下の秒数だけ休みます（敵は来ません）。"));
                if (ev.objectReferenceValue == null)
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("restSeconds"), new GUIContent("休む秒数"));
                }
                else if (wave.intervalEvent != null)
                {
                    EditorGUILayout.LabelField($"「{wave.intervalEvent.DisplayName}」 {wave.intervalEvent.DurationSeconds:0.#}秒（秒数・回数・ごほうびは「全体」ページの「合間のイベント」で変えます）", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        public static MessageType ToMessageType(WavePlanIssueLevel level) =>
            level == WavePlanIssueLevel.Error ? MessageType.Error : level == WavePlanIssueLevel.Warning ? MessageType.Warning : MessageType.Info;
    }
}
