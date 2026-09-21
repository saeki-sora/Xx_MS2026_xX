using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>Play中の計測表示。敵の数・FPS・処理時間と、直近の処理時間グラフ。</summary>
    public sealed class SwarmLivePanel
    {
        private const float FrameBudgetMs = 1000f / 60f;

        public void Draw(SwarmSystem system)
        {
            EditorGUILayout.LabelField("ライブ計測", EditorStyles.miniBoldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入ると、敵の数・FPS・処理時間がここに表示されます。", MessageType.Info);
                return;
            }

            var stats = system.Stats;
            var fps = stats.frameMs > 0.01f ? 1000f / stats.frameMs : 0f;

            var rect = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, stats.capacity > 0 ? (float)stats.alive / stats.capacity : 0f,
                $"敵 {stats.alive} / {stats.capacity}");

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetric("FPS", $"{fps:0}", fps >= 58f ? Color.green : fps >= 30f ? Color.yellow : Color.red);
                DrawMetric("フレーム", $"{stats.frameMs:0.0} ms", Color.white);
                DrawMetric("メインの待ち", $"{stats.simulationMs:0.00} ms", stats.simulationMs <= 2f ? Color.green : Color.yellow);
                DrawMetric("ジョブ所要", $"{stats.jobLatencyMs:0.0} ms", stats.jobLatencyMs <= 12f ? Color.green : Color.yellow);
                DrawMetric("描画バッチ", stats.drawBatches.ToString(), Color.white);
            }

            EditorGUILayout.LabelField(
                $"累計  湧き {stats.totalSpawned} / 撃破 {stats.totalKilled} / コア到達 {stats.totalArrived}" +
                (stats.rejectedSpawns > 0 ? $" / 上限で拒否 {stats.rejectedSpawns}" : string.Empty),
                EditorStyles.miniLabel);

            DrawGraph("フレーム時間 (ms)  ※水平線=60FPSの目安 16.6ms", system.FrameMsHistory, system.HistoryCursor, 40f);
            DrawGraph("メインスレッドの待ち時間 (ms) ※ジョブの完了待ち", system.SimulationMsHistory, system.HistoryCursor, 10f);
            EditorGUILayout.Space(4);
            DrawStages(system);
            DrawSpikes(system);
            EditorGUILayout.Space(6);
        }

        private static void DrawStages(SwarmSystem system)
        {
            if (!system.detailedProfiling)
            {
                EditorGUILayout.HelpBox(
                    "「詳細計測」をONにすると、処理の段階ごとの時間と、最も混んだ場所の密度が分かります（原因調査用）。",
                    MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("処理段階ごとの時間 (ms, 平滑化)", EditorStyles.miniBoldLabel);
            for (var i = 0; i < SwarmSystem.StageNames.Length; i++)
            {
                var ms = system.StageMs[i];
                var rect = GUILayoutUtility.GetRect(10, 16, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, Mathf.Clamp01(ms / 4f), $"{SwarmSystem.StageNames[i]}  {ms:0.00} ms");
            }

            var stats = system.Stats;
            EditorGUILayout.LabelField(
                $"最も混んだセルの敵数 {stats.maxCellCount}体 / 近傍チェック回数の概算 {stats.pairCheckEstimate / 1000f:0}k回/段階",
                EditorStyles.miniLabel);
            if (stats.maxCellCount > 40)
            {
                EditorGUILayout.HelpBox(
                    "1つのセルに敵が集中しています。密集地帯では近傍チェックの回数が敵数の2乗に近づくため、処理時間が急増します。",
                    MessageType.Warning);
            }
        }

        private static void DrawSpikes(SwarmSystem system)
        {
            var spikes = system.RecentSpikes;
            EditorGUILayout.LabelField(
                $"スパイク記録（{system.spikeThresholdMs:0}ms超のフレーム / 直近{spikes.Count}件）", EditorStyles.miniBoldLabel);

            if (spikes.Count == 0)
            {
                EditorGUILayout.LabelField("まだありません。", EditorStyles.miniLabel);
                return;
            }

            for (var i = spikes.Count - 1; i >= Mathf.Max(0, spikes.Count - 6); i--)
            {
                var spike = spikes[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"{spike.time:0.0}秒  {spike.frameMs:0.0} ms  →  {Diagnose(spike)}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"群衆内: 経路 {spike.navigationMs:0.00} / 処理 {spike.simulationMs:0.00} / 描画 {spike.renderMs:0.00} ms" +
                        $"   群衆の外: {spike.OtherMs:0.0} ms   敵 {spike.alive}体" +
                        (spike.gcOccurred ? "   [GC発生]" : string.Empty) +
                        (spike.navigationRebuilt ? "   [経路を再計算]" : string.Empty),
                        EditorStyles.miniLabel);
                }
            }

            if (GUILayout.Button("スパイク記録をクリア"))
            {
                system.ClearSpikes();
            }
        }

        /// <summary>スパイクの内訳から、最も疑わしい原因を文章にする。</summary>
        private static string Diagnose(SwarmSpike spike)
        {
            var inside = spike.navigationMs + spike.simulationMs + spike.renderMs;
            if (spike.gcOccurred && spike.OtherMs > inside)
            {
                return "GC（ガベージコレクション）";
            }

            if (spike.OtherMs > inside * 1.5f && spike.OtherMs > 6f)
            {
                return "群衆システムの外（エディタの描画・他スクリプト・GPU待ちなど）";
            }

            if (spike.navigationMs > spike.simulationMs && spike.navigationMs > spike.renderMs)
            {
                return spike.navigationRebuilt ? "経路の再計算（障害物の破壊・再生）" : "経路データの更新";
            }

            if (spike.simulationMs >= spike.renderMs)
            {
                return "群衆のシミュレーション（密集）";
            }

            return "描画データの準備";
        }

        private static void DrawMetric(string label, string value, Color color)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                var previous = GUI.contentColor;
                GUI.contentColor = color;
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
                GUI.contentColor = previous;
            }
        }

        private static void DrawGraph(string title, float[] history, int cursor, float maxMs)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
            var rect = GUILayoutUtility.GetRect(10, 54, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));

            var barWidth = rect.width / history.Length;
            for (var i = 0; i < history.Length; i++)
            {
                var value = history[(cursor + i) % history.Length];
                var height = Mathf.Clamp01(value / maxMs) * rect.height;
                var color = value <= FrameBudgetMs ? new Color(0.35f, 0.85f, 0.45f)
                    : value <= FrameBudgetMs * 2f ? new Color(0.95f, 0.8f, 0.25f)
                    : new Color(0.95f, 0.35f, 0.3f);
                EditorGUI.DrawRect(new Rect(rect.x + i * barWidth, rect.yMax - height, Mathf.Max(1f, barWidth - 0.5f), height), color);
            }

            var budgetY = rect.yMax - Mathf.Clamp01(FrameBudgetMs / maxMs) * rect.height;
            EditorGUI.DrawRect(new Rect(rect.x, budgetY, rect.width, 1f), new Color(1f, 1f, 1f, 0.5f));
        }
    }
}
