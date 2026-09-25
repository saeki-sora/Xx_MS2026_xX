using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>障害物・地帯の一覧編集（位置・大きさ・耐久・追加/複製/削除）。</summary>
    public sealed class ObstaclesPanel
    {
        private bool _foldout = true;

        public void Draw()
        {
            var obstacles = NavigationObstacle.FindAll()
                .Where(o => o != null)
                .OrderBy(o => o.name, System.StringComparer.Ordinal)
                .ToArray();

            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"障害物・地帯（{obstacles.Length}個）");
            if (_foldout)
            {
                EditorGUILayout.HelpBox(
                    "破壊可＝レーザーで壊せて時間で再生する壁 / 壁＝壊れない壁 / 地帯＝通れるが遅くなり、敵が避けやすい範囲。" +
                    "位置・大きさはここで数値編集するか、Sceneで直接動かせます。破壊可能物の耐久・見た目などの詳細は「破壊可能物」タブで編集します。",
                    MessageType.None);

                foreach (var obstacle in obstacles)
                {
                    DrawRow(obstacle);
                }

                if (obstacles.Length == 0)
                {
                    EditorGUILayout.HelpBox("障害物がありません。下のボタンで追加してください。", MessageType.Info);
                }

                EditorGUILayout.Space(4);
                DrawAddButtons();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRow(NavigationObstacle obstacle)
        {
            var transform = obstacle.transform;
            var destructible = obstacle.GetComponent<DestructibleObstacle>();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(ObstacleFactory.KindLabel(obstacle), EditorStyles.miniBoldLabel, GUILayout.Width(40));

                EditorGUI.BeginChangeCheck();
                var newName = EditorGUILayout.DelayedTextField(obstacle.name, GUILayout.MinWidth(70));
                if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(newName))
                {
                    Undo.RecordObject(obstacle.gameObject, "Rename Obstacle");
                    obstacle.gameObject.name = newName;
                }

                EditorGUI.BeginChangeCheck();
                var x = LabeledFloat("X", transform.position.x);
                var y = LabeledFloat("Y", transform.position.y);
                var w = LabeledFloat("W", transform.localScale.x);
                var h = LabeledFloat("H", transform.localScale.y);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(transform, "Edit Obstacle");
                    transform.position = new Vector3(x, y, transform.position.z);
                    transform.localScale = new Vector3(Mathf.Max(0.1f, w), Mathf.Max(0.1f, h), transform.localScale.z);
                }

                if (destructible != null)
                {
                    EditorGUILayout.LabelField(
                        new GUIContent($"HP {destructible.settings.durability.maxHealth:0.#}", "耐久・見た目などの詳細は「破壊可能物」タブで編集できます。"),
                        GUILayout.Width(62));
                }

                if (GUILayout.Button(new GUIContent("選択", "Sceneビューで選択して表示します。"), GUILayout.Width(40)))
                {
                    Selection.activeGameObject = obstacle.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                if (GUILayout.Button(new GUIContent("複製", "少しずらした位置にコピーを作ります。"), GUILayout.Width(40)))
                {
                    var copy = Object.Instantiate(obstacle.gameObject, obstacle.transform.parent);
                    Undo.RegisterCreatedObjectUndo(copy, "Duplicate Obstacle");
                    copy.name = obstacle.name + "_copy";
                    copy.transform.position = obstacle.transform.position + new Vector3(1f, 1f, 0f);
                }

                if (GUILayout.Button(new GUIContent("削除", "この障害物を削除します。"), GUILayout.Width(40)))
                {
                    Undo.DestroyObjectImmediate(obstacle.gameObject);
                }
            }
        }

        private static float LabeledFloat(string label, float value)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(label.Length * 8f + 4f));
            return EditorGUILayout.FloatField(value, GUILayout.Width(46));
        }

        private static void DrawAddButtons()
        {
            var pivot = SceneView.lastActiveSceneView != null
                ? (Vector2)SceneView.lastActiveSceneView.pivot
                : Vector2.zero;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("＋ 破壊可能ブロック", "レーザーで壊せて、時間で再生する壁。")))
                {
                    Selection.activeGameObject = ObstacleFactory.Create(ObstacleKind.Destructible, pivot);
                }

                if (GUILayout.Button(new GUIContent("＋ 壊れない壁", "レーザーでも壊れない壁。")))
                {
                    Selection.activeGameObject = ObstacleFactory.Create(ObstacleKind.Solid, pivot);
                }

                if (GUILayout.Button(new GUIContent("＋ 通行コスト地帯", "通れるが遅くなり、敵が避けようとする範囲（泥・水たまり等）。")))
                {
                    Selection.activeGameObject = ObstacleFactory.Create(ObstacleKind.CostZone, pivot);
                }
            }
        }
    }
}
