using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEditorInternal;
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
        private readonly SpawnPointsPanel _spawnPointsPanel = new SpawnPointsPanel();
        private readonly WaveTimelinePanel _timeline = new WaveTimelinePanel();

        private EnemyWaveConfig _cachedWave;
        private SerializedObject _serializedWave;
        private ReorderableList _entriesList;
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
                BuildEntriesList(_serializedWave);
            }

            _serializedWave.Update();

            EditorGUILayout.PropertyField(_serializedWave.FindProperty("waveName"), new GUIContent("ウェーブ名"));

            EditorGUILayout.Space(6);
            _timeline.Draw(wave, director, KnownSpawnPointIds(), _entriesList.index, index => _entriesList.index = index);
            EditorGUILayout.Space(6);

            _entriesList.DoLayoutList();
            _entryToolbar.Draw(wave, _serializedWave, _entriesList);
            EditorGUILayout.Space(4);

            var loopProp = _serializedWave.FindProperty("loop");
            EditorGUILayout.PropertyField(loopProp, new GUIContent("ループ再生", "最後のエントリの後、最初から繰り返す。"));
            if (loopProp.boolValue)
            {
                EditorGUILayout.PropertyField(
                    _serializedWave.FindProperty("loopInterval"),
                    new GUIContent("ループ間隔(秒)"));
            }

            _serializedWave.ApplyModifiedProperties();

            DrawValidationSummary(wave);
        }

        private void BuildEntriesList(SerializedObject serializedWave)
        {
            var entriesProperty = serializedWave.FindProperty("spawnEntries");
            _entriesList = new ReorderableList(serializedWave, entriesProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "湧きエントリ（発生時刻順に自動再生されます）"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 6f + 18f,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = entriesProperty.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    var lineHeight = EditorGUIUtility.singleLineHeight;
                    var lineSpacing = lineHeight + 2f;

                    // 行の中身を1行で（「12.0秒｜Spawn_0｜スライム ×20（0.3秒おき・6.0秒かけて）」）。
                    var entry = index < _cachedWave.spawnEntries.Count ? _cachedWave.spawnEntries[index] : null;
                    if (entry != null)
                    {
                        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, lineHeight), WaveEntryToolbar.Summary(entry), EditorStyles.miniBoldLabel);
                    }

                    rect.y += lineSpacing;
                    var triggerTimeRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
                    EditorGUI.PropertyField(triggerTimeRect, element.FindPropertyRelative("triggerTime"),
                        new GUIContent("発生時刻(秒)"));

                    var spawnPointRect = new Rect(rect.x, rect.y + lineSpacing, rect.width, lineHeight);
                    EditorGUI.PropertyField(spawnPointRect, element.FindPropertyRelative("spawnPointId"),
                        new GUIContent("湧き位置"));

                    var enemyTypeRect = new Rect(rect.x, rect.y + lineSpacing * 2f, rect.width, lineHeight);
                    EditorGUI.PropertyField(enemyTypeRect, element.FindPropertyRelative("enemyType"),
                        new GUIContent("敵の種類"));

                    var halfWidth = rect.width * 0.5f - 4f;
                    var countRect = new Rect(rect.x, rect.y + lineSpacing * 3f, halfWidth, lineHeight);
                    EditorGUI.PropertyField(countRect, element.FindPropertyRelative("count"), new GUIContent("数"));

                    var intervalRect = new Rect(rect.x + halfWidth + 8f, rect.y + lineSpacing * 3f, halfWidth, lineHeight);
                    EditorGUI.PropertyField(intervalRect, element.FindPropertyRelative("intervalBetweenSpawns"),
                        new GUIContent("間隔(秒)"));
                }
            };
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
