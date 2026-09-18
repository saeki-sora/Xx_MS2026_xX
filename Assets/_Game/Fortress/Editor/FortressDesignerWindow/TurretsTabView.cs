using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>「砲台配置」タブ。シーン内の砲台一覧と、共有チューニング設定の即時編集を提供する。</summary>
    public sealed class TurretsTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;

        public TurretsTabView()
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
            EditorGUILayout.LabelField("シーン内の砲台", EditorStyles.boldLabel);

            var controller = Object.FindFirstObjectByType<FortressLayoutController>();
            if (controller == null)
            {
                EditorGUILayout.HelpBox(
                    "シーンに FortressLayoutController が見つかりません。「はじめに」タブのテストシーン生成、" +
                    "または手動でGameObjectに FortressLayoutController を追加してください。",
                    MessageType.Warning);
                return;
            }

            controller.EnsureTurrets();
            var turrets = (controller.turrets ?? System.Array.Empty<LaserTurret>())
                .Where(t => t != null)
                .OrderBy(t => t.playerIndex)
                .ToArray();

            if (turrets.Length == 0)
            {
                EditorGUILayout.HelpBox("砲台(LaserTurret)が1つも見つかりません。", MessageType.Warning);
            }

            foreach (var turret in turrets)
            {
                DrawTurretRow(turret);
            }

            if (controller.HasDuplicatePlayerIndex())
            {
                EditorGUILayout.HelpBox("Player Indexが重複している砲台があります。0-3が一意になるよう調整してください。", MessageType.Warning);
            }

            EditorGUILayout.Space(12);
            DrawSharedTuningSection(turrets);
        }

        private static void DrawTurretRow(LaserTurret turret)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var color = FortressColors.PlayerColor(turret.playerIndex);
                var prevColor = GUI.color;
                GUI.color = color;
                GUILayout.Box(GUIContent.none, GUILayout.Width(14), GUILayout.Height(14));
                GUI.color = prevColor;

                EditorGUILayout.LabelField($"Player {turret.playerIndex}", GUILayout.Width(70));
                EditorGUILayout.LabelField(turret.name, GUILayout.Width(140));

                var pos = turret.transform.position;
                EditorGUILayout.LabelField($"({pos.x:0.00}, {pos.y:0.00})", GUILayout.Width(120));

                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField(turret.State.ToString(), GUILayout.Width(90));
                }

                if (GUILayout.Button("シーンで選択", GUILayout.Width(90)))
                {
                    Selection.activeGameObject = turret.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }

        private static void DrawSharedTuningSection(LaserTurret[] turrets)
        {
            EditorGUILayout.LabelField("レーザー・熱チューニング", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ここでの変更は選択中の Laser Tuning Config アセットそのものを書き換えます。" +
                "同じアセットを参照している砲台すべてに即反映されます。",
                MessageType.None);

            var tuning = turrets.Select(t => t.tuning).FirstOrDefault(t => t != null);
            tuning = (LaserTuningConfig)EditorGUILayout.ObjectField(
                new GUIContent("対象の Laser Tuning Config"), tuning, typeof(LaserTuningConfig), false);

            if (tuning == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            var minThickness = EditorGUILayout.FloatField(
                new GUIContent("最小の太さ", "握力0のときのレーザーの太さ。"), tuning.minThickness);
            var maxThickness = EditorGUILayout.FloatField(
                new GUIContent("最大の太さ", "握力1.0のときのレーザーの太さ。"), tuning.maxThickness);
            var heatGain = EditorGUILayout.FloatField(
                new GUIContent("熱上昇係数", "熱 = 握力^2 × この値 × 時間、で積分される。"), tuning.heatGainPerSecond);
            var heatCooling = EditorGUILayout.FloatField(
                new GUIContent("熱冷却速度", "握っていない間、熱が下がる速度(熱量/秒)。"), tuning.heatCoolingPerSecond);
            var overheatThreshold = EditorGUILayout.FloatField(
                new GUIContent("オーバーヒートしきい値", "この熱量に達すると沈黙する。"), tuning.overheatThreshold);
            var silenceDuration = EditorGUILayout.FloatField(
                new GUIContent("沈黙時間(秒)", "オーバーヒート後、再び発射できるまでの時間。"), tuning.overheatSilenceDuration);
            var range = EditorGUILayout.FloatField(new GUIContent("射程"), tuning.range);
            var maxDamage = EditorGUILayout.FloatField(
                new GUIContent("最大秒間ダメージ", "太さ最大時の秒間ダメージ。太さに比例してスケールする。"), tuning.maxDamagePerSecond);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tuning, "Edit Laser Tuning Config");
                tuning.minThickness = Mathf.Max(0f, minThickness);
                tuning.maxThickness = Mathf.Max(0f, maxThickness);
                tuning.heatGainPerSecond = Mathf.Max(0f, heatGain);
                tuning.heatCoolingPerSecond = Mathf.Max(0f, heatCooling);
                tuning.overheatThreshold = Mathf.Max(0.01f, overheatThreshold);
                tuning.overheatSilenceDuration = Mathf.Max(0f, silenceDuration);
                tuning.range = Mathf.Max(0.1f, range);
                tuning.maxDamagePerSecond = Mathf.Max(0f, maxDamage);
                EditorUtility.SetDirty(tuning);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("この設定が未割り当ての砲台に適用"))
            {
                foreach (var turret in turrets)
                {
                    if (turret.tuning == null)
                    {
                        Undo.RecordObject(turret, "Assign Laser Tuning Config");
                        turret.tuning = tuning;
                    }
                }
            }
        }
    }
}
