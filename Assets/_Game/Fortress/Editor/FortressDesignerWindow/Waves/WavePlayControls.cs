using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの再生の欄。編集中は「Play したらすぐ再生する」の切り替えと、Play 中に変えた物が残るかの説明。
    /// Play 中は今の状態と「最初から再生」「選んだ行から再生」「止める」（テストタブへ行かなくても試せる）。
    /// </summary>
    public sealed class WavePlayControls
    {
        /// <summary>
        /// ウェーブの計画があるときの欄。Play 中は今の段階・倒した数・残り時間と、テスト用のボタン
        /// （選んでいるウェーブから・今の段階を飛ばす）。Host／オフラインだけ効く。
        /// </summary>
        /// <param name="selectedWave">開いているタブのウェーブ（全体なら −1）。</param>
        public void DrawPlan(WavePlan plan, int selectedWave)
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    "Play すると、全員の準備（幕が開く）がそろってから WAVE 1 の発表が始まります。計画の中身は Play 中に変えても残ります（次に始めたウェーブから反映）。",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!FortressWaves.IsActive)
                {
                    EditorGUILayout.LabelField("ウェーブ（Play 中）: 動いていません（試合の外、またはこのステージに計画がありません）", EditorStyles.boldLabel);
                    return;
                }

                var time = FortressWaves.TimeLeft >= 0f ? $" ・ 残り {WavePlanOverviewPanel.Clock(FortressWaves.TimeLeft)}" : "";
                EditorGUILayout.LabelField(
                    $"ウェーブ（Play 中）: {PhaseName(FortressWaves.Phase)} ・ WAVE {FortressWaves.WaveNumber}/{FortressWaves.WaveCount} ・ 倒した {FortressWaves.KillsInWave}/{FortressWaves.TargetKills}{time}",
                    EditorStyles.boldLabel);
                var canControl = Net.FortressNet.HasSimulationAuthority;
                using (new EditorGUI.DisabledScope(!canControl))
                using (new EditorGUILayout.HorizontalScope())
                {
                    var target = Mathf.Clamp(selectedWave, 0, Mathf.Max(0, plan.Count - 1));
                    if (GUILayout.Button(new GUIContent($"▶ WAVE {target + 1} の発表から", "開いているタブのウェーブから始め直します（全体タブなら WAVE 1）。今いる敵はそのまま。残り時間は戻りません。")))
                    {
                        FortressWaves.DebugJumpTo(target);
                    }

                    var skip = FortressWaves.Phase == WavePhase.Combat ? "目標を倒したことにする" : "今の段階を飛ばす";
                    if (GUILayout.Button(new GUIContent(skip, "発表・撤退・合間をすぐ終わらせます。戦っている間なら、目標を倒したことにして次へ進みます。")))
                    {
                        FortressWaves.DebugSkip();
                    }
                }

                if (!canControl)
                {
                    EditorGUILayout.LabelField("ネット対戦の Client では動かせません（Host で押してください）。", EditorStyles.miniLabel);
                }
            }
        }

        public static string PhaseName(WavePhase phase)
        {
            switch (phase)
            {
                case WavePhase.WaitingForPlayers: return "全員の準備を待っている";
                case WavePhase.Announce: return "発表中";
                case WavePhase.Combat: return "戦っている";
                case WavePhase.Retreat: return "クリア・敵が逃げている";
                case WavePhase.Break: return "合間";
                case WavePhase.Finished: return "終わり";
                default: return "—";
            }
        }

        /// <param name="selectedTime">表で選んでいる行の発生時刻（選んでいなければ null）。</param>
        public void Draw(EnemySpawnDirector director, EnemyWaveConfig wave, float? selectedTime)
        {
            if (!Application.isPlaying)
            {
                EditorGUI.BeginChangeCheck();
                var auto = EditorGUILayout.ToggleLeft(
                    new GUIContent("Play したらすぐウェーブを再生する", "OFF にすると、Play しても敵は湧きません。下の「最初から再生」などで好きなときに始められます（EnemySpawnDirector の autoStartOnPlay）。"),
                    director.autoStartOnPlay);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(director, "ウェーブの自動再生を切り替え");
                    director.autoStartOnPlay = auto;
                    EditorUtility.SetDirty(director);
                }

                EditorGUILayout.LabelField(
                    "表の中身（ウェーブの設定ファイル）は Play 中に変えても残ります。湧き位置（シーンの物）を Play 中に動かした分は、止めると戻ります。",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var state = director.IsPlaying ? $"再生中 {director.ElapsedTime:0.0}秒" : "止まっています";
                EditorGUILayout.LabelField($"ウェーブの再生（Play 中）: {state}", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(wave == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("▶ 最初から再生", "ウェーブを 0 秒から再生し直します（今いる敵はそのまま）。")))
                    {
                        director.StopWave();
                        director.StartWave();
                    }

                    using (new EditorGUI.DisabledScope(selectedTime == null))
                    {
                        var label = selectedTime != null ? $"▶ {selectedTime.Value:0.#}秒（選んだ行）から" : "▶ 選んだ行から";
                        if (GUILayout.Button(new GUIContent(label, "表で選んでいる行の時刻から再生します。それより前の行は飛ばすので、後半の場面だけをすぐ試せます。")))
                        {
                            director.StopWave();
                            director.StartWaveFrom(selectedTime ?? 0f);
                        }
                    }

                    if (GUILayout.Button(new GUIContent("■ 止める", "ウェーブを止めます（1体ずつ湧いている途中の行も止まります。今いる敵はそのまま）。")))
                    {
                        director.StopWave();
                    }
                }

                EditorGUILayout.LabelField("表の数・時刻を変えたら、もう一度「再生」で試せます（変えた中身は Play を止めても残ります）。", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
