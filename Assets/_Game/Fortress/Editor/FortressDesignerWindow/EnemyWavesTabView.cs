using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブ。ステージのウェーブの計画（<see cref="WavePlan"/>）を「全体」「WAVE 1〜n」「湧き位置」のページで編集する。
    /// 計画の無いステージは、今までどおり時間割1本（<see cref="EnemyWaveConfig"/>）を編集する。
    /// 中身はページごとの部品（<see cref="WavePlanOverviewPanel"/>、<see cref="WaveDefinitionPanel"/>、<see cref="SpawnPointsPanel"/>）が描く。
    /// </summary>
    public sealed class EnemyWavesTabView : VisualElement
    {
        private const string PrefPage = "MS2026.FortressDesigner.Wave.Page";

        /// <summary>ページ: 全体。</summary>
        private const int OverviewPage = -1;

        /// <summary>ページ: 湧き位置。</summary>
        private const int SpawnPointsPage = -2;

        private readonly IMGUIContainer _imgui;
        private readonly WavePlayControls _playControls = new WavePlayControls();
        private readonly SpawnPointsPanel _spawnPointsPanel = new SpawnPointsPanel();
        private readonly WaveTimelinePanel _timeline = new WaveTimelinePanel();
        private readonly WavePlanOverviewPanel _overview = new WavePlanOverviewPanel();
        private readonly WaveDefinitionPanel _wavePanel = new WaveDefinitionPanel();

        private EnemyWaveConfig _cachedWave;
        private SerializedObject _serializedWave;
        private readonly WaveEntryListPanel _entryList = new WaveEntryListPanel();
        private readonly WaveEntryToolbar _entryToolbar = new WaveEntryToolbar();

        private WavePlan _cachedPlan;
        private SerializedObject _serializedPlan;

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

        private static int Page
        {
            get => SessionState.GetInt(PrefPage, OverviewPage);
            set => SessionState.SetInt(PrefPage, value);
        }

        private void OnIMGUI()
        {
            EditorGUILayout.LabelField("敵ウェーブ", EditorStyles.boldLabel);

            var director = FindDirector();
            if (director == null)
            {
                EditorGUILayout.HelpBox(
                    "シーンに EnemySpawnDirector が見つかりません。ステージのシーン（Assets/Scenes/Stages/）を開いてください。" +
                    "（新しく作るなら「はじめに」タブのテストシーン生成、または GameObject に EnemySpawnDirector を追加）",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"ステージ: {director.gameObject.scene.name}", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            var plan = (WavePlan)EditorGUILayout.ObjectField(
                new GUIContent("ウェーブの計画", "このステージの WAVE 1〜n の計画（1ステージ1ファイル）。空なら下の時間割1本をくり返す遊び方。"),
                director.wavePlan, typeof(WavePlan), false);
            if (EditorGUI.EndChangeCheck())
            {
                WavePlanEditing.Assign(director, plan);
            }

            if (plan == null)
            {
                WaveRouteSceneOverlay.Clear();
                DrawNoPlan(director);
                return;
            }

            DrawPlan(director, plan);
        }

        // ステージの EnemySpawnDirector（作業中のシーン＝アクティブなシーンの物を優先、次に計画を持っている物）。
        private static EnemySpawnDirector FindDirector()
        {
            var all = Object.FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None);
            if (all.Length == 0)
            {
                return null;
            }

            var active = SceneManager.GetActiveScene();
            return all.FirstOrDefault(d => d.gameObject.scene == active)
                   ?? all.FirstOrDefault(d => d.wavePlan != null)
                   ?? all[0];
        }

        // ───────── 計画あり ─────────

        private void DrawPlan(EnemySpawnDirector director, WavePlan plan)
        {
            if (_cachedPlan != plan || _serializedPlan == null || _serializedPlan.targetObject == null)
            {
                _cachedPlan = plan;
                _serializedPlan = new SerializedObject(plan);
            }

            _serializedPlan.Update();
            var knownIds = KnownSpawnPointIds();
            var issues = WavePlanChecks.Check(plan, knownIds);

            var page = Page;
            if (page >= plan.Count)
            {
                page = plan.Count - 1;
            }

            _playControls.DrawPlan(plan, page);
            EditorGUILayout.Space(4);
            page = DrawPageBar(plan, issues, page);
            Page = page;
            EditorGUILayout.Space(4);

            if (page < 0 || page >= plan.Count)
            {
                WaveRouteSceneOverlay.Clear();
            }

            if (page == OverviewPage)
            {
                _overview.Draw(plan, _serializedPlan, issues, i => Page = i);
            }
            else if (page == SpawnPointsPage)
            {
                _spawnPointsPanel.Draw(director);
            }
            else if (page >= 0 && page < plan.Count)
            {
                _wavePanel.Draw(plan, _serializedPlan, page, director, knownIds, issues);
            }

            _serializedPlan.ApplyModifiedProperties();
        }

        // 「全体」「WAVE 1」…「湧き位置」のページの切り替え。問題のあるウェーブには ⚠、最終ウェーブには ★。
        private int DrawPageBar(WavePlan plan, List<WavePlanIssue> issues, int page)
        {
            var labels = new List<string> { "全体" };
            for (var i = 0; i < plan.Count; i++)
            {
                var mark = issues.Any(x => x.Wave == i && x.Level == WavePlanIssueLevel.Error) ? " ✖"
                    : issues.Any(x => x.Wave == i && x.Level == WavePlanIssueLevel.Warning) ? " ⚠" : "";
                labels.Add($"WAVE {i + 1}{(i == plan.Count - 1 ? "★" : "")}{mark}");
            }

            labels.Add("湧き位置");
            var selected = page == OverviewPage ? 0 : page == SpawnPointsPage ? labels.Count - 1 : page + 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                selected = GUILayout.Toolbar(selected, labels.ToArray(), GUILayout.Height(24));
                if (GUILayout.Button(new GUIContent("＋", "最後のウェーブを写して、ウェーブを1つ足します（数は1.2倍）。"), GUILayout.Width(26), GUILayout.Height(24)))
                {
                    _serializedPlan.ApplyModifiedProperties();
                    WavePlanEditing.AddWave(plan);
                    _serializedPlan.Update();
                    return plan.Count - 1;
                }

                using (new EditorGUI.DisabledScope(page < 0 || plan.Count <= 1))
                {
                    if (GUILayout.Button(new GUIContent("−", "開いているウェーブを消します。"), GUILayout.Width(26), GUILayout.Height(24)) &&
                        EditorUtility.DisplayDialog("ウェーブを消す", $"WAVE {page + 1} を消します（Ctrl+Z で戻せます）。", "消す", "やめる"))
                    {
                        _serializedPlan.ApplyModifiedProperties();
                        WavePlanEditing.RemoveWave(plan, page);
                        _serializedPlan.Update();
                        return Mathf.Min(page, plan.Count - 1);
                    }
                }
            }

            return selected == 0 ? OverviewPage : selected == labels.Count - 1 ? SpawnPointsPage : selected - 1;
        }

        // ───────── 計画なし（今までの時間割1本） ─────────

        private void DrawNoPlan(EnemySpawnDirector director)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "このステージにはウェーブの計画がありません。今は「時間割1本をくり返し、撃破数（試合の決まり）でクリア」で動きます。" +
                    "WAVE 1〜5・目標撃破数・制限時間で遊ぶには、計画を作ってください。",
                    EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("今の時間割から作る（5ウェーブ）", "下の時間割を5本に写して、数を ×1/1.2/1.4/1.6/2 にした計画を作ります。目標は1周の数の9割。")))
                    {
                        WavePlanEditing.CreateFor(director, director.wave);
                        Page = OverviewPage;
                    }

                    if (GUILayout.Button(new GUIContent("空の計画を作る（5ウェーブ）", "行の無い5ウェーブの計画を作ります。")))
                    {
                        WavePlanEditing.CreateFor(director, null);
                        Page = OverviewPage;
                    }
                }
            }

            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();
            var wave = (EnemyWaveConfig)EditorGUILayout.ObjectField(
                new GUIContent("時間割（計画が無いとき）", "EnemySpawnDirectorに割り当てるウェーブアセット。"),
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
            _spawnPointsPanel.Draw(director);

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
