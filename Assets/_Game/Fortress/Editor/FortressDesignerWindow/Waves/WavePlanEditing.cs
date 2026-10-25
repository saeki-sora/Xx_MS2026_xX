using System.Collections.Generic;
using System.IO;
using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>ウェーブの計画のファイルを作る・写す（要塞デザイナーの「敵ウェーブ」タブから使う）。</summary>
    public static class WavePlanEditing
    {
        /// <summary>今の時間割から作るときの、ウェーブごとの数の倍率（だんだん多くなる）。</summary>
        public static readonly float[] DefaultScales = { 1f, 1.2f, 1.4f, 1.6f, 2f };

        /// <summary>今の時間割から作るときの、1周の数に対する目標の割合。</summary>
        public const float DefaultTargetRatio = 0.9f;

        /// <summary>計画を置くフォルダの既定（ステージのシーンの名前ごと）。</summary>
        public static string DefaultFolder(EnemySpawnDirector director)
        {
            var scene = director != null ? director.gameObject.scene.name : "";
            return string.IsNullOrEmpty(scene) ? "Assets/Stages/Data" : $"Assets/Stages/Data/{scene}";
        }

        /// <summary>
        /// 新しい計画を作ってディレクターに入れる。from があれば、その時間割を5本に写して数を少しずつ増やす。キャンセルなら null。
        /// </summary>
        public static WavePlan CreateFor(EnemySpawnDirector director, EnemyWaveConfig from)
        {
            var folder = DefaultFolder(director);
            EnsureFolder(folder);
            var scene = director != null ? director.gameObject.scene.name : "Stage";
            var path = EditorUtility.SaveFilePanelInProject("ウェーブの計画を作る", $"WavePlan_{scene}", "asset",
                "ステージ1つに1ファイル（ほかのステージと共有しない）。", folder);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var plan = ScriptableObject.CreateInstance<WavePlan>();
            plan.waves = new List<WaveDefinition>();
            var brushing = DefaultIntervalEvent();
            for (var i = 0; i < WavePlan.DefaultWaveCount; i++)
            {
                var wave = MakeWave(from, i);
                if (i < WavePlan.DefaultWaveCount - 1)
                {
                    wave.intervalEvent = brushing; // 合間は歯みがきタイム（最後のウェーブの後は無し）
                }

                plan.waves.Add(wave);
            }

            plan.display = WaveDisplayPanel.LoadOrCreateDefault();
            plan.memo = from != null ? $"時間割「{from.name}」から作った（数 ×{string.Join("/", DefaultScales)}）。" : "";
            AssetDatabase.CreateAsset(plan, path);
            AssetDatabase.SaveAssets();
            Assign(director, plan);
            return plan;
        }

        /// <summary>合間のイベントの既定（プロジェクトにある歯みがきタイムの設定。無ければ null＝休むだけ）。</summary>
        public static WaveIntervalEvent DefaultIntervalEvent()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BrushingEvent"))
            {
                var found = AssetDatabase.LoadAssetAtPath<BrushingEvent>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>ディレクターに計画を入れる（Ctrl+Z で戻せる。シーンに保存の印）。</summary>
        public static void Assign(EnemySpawnDirector director, WavePlan plan)
        {
            if (director == null)
            {
                return;
            }

            Undo.RecordObject(director, "ウェーブの計画を入れる");
            director.wavePlan = plan;
            EditorUtility.SetDirty(director);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }
        }

        private static WaveDefinition MakeWave(EnemyWaveConfig from, int index)
        {
            var wave = new WaveDefinition { repeatUntilCleared = true, repeatGapSeconds = 3f, restSeconds = 3f };
            if (from == null || from.spawnEntries == null)
            {
                wave.targetKills = 30 + index * 15;
                return wave;
            }

            foreach (var entry in from.spawnEntries)
            {
                if (entry != null)
                {
                    wave.spawnEntries.Add(entry.Clone());
                }
            }

            var scale = DefaultScales[Mathf.Clamp(index, 0, DefaultScales.Length - 1)];
            WaveEntryToolbar.ScaleCounts(wave.spawnEntries, scale);
            wave.repeatGapSeconds = from.loop ? from.loopInterval : 3f;
            wave.targetKills = Mathf.Max(5, Mathf.RoundToInt(EnemyWaveStats.TotalEnemies(wave.spawnEntries) * DefaultTargetRatio));
            return wave;
        }

        /// <summary>計画に1ウェーブ足す（最後のウェーブを写して数を少し増やす）。</summary>
        public static void AddWave(WavePlan plan)
        {
            Undo.RecordObject(plan, "ウェーブを足す");
            var last = plan.Count > 0 ? plan.waves[plan.Count - 1] : null;
            var wave = last != null ? last.Clone() : new WaveDefinition();
            if (last != null)
            {
                wave.label = "";
                wave.announceText = "";
                WaveEntryToolbar.ScaleCounts(wave.spawnEntries, 1.2f);
                wave.targetKills = Mathf.RoundToInt(last.targetKills * 1.2f);

                // 今までの最終ウェーブは、もう最後ではないので後に合間が来る。合間が空なら、ほかのウェーブと同じ歯みがきタイムにする
                // （空のままだと「休む 3秒」になり、気づきにくい）。
                if (last.intervalEvent == null)
                {
                    last.intervalEvent = PreviousIntervalEvent(plan) ?? DefaultIntervalEvent();
                }
            }

            plan.waves.Add(wave);
            EditorUtility.SetDirty(plan);
        }

        // 最終ウェーブより前で、いちばん後ろにある合間のイベント（無ければ null）。
        private static WaveIntervalEvent PreviousIntervalEvent(WavePlan plan)
        {
            for (var i = plan.Count - 2; i >= 0; i--)
            {
                if (plan.waves[i]?.intervalEvent != null)
                {
                    return plan.waves[i].intervalEvent;
                }
            }

            return null;
        }

        /// <summary>index のウェーブを消す。</summary>
        public static void RemoveWave(WavePlan plan, int index)
        {
            if (index < 0 || index >= plan.Count)
            {
                return;
            }

            Undo.RecordObject(plan, "ウェーブを消す");
            plan.waves.RemoveAt(index);
            EditorUtility.SetDirty(plan);
        }

        /// <summary>前のウェーブの中身を写す（数は倍率で変える。目標・名前・合間はそのまま）。</summary>
        public static void CopyFromPrevious(WavePlan plan, int index, float scale)
        {
            if (index <= 0 || index >= plan.Count)
            {
                return;
            }

            Undo.RecordObject(plan, "前のウェーブを写す");
            var source = plan.waves[index - 1];
            var target = plan.waves[index];
            target.spawnEntries.Clear();
            foreach (var entry in source.spawnEntries)
            {
                if (entry != null)
                {
                    target.spawnEntries.Add(entry.Clone());
                }
            }

            WaveEntryToolbar.ScaleCounts(target.spawnEntries, scale);
            target.repeatUntilCleared = source.repeatUntilCleared;
            target.repeatGapSeconds = source.repeatGapSeconds;
            EditorUtility.SetDirty(plan);
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
