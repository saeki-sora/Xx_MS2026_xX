using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>配置のしかた（プリセット・大きさ・スナップ・クリック配置モード）と、Sceneの表示設定。</summary>
    public sealed class DestructiblePlacementPanel
    {
        private bool _foldout = true;

        public void Draw()
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, "配置");
            if (_foldout)
            {
                DrawPreset();
                DrawSize();
                DrawSnap();
                DrawPlaceButtons();
                DrawDisplayOptions();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawPreset()
        {
            var presets = DestructiblePresetTools.FindAll();
            var names = new string[presets.Count + 1];
            names[0] = "（プリセットなし・既定設定）";
            var selectedIndex = 0;
            var active = DestructibleEditorSettings.ActivePreset;

            for (var i = 0; i < presets.Count; i++)
            {
                names[i + 1] = presets[i].displayName;
                if (presets[i] == active)
                {
                    selectedIndex = i + 1;
                }
            }

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup(
                new GUIContent("配置する種類", "新しく置く破壊可能物の設定（耐久・見た目・演出など）をまとめたプリセット。"),
                selectedIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                DestructibleEditorSettings.ActivePreset = newIndex == 0 ? null : presets[newIndex - 1];
            }
        }

        private static void DrawSize()
        {
            var preset = DestructibleEditorSettings.ActivePreset;

            using (new EditorGUI.DisabledScope(preset == null))
            {
                DestructibleEditorSettings.UsePresetSize = EditorGUILayout.ToggleLeft(
                    new GUIContent("プリセットの大きさを使う", "OFFなら下の大きさで置く。ドラッグで置くときは、ドラッグした範囲が大きさになる。"),
                    DestructibleEditorSettings.UsePresetSize && preset != null);
            }

            using (new EditorGUI.DisabledScope(DestructibleEditorSettings.UsePresetSize && preset != null))
            {
                DestructibleEditorSettings.PlaceSize = EditorGUILayout.Vector2Field(
                    new GUIContent("置くときの大きさ(W,H)"), DestructibleEditorSettings.PlaceSize);
            }
        }

        private static void DrawSnap()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DestructibleEditorSettings.SnapEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent("グリッドにスナップ", "置く位置・大きさを、指定した間隔の目盛りに揃える。"),
                    DestructibleEditorSettings.SnapEnabled, GUILayout.Width(150));

                using (new EditorGUI.DisabledScope(!DestructibleEditorSettings.SnapEnabled))
                {
                    EditorGUILayout.LabelField("間隔", GUILayout.Width(30));
                    DestructibleEditorSettings.SnapSize = EditorGUILayout.FloatField(DestructibleEditorSettings.SnapSize, GUILayout.Width(60));
                }
            }
        }

        private static void DrawPlaceButtons()
        {
            EditorGUILayout.Space(2);

            var placing = DestructibleEditorSettings.PlacementMode;
            var label = placing
                ? "■ 配置モード中（Sceneをクリック=置く／ドラッグ=好きな大きさで置く／Esc=終了）"
                : "▶ Sceneをクリック/ドラッグして配置";
            var toggled = GUILayout.Toggle(placing, new GUIContent(label, "ONの間、Sceneビューでクリックするだけで置けます。"), "Button", GUILayout.Height(28));
            if (toggled != placing)
            {
                DestructibleEditorSettings.PlacementMode = toggled;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button(new GUIContent("Scene画面の中央に1つ置く", "今Sceneに映っている中心に、上の設定で置きます。")))
            {
                var pivot = SceneView.lastActiveSceneView != null
                    ? (Vector2)SceneView.lastActiveSceneView.pivot
                    : Vector2.zero;
                var obstacle = DestructibleFactory.Create(
                    DestructibleEditorSettings.ActivePreset,
                    DestructibleEditorSettings.Snap(pivot),
                    DestructibleEditorSettings.CurrentPlaceSize());
                Selection.activeGameObject = obstacle.gameObject;
            }
        }

        private static void DrawDisplayOptions()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Sceneでの表示", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DestructibleEditorSettings.ShowResizeHandles = EditorGUILayout.ToggleLeft(
                    new GUIContent("リサイズハンドル", "選択中の破壊可能物の角・辺をドラッグして大きさを変えられる。"),
                    DestructibleEditorSettings.ShowResizeHandles, GUILayout.Width(120));
                DestructibleEditorSettings.ShowLabels = EditorGUILayout.ToggleLeft(
                    new GUIContent("名前・耐久の表示"), DestructibleEditorSettings.ShowLabels, GUILayout.Width(120));
                DestructibleEditorSettings.ShowLinks = EditorGUILayout.ToggleLeft(
                    new GUIContent("連動の線・連鎖範囲", "同じグループを線で結び、連鎖の半径を円で表示する。"),
                    DestructibleEditorSettings.ShowLinks);
            }
        }
    }
}
