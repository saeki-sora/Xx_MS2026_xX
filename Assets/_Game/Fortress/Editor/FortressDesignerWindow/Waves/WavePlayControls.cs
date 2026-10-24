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
