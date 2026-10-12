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
        private const string PrefPrefix = "MS2026.FortressDesigner.SpawnPoints.";

        // シーンビューの描画のたびに設定を読みに行かないよう、最初の1回だけ読んで手元に持つ。
        private static bool? _showLabels;
        private static bool? _enableHandles;

        /// <summary>シーンにIDを出すか（パソコンごとに覚える）。</summary>
        public static bool ShowLabels
        {
            get => _showLabels ??= EditorPrefs.GetBool(PrefPrefix + "Labels", true);
            set
            {
                if (_showLabels != value)
                {
                    _showLabels = value;
                    EditorPrefs.SetBool(PrefPrefix + "Labels", value);
                }
            }
        }

        /// <summary>シーンで移動ハンドルを出すか（パソコンごとに覚える）。</summary>
        public static bool EnableHandles
        {
            get => _enableHandles ??= EditorPrefs.GetBool(PrefPrefix + "Handles", false);
            set
            {
                if (_enableHandles != value)
                {
                    _enableHandles = value;
                    EditorPrefs.SetBool(PrefPrefix + "Handles", value);
                }
            }
        }

        /// <summary>湧き位置ごとの、編集中のウェーブで湧く数（ラベルに「（20体）」と出す。無ければ出さない）。</summary>
        public static System.Collections.Generic.Dictionary<string, int> EnemiesById;

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
                    var id = point.ResolvedId;
                    var text = EnemiesById == null ? id
                        : EnemiesById.TryGetValue(id, out var count) && count > 0 ? $"{id}（{count}体）" : $"{id}（未使用）";
                    Handles.Label(position + Vector3.up * 0.8f, text, _labelStyle);
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
