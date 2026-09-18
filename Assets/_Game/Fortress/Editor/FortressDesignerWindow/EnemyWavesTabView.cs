using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブ。敵の湧く位置・数・タイミングを一覧編集し、簡易タイムラインで俯瞰できる。
    /// </summary>
    public sealed class EnemyWavesTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;

        private EnemyWaveConfig _cachedWave;
        private SerializedObject _serializedWave;
        private ReorderableList _entriesList;

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

            if (wave == null)
            {
                EditorGUILayout.HelpBox("ウェーブアセットを割り当てるか、新規作成してください。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            DrawWaveEditor(wave);
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

        private void DrawWaveEditor(EnemyWaveConfig wave)
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
            DrawTimeline(wave);
            EditorGUILayout.Space(6);

            _entriesList.DoLayoutList();

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
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 5f + 16f,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = entriesProperty.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    var lineHeight = EditorGUIUtility.singleLineHeight;
                    var lineSpacing = lineHeight + 2f;

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

        private static void DrawTimeline(EnemyWaveConfig wave)
        {
            if (wave.spawnEntries == null || wave.spawnEntries.Count == 0)
            {
                return;
            }

            var maxTime = Mathf.Max(1f, wave.spawnEntries.Max(e => e.triggerTime));
            var rect = GUILayoutUtility.GetRect(10, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));

            foreach (var entry in wave.spawnEntries)
            {
                var t = Mathf.Clamp01(entry.triggerTime / maxTime);
                var x = rect.x + t * (rect.width - 6f);
                var tickRect = new Rect(x, rect.y + 3f, 6f, rect.height - 6f);
                EditorGUI.DrawRect(tickRect, new Color(1f, 0.55f, 0.15f, 0.9f));
            }

            var labelRect = new Rect(rect.x, rect.y + rect.height, rect.width, 16f);
            EditorGUI.LabelField(labelRect, "0秒", EditorStyles.miniLabel);
            var endLabelRect = new Rect(rect.x + rect.width - 60f, rect.y + rect.height, 60f, 16f);
            EditorGUI.LabelField(endLabelRect, $"{maxTime:0.0}秒", EditorStyles.miniLabel);
            GUILayout.Space(16f);
        }

        private static void DrawValidationSummary(EnemyWaveConfig wave)
        {
            var knownIds = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .Select(sp => sp.ResolvedId)
                .ToHashSet();

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
