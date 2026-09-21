using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「群衆」タブ。数千体の敵を動かす群衆システムの計測・ストレステスト・設定（流体らしさ）・敵の種類を管理する。
    /// </summary>
    public sealed class SwarmTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly SwarmLivePanel _live = new SwarmLivePanel();
        private readonly SwarmStressPanel _stress = new SwarmStressPanel();
        private readonly SwarmSettingsPanel _settings = new SwarmSettingsPanel();
        private readonly SwarmTypesPanel _types = new SwarmTypesPanel();

        public SwarmTabView()
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
            EditorGUILayout.LabelField("群衆（数千体の敵）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "敵を配列でまとめてシミュレーション・一括描画して、数千体を60FPSで動かします。" +
                "敵同士が押し合い、細い通路で詰まり、隙間から流れ出す動きは、下の設定で調整できます。",
                MessageType.None);

            var system = Object.FindFirstObjectByType<SwarmSystem>();
            if (system == null)
            {
                EditorGUILayout.HelpBox("シーンに SwarmSystem がありません。", MessageType.Warning);
                if (GUILayout.Button("群衆システムを作成"))
                {
                    SwarmTools.CreateSystem();
                }

                _types.Draw();
                return;
            }

            DrawSystemOptions(system);
            _live.Draw(system);
            _stress.Draw(system);
            EditorGUILayout.Space(6);
            DrawDebugOverlay();
            _settings.Draw(system);
            _types.Draw();
        }

        private static void DrawSystemOptions(SwarmSystem system)
        {
            EditorGUI.BeginChangeCheck();
            var showHud = EditorGUILayout.ToggleLeft(
                new GUIContent("ゲーム画面にHUD（敵数・FPS・処理時間）を表示", "実機やビルドでも負荷を確認できます。"),
                system.showHud);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(system, "Toggle Swarm HUD");
                system.showHud = showHud;
                EditorUtility.SetDirty(system);
            }

            EditorGUI.BeginChangeCheck();
            var detailed = EditorGUILayout.ToggleLeft(
                new GUIContent("詳細計測（原因調査用・少し重くなる）",
                    "処理の段階ごとの時間と、最も混んだセルの密度を計測します。"),
                system.detailedProfiling);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(system, "Toggle Swarm Detailed Profiling");
                system.detailedProfiling = detailed;
                EditorUtility.SetDirty(system);
            }

            EditorGUILayout.Space(4);
        }

        private static void DrawDebugOverlay()
        {
            EditorGUILayout.LabelField("Sceneでのデバッグ表示（Play中）", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                SwarmSceneOverlay.ShowDensity = EditorGUILayout.ToggleLeft(
                    new GUIContent("密度ヒートマップ", "敵が集まっている場所ほど赤くなります。渋滞・押し合いの確認用。"),
                    SwarmSceneOverlay.ShowDensity, GUILayout.Width(140));
                SwarmSceneOverlay.ShowHashGrid = EditorGUILayout.ToggleLeft(
                    new GUIContent("空間ハッシュの格子", "近傍探索用の格子を表示します。"),
                    SwarmSceneOverlay.ShowHashGrid, GUILayout.Width(140));
            }

            if (SwarmSceneOverlay.ShowDensity)
            {
                SwarmSceneOverlay.DensityReference = EditorGUILayout.Slider(
                    new GUIContent("最も赤くなる密度（1セルの敵数）"), SwarmSceneOverlay.DensityReference, 1f, 20f);
            }

            EditorGUILayout.Space(6);
        }
    }
}
