using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    [CustomEditor(typeof(DestructibleObstacle))]
    public sealed class DestructibleObstacleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var obstacle = (DestructibleObstacle)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("サイズ", EditorStyles.boldLabel);
            DrawSizeField(obstacle);

            if (obstacle.tuning == null)
            {
                EditorGUILayout.HelpBox(
                    "チューニング(Destructible Obstacle Tuning)が未設定です。「地形破壊」タブから割り当てるか、" +
                    "上のtuningフィールドに直接アサインしてください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("状態", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入ると、HP・破壊/再生の状態をここで確認できます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("State", obstacle.State.ToString());

            var hpRect = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(hpRect, obstacle.HealthRatio01, $"HP {obstacle.HealthRatio01 * 100f:0}%");

            if (obstacle.State == ObstacleState.Destroyed)
            {
                EditorGUILayout.LabelField("再生までの残り時間", $"{obstacle.RegenDelayRemaining:0.0}秒");
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("テスト用に破壊"))
                {
                    obstacle.ForceDestroy();
                }

                if (GUILayout.Button("即座に全回復"))
                {
                    obstacle.ResetToFull();
                }
            }

            Repaint();
        }

        private static void DrawSizeField(DestructibleObstacle obstacle)
        {
            EditorGUI.BeginChangeCheck();
            var newSize = EditorGUILayout.Vector2Field(new GUIContent("サイズ(幅, 高さ)"), obstacle.Size);

            if (EditorGUI.EndChangeCheck())
            {
                ResizeWithUndo(obstacle, newSize);
            }
        }

        /// <summary>
        /// サイズはtransform.localScale経由でコリジョン(BoxCollider2D)と見た目(子のSpriteRenderer)に
        /// 親子階層の仕組みだけで同時に反映される。Undoの対象もtransformだけで十分。
        /// 「地形破壊」タブの一覧からも同じ処理を使う。
        /// </summary>
        public static void ResizeWithUndo(DestructibleObstacle obstacle, Vector2 newSize)
        {
            Undo.RecordObject(obstacle.transform, "Resize Obstacle");

            var collider = obstacle.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                Undo.RecordObject(collider, "Resize Obstacle");
            }

            obstacle.SetSize(newSize);
        }
    }
}
