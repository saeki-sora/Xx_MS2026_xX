using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「経路・障害物」タブ。敵の迂回経路（NavigationField）の設定、Sceneへの可視化、
    /// 到達チェック、障害物の一覧編集をまとめる。
    /// </summary>
    public sealed class NavigationTabView : VisualElement
    {
        private const int HeavyCellCount = 40000;

        private readonly IMGUIContainer _imgui;
        private readonly NavigationValidationPanel _validation = new NavigationValidationPanel();
        private readonly ObstaclesPanel _obstacles = new ObstaclesPanel();
        private SerializedObject _serializedField;

        public NavigationTabView()
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
            EditorGUILayout.LabelField("経路・障害物", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "敵は障害物を迂回してコアへ向かいます。障害物（破壊可能/壊れない/通行コスト地帯）を置くと、" +
                "壊れたり再生したりするたびに経路が自動で再計算されます。",
                MessageType.None);

            var field = Object.FindFirstObjectByType<NavigationField>();
            if (field == null)
            {
                EditorGUILayout.HelpBox("シーンに NavigationField がありません。", MessageType.Warning);
                if (GUILayout.Button("経路フィールドを作成"))
                {
                    NavigationFieldTools.CreateField();
                }

                return;
            }

            if (!Application.isPlaying)
            {
                field.RebuildIfChanged();
            }

            field.EnsureBuilt();

            DrawFieldSettings(field);
            DrawVisualization();
            _validation.Draw(field);
            _obstacles.Draw();
        }

        private void DrawFieldSettings(NavigationField field)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("経路フィールドの設定", EditorStyles.miniBoldLabel);

            if (_serializedField == null || _serializedField.targetObject != field)
            {
                _serializedField = new SerializedObject(field);
            }

            _serializedField.Update();
            DrawProperty("areaCenter", "対象範囲の中心");
            DrawProperty("areaSize", "対象範囲の大きさ");
            DrawProperty("cellSize", "グリッド1マスの大きさ");
            DrawProperty("defaultProfile", "既定の経路プロファイル");
            DrawProperty("goalClearCells", "目的地の周囲の確保範囲");
            DrawProperty("autoCollectCores", "コアを自動で目的地にする");
            DrawProperty("extraGoals", "追加の目的地");
            DrawProperty("rebuildMinInterval", "再計算の最短間隔(秒)");
            _serializedField.ApplyModifiedProperties();

            var data = field.Data;
            EditorGUILayout.LabelField(
                "グリッド", $"{data.Width} × {data.Height} = {data.CellCount}セル（再計算{field.RebuildCount}回）");

            if (data.CellCount > HeavyCellCount)
            {
                EditorGUILayout.HelpBox(
                    "セル数が多く、障害物の変化のたびの再計算が重くなる可能性があります。" +
                    "グリッド1マスを大きくするか、対象範囲を絞ってください。", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("範囲を自動フィット", "湧き位置・砲台・コア・障害物が全て収まる範囲に合わせます。")))
                {
                    if (!NavigationFieldTools.AutoFit(field, 4f))
                    {
                        EditorUtility.DisplayDialog("対象がありません", "範囲に含める湧き位置・砲台・コア・障害物がシーンにありません。", "OK");
                    }
                }

                if (GUILayout.Button(new GUIContent("今すぐ再計算", "経路を強制的に再計算します。")))
                {
                    field.RebuildNow();
                }
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            var property = _serializedField.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        private static void DrawVisualization()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Sceneでの可視化", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                NavigationSceneOverlay.ShowBounds = EditorGUILayout.ToggleLeft("範囲の枠", NavigationSceneOverlay.ShowBounds, GUILayout.Width(90));
                NavigationSceneOverlay.ShowBlocked = EditorGUILayout.ToggleLeft("通行不可", NavigationSceneOverlay.ShowBlocked, GUILayout.Width(90));
                NavigationSceneOverlay.ShowCost = EditorGUILayout.ToggleLeft("コスト地帯", NavigationSceneOverlay.ShowCost, GUILayout.Width(100));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                NavigationSceneOverlay.ShowDistanceHeat = EditorGUILayout.ToggleLeft(
                    new GUIContent("距離ヒートマップ", "コアに近いほど青、遠いほど赤で塗ります。"),
                    NavigationSceneOverlay.ShowDistanceHeat, GUILayout.Width(130));
                NavigationSceneOverlay.ShowArrows = EditorGUILayout.ToggleLeft(
                    new GUIContent("進行方向の矢印", "各マスで敵が進む向きを矢印で表示します。"),
                    NavigationSceneOverlay.ShowArrows, GUILayout.Width(130));
                NavigationSceneOverlay.ShowSpawnPaths = EditorGUILayout.ToggleLeft(
                    new GUIContent("湧き位置→コアの予想ルート", "湧き位置ごとの予想ルートを線で表示。緑=到達可、赤=到達不可。"),
                    NavigationSceneOverlay.ShowSpawnPaths);
            }

            if (NavigationSceneOverlay.ShowArrows)
            {
                NavigationSceneOverlay.ArrowStride = EditorGUILayout.IntSlider(
                    new GUIContent("矢印の間引き", "大きいほど矢印の数が減って見やすくなります。"),
                    NavigationSceneOverlay.ArrowStride, 1, 6);
            }

            NavigationSceneOverlay.PreviewProfile = (NavigationProfile)EditorGUILayout.ObjectField(
                new GUIContent("可視化するプロファイル", "大型の敵など、特定の経路プロファイルでの見え方を確認できます。空なら既定。"),
                NavigationSceneOverlay.PreviewProfile, typeof(NavigationProfile), false);
        }
    }
}
