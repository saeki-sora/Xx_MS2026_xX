using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブ。敵の湧く位置・数・タイミングを一覧編集し、湧き位置ごとのタイムライン（WaveTimelinePanel）で俯瞰できる。
    /// </summary>
    public sealed class EnemyWavesTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly WavePlayControls _playControls = new WavePlayControls();
        private readonly SpawnPointsPanel _spawnPointsPanel = new SpawnPointsPanel();
        private readonly WaveTimelinePanel _timeline = new WaveTimelinePanel();

        private EnemyWaveConfig _cachedWave;
        private SerializedObject _serializedWave;
        private readonly WaveEntryListPanel _entryList = new WaveEntryListPanel();
        private readonly WaveEntryToolbar _entryToolbar = new WaveEntryToolbar();

        public EnemyWavesTabView()
        {
            AddToClassList("fd-tab-content");
            _imgui = new IMGUIContainer(OnIMGUI);
            Add(_imgui);
        }

        public void Refresh()
        {
            _imgui.MarkDirtyRepaint();
        }

        private void OnIMGUI()
        {
            EditorGUILayout.LabelField("敵ウェーブ", EditorStyles.boldLabel);

            var director = Object.FindFirstObjectByType<EnemySpawnDirector>();
            if (director == null)
            {
                EditorGUILayout.HelpBox(
                    "シーンに EnemySpawnDirector が見つかりません。「はじめに」タブのテストシーン生成、" +
                    "または手動でGameObjectに EnemySpawnDirector を追加してください。",
                    MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            var wave = (EnemyWaveConfig)EditorGUILayout.ObjectField(
                new GUIContent("編集対象のウェーブ", "EnemySpawnDirectorに割り当てるウェーブアセット。"),
                director.wave, typeof(EnemyWaveConfig), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(director, "Assign Enemy Wave");
                director.wave = wave;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新規ウェーブを作成", GUILayout.Width(160)))
                {
                    var created = CreateNewWaveAsset();
                    if (created != null)
                    {
                        Undo.RecordObject(director, "Assign Enemy Wave");
                        director.wave = created;
                        wave = created;
                    }
                }

                var pointCount = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None).Length;
                EditorGUILayout.LabelField($"シーン内のスポーン地点: {pointCount}箇所", EditorStyles.miniLabel);
            }

            _playControls.Draw(director, wave, SelectedEntryTime(wave));

            EditorGUILayout.Space(6);
            _spawnPointsPanel.Draw(director, wave);

            if (wave == null)
            {
                EditorGUILayout.HelpBox("ウェーブアセットを割り当てるか、新規作成してください。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            DrawWaveEditor(wave, director);
        }

        private float? SelectedEntryTime(EnemyWaveConfig wave)
        {
            var index = _entryList.SelectedIndex;
            if (wave == null || _cachedWave != wave || index < 0 || index >= wave.spawnEntries.Count)
            {
                return null;
            }

            return wave.spawnEntries[index]?.triggerTime;
        }

        private static EnemyWaveConfig CreateNewWaveAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "新しい敵ウェーブを作成", "New EnemyWaveConfig", "asset", "保存先を選択してください。");

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = ScriptableObject.CreateInstance<EnemyWaveConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private void DrawWaveEditor(EnemyWaveConfig wave, EnemySpawnDirector director)
        {
            if (_cachedWave != wave || _serializedWave == null || _serializedWave.targetObject == null)
            {
                _cachedWave = wave;
                _serializedWave = new SerializedObject(wave);
            }

            _serializedWave.Update();
            var target = new WaveEntriesTarget(wave, _serializedWave, "spawnEntries", wave.spawnEntries, wave.waveName);

            EditorGUILayout.PropertyField(_serializedWave.FindProperty("waveName"), new GUIContent("ウェーブ名"));

            EditorGUILayout.Space(6);
            var repeat = wave.loop ? $" ・ そのあと {wave.loopInterval:0.#}秒 待って最初から繰り返す" : " ・ 1回で終わり";
            float? playhead = Application.isPlaying && director != null && director.IsPlaying ? director.ElapsedTime : (float?)null;
            _timeline.Draw(wave.spawnEntries, repeat, playhead, KnownSpawnPointIds(), _entryList.SelectedIndex, index => _entryList.SelectedIndex = index);
            EditorGUILayout.Space(6);

            _entryList.Draw(target);
            _entryToolbar.Draw(target, _entryList);
            EditorGUILayout.Space(4);

            var loopProp = _serializedWave.FindProperty("loop");
            EditorGUILayout.PropertyField(loopProp, new GUIContent("ループ再生", "最後の1体が出た後、待ってから最初から繰り返す。"));
            if (loopProp.boolValue)
            {
                EditorGUILayout.PropertyField(
                    _serializedWave.FindProperty("loopInterval"),
                    new GUIContent("ループ間隔(秒)", "最後の1体が出てから、次の周を始めるまでの秒数。"));
            }

            _serializedWave.ApplyModifiedProperties();

            DrawValidationSummary(wave);
        }

        private static HashSet<string> KnownSpawnPointIds() =>
            Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .Select(sp => sp.ResolvedId)
                .ToHashSet();

        private static void DrawValidationSummary(EnemyWaveConfig wave)
        {
            var knownIds = KnownSpawnPointIds();

            var missingSpawnPoint = wave.spawnEntries.Count(e => !knownIds.Contains(e.spawnPointId));
            var missingEnemyType = wave.spawnEntries.Count(e => e.enemyType == null);

            if (missingSpawnPoint > 0)
            {
                EditorGUILayout.HelpBox($"湧き位置がシーンに見つからないエントリが{missingSpawnPoint}件あります。", MessageType.Warning);
            }

            if (missingEnemyType > 0)
            {
                EditorGUILayout.HelpBox($"敵の種類が未設定のエントリが{missingEnemyType}件あります。", MessageType.Warning);
            }
        }
    }
}
