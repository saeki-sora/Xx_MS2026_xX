using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Sceneビュー上に沸き位置のIDラベルと移動ハンドルを描画する。
    /// 表示/編集のON・OFFやスナップ幅はSpawnPointsPanelから切り替える。
    /// </summary>
    [InitializeOnLoad]
    public static class SpawnPointSceneOverlay
    {
        public static bool ShowLabels = true;
        public static bool EnableHandles;

        /// <summary>0以下ならスナップしない。</summary>
        public static float SnapSize;

        private static GUIStyle _labelStyle;

        static SpawnPointSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public static float Snap(float value)
        {
            return SnapSize > 0f ? Mathf.Round(value / SnapSize) * SnapSize : value;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!ShowLabels && !EnableHandles)
            {
                return;
            }

            _labelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.6f, 0.2f) },
                alignment = TextAnchor.MiddleCenter
            };

            var points = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            foreach (var point in points)
            {
                var position = point.transform.position;

                if (ShowLabels)
                {
                    Handles.Label(position + Vector3.up * 0.8f, point.ResolvedId, _labelStyle);
                }

                if (!EnableHandles)
                {
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                var moved = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(point.transform, "Move Spawn Point");
                    point.transform.position = new Vector3(Snap(moved.x), Snap(moved.y), position.z);
                }
            }
        }
    }
}
