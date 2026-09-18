using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「テスト」タブ。Play Mode中に握力・熱ゲージ・砲台の状態・ウェーブ再生をリアルタイムに確認する。
    /// </summary>
    public sealed class TestTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;

        public TestTabView()
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
            EditorGUILayout.LabelField("テスト / デバッグ", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入ると、握力・熱ゲージ・砲台の状態をリアルタイムに確認できます。", MessageType.Info);

                if (GUILayout.Button("Grip Input Bridgeシミュレータを開く"))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Grip Input Bridge/Open Window");
                }

                return;
            }

            DrawGripAndTurretStatus();
            EditorGUILayout.Space(10);
            DrawWaveControls();
        }

        private static void DrawGripAndTurretStatus()
        {
            EditorGUILayout.LabelField("砲台の状態", EditorStyles.boldLabel);

            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None)
                .OrderBy(t => t.playerIndex)
                .ToArray();

            if (turrets.Length == 0)
            {
                EditorGUILayout.HelpBox("シーンにLaserTurretが見つかりません。", MessageType.Warning);
                return;
            }

            var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;

            foreach (var turret in turrets)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var color = FortressColors.PlayerColor(turret.playerIndex);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var prevColor = GUI.color;
                        GUI.color = color;
                        GUILayout.Box(GUIContent.none, GUILayout.Width(14), GUILayout.Height(14));
                        GUI.color = prevColor;

                        EditorGUILayout.LabelField($"Player {turret.playerIndex}", GUILayout.Width(70));
                        EditorGUILayout.LabelField(turret.State.ToString(), GUILayout.Width(90));

                        var grip = provider.GetGripValue(turret.playerIndex);
                        EditorGUILayout.LabelField($"握力 {grip:0.00}", GUILayout.Width(90));
                    }

                    var gripRect = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(gripRect, provider.GetGripValue(turret.playerIndex), "握力");

                    var heatRect = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
                    var prevBg = GUI.color;
                    GUI.color = Color.Lerp(Color.white, Color.red, turret.HeatRatio01);
                    EditorGUI.ProgressBar(heatRect, turret.HeatRatio01, "熱");
                    GUI.color = prevBg;
                }
            }
        }

        private static void DrawWaveControls()
        {
            EditorGUILayout.LabelField("敵ウェーブ再生", EditorStyles.boldLabel);

            var director = Object.FindFirstObjectByType<EnemySpawnDirector>();
            if (director == null)
            {
                EditorGUILayout.HelpBox("シーンにEnemySpawnDirectorが見つかりません。", MessageType.Warning);
                return;
            }

            var waveLabel = director.wave != null ? director.wave.waveName : "(ウェーブ未割り当て)";
            EditorGUILayout.LabelField("対象ウェーブ", waveLabel);
            EditorGUILayout.LabelField("状態", director.IsPlaying
                ? $"再生中 ({director.ElapsedTime:0.0}秒経過)"
                : "停止中");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(director.IsPlaying || director.wave == null))
                {
                    if (GUILayout.Button("ウェーブ開始"))
                    {
                        director.StartWave();
                    }
                }

                using (new EditorGUI.DisabledScope(!director.IsPlaying))
                {
                    if (GUILayout.Button("停止"))
                    {
                        director.StopWave();
                    }
                }
            }

            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("コアクリスタル HP", $"{core.CurrentHealth:0} / {core.maxHealth:0}");
            }
        }
    }
}
