using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Sceneビューに経路探索の状態を重ねて描く。通行不可セル・コスト・ゴールまでの距離・進行方向の矢印・
    /// 湧き位置からコアまでの予想ルートを、それぞれON/OFFできる。
    /// </summary>
    [InitializeOnLoad]
    public static class NavigationSceneOverlay
    {
        public static bool ShowBounds = true;
        public static bool ShowBlocked = true;
        public static bool ShowCost;
        public static bool ShowDistanceHeat;
        public static bool ShowArrows;
        public static bool ShowSpawnPaths = true;
        public static int ArrowStride = 2;

        /// <summary>可視化に使うプロファイル。nullなら既定。</summary>
        public static NavigationProfile PreviewProfile;

        private const int MaxHeatCells = 30000;

        private static readonly Vector3[] RectBuffer = new Vector3[4];
        private static readonly List<Vector2> PathBuffer = new List<Vector2>();
        private static double _lastCheckTime;

        static NavigationSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public static bool AnyEnabled =>
            ShowBounds || ShowBlocked || ShowCost || ShowDistanceHeat || ShowArrows || ShowSpawnPaths;

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!AnyEnabled)
            {
                return;
            }

            var field = Object.FindFirstObjectByType<NavigationField>();
            if (field == null)
            {
                return;
            }

            if (!Application.isPlaying && EditorApplication.timeSinceStartup - _lastCheckTime > 0.25)
            {
                _lastCheckTime = EditorApplication.timeSinceStartup;
                field.RebuildIfChanged();
            }

            field.EnsureBuilt();

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var data = field.Data;
            var result = field.GetResult(PreviewProfile);

            if (ShowBounds)
            {
                DrawRect(data.WorldBounds.min, data.WorldBounds.max, Color.clear, new Color(0.2f, 0.9f, 0.9f, 0.9f));
            }

            DrawCells(data, result);

            if (ShowArrows)
            {
                DrawArrows(data, result);
            }

            if (ShowSpawnPaths)
            {
                DrawSpawnPaths(result);
            }
        }

        private static void DrawCells(NavigationGridData data, FlowFieldResult result)
        {
            var drawHeat = ShowDistanceHeat && data.CellCount <= MaxHeatCells;
            var maxDistance = Mathf.Max(0.0001f, result.MaxFiniteDistance);

            for (var y = 0; y < data.Height; y++)
            {
                for (var x = 0; x < data.Width; x++)
                {
                    var index = data.Index(x, y);
                    var min = data.Origin + new Vector2(x, y) * data.CellSize;
                    var max = min + Vector2.one * data.CellSize;

                    if (drawHeat)
                    {
                        var distance = result.DistanceAtCell(x, y);
                        if (!float.IsPositiveInfinity(distance))
                        {
                            var t = Mathf.Clamp01(distance / maxDistance);
                            DrawRect(min, max, Color.HSVToRGB(0.6f * (1f - t), 0.8f, 1f) * new Color(1f, 1f, 1f, 0.28f), Color.clear);
                        }
                    }

                    if (ShowCost && data.ExtraCost[index] > 0f)
                    {
                        var alpha = Mathf.Clamp01(0.15f + data.ExtraCost[index] * 0.1f);
                        DrawRect(min, max, new Color(1f, 0.6f, 0.1f, alpha), Color.clear);
                    }

                    if (ShowBlocked && data.Blocked[index])
                    {
                        DrawRect(min, max, new Color(0.95f, 0.2f, 0.2f, 0.45f), new Color(0.95f, 0.2f, 0.2f, 0.7f));
                    }
                }
            }
        }

        private static void DrawArrows(NavigationGridData data, FlowFieldResult result)
        {
            var stride = Mathf.Max(1, ArrowStride);
            Handles.color = new Color(0.3f, 0.85f, 1f, 0.9f);

            for (var y = 0; y < data.Height; y += stride)
            {
                for (var x = 0; x < data.Width; x += stride)
                {
                    var direction = result.DirectionAtCell(x, y);
                    if (direction.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }

                    var center = data.CellCenter(x, y);
                    var half = data.CellSize * 0.35f * stride;
                    var tail = center - direction * half;
                    var head = center + direction * half;
                    var side = new Vector2(-direction.y, direction.x) * half * 0.3f;

                    Handles.DrawLine(tail, head);
                    Handles.DrawLine(head, head - direction * half * 0.5f + side);
                    Handles.DrawLine(head, head - direction * half * 0.5f - side);
                }
            }
        }

        private static void DrawSpawnPaths(FlowFieldResult result)
        {
            foreach (var spawnPoint in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                var start = (Vector2)spawnPoint.transform.position;
                var reached = result.TracePath(start, PathBuffer);

                var points = new Vector3[PathBuffer.Count];
                for (var i = 0; i < points.Length; i++)
                {
                    points[i] = PathBuffer[i];
                }

                Handles.color = reached ? new Color(0.4f, 1f, 0.4f, 0.95f) : new Color(1f, 0.25f, 0.25f, 0.95f);
                if (points.Length > 1)
                {
                    Handles.DrawAAPolyLine(3f, points);
                }

                if (!reached)
                {
                    Handles.Label(start + Vector2.down * 0.9f, "到達不可", EditorStyles.boldLabel);
                }
            }
        }

        private static void DrawRect(Vector2 min, Vector2 max, Color fill, Color outline)
        {
            RectBuffer[0] = new Vector3(min.x, min.y, 0f);
            RectBuffer[1] = new Vector3(min.x, max.y, 0f);
            RectBuffer[2] = new Vector3(max.x, max.y, 0f);
            RectBuffer[3] = new Vector3(max.x, min.y, 0f);
            Handles.DrawSolidRectangleWithOutline(RectBuffer, fill, outline);
        }
    }
}
