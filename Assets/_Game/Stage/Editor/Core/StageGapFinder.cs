using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトどうしの「すき間」を測る。例: シャンプーと歯ブラシの間を敵が通れるか。
    /// ・幅（実際の形の間の距離）が敵の体より狭いと、群衆が詰まる
    /// ・幅が経路のマス目より狭いと、経路の計算上は「塞がっている」扱いになり、敵はそこを通ろうとしない
    /// </summary>
    public static class StageGapFinder
    {
        /// <summary>この幅より広いすき間は気にしない（ワールド単位）。</summary>
        public const float ReportWidth = 1.5f;

        public struct Gap
        {
            public StageProp A;
            public StageProp B;
            public Vector2 PointA;
            public Vector2 PointB;
            public float Width;

            /// <summary>経路のマス目の上でも通れるか（真ん中のマスが塞がっていないか）。</summary>
            public bool PassableOnGrid;

            public Vector2 Middle => (PointA + PointB) * 0.5f;
        }

        public static List<Gap> Find(IReadOnlyList<StageProp> props)
        {
            var shapes = new List<(StageProp prop, Vector2[] points, Rect bounds)>();
            foreach (var prop in props)
            {
                if (prop == null || !prop.isActiveAndEnabled || !BlocksEnemies(prop))
                {
                    continue;
                }

                foreach (var path in WorldPaths(prop))
                {
                    shapes.Add((prop, path, BoundsOf(path)));
                }
            }

            var field = Object.FindFirstObjectByType<NavigationField>();
            var grid = field != null ? (field.Data ?? Build(field)) : null;
            var result = new List<Gap>();

            for (var i = 0; i < shapes.Count; i++)
            {
                for (var j = i + 1; j < shapes.Count; j++)
                {
                    if (shapes[i].prop == shapes[j].prop || !Near(shapes[i].bounds, shapes[j].bounds, ReportWidth))
                    {
                        continue;
                    }

                    var width = FootprintGeometry.ClosestPoints(shapes[i].points, shapes[j].points, out var a, out var b);
                    if (width <= 0.001f || width > ReportWidth)
                    {
                        continue;
                    }

                    var gap = new Gap { A = shapes[i].prop, B = shapes[j].prop, PointA = a, PointB = b, Width = width };
                    gap.PassableOnGrid = grid == null || IsFree(grid, gap.Middle);
                    result.Add(gap);
                }
            }

            return result;
        }

        private static float _cachedDiameter;
        private static double _diameterExpires;

        /// <summary>群衆の敵の一番大きい体の直径（プロジェクト内の敵の種類から。10秒ごとに調べ直す）。</summary>
        public static float LargestEnemyDiameter()
        {
            if (EditorApplication.timeSinceStartup < _diameterExpires)
            {
                return _cachedDiameter;
            }

            _diameterExpires = EditorApplication.timeSinceStartup + 10.0;
            var largest = 0f;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(EnemyTypeDefinition)))
            {
                var type = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (type != null && type.simulationMode == EnemySimulationMode.Swarm)
                {
                    largest = Mathf.Max(largest, type.swarm.radius * 2f);
                }
            }

            _cachedDiameter = largest > 0f ? largest : 0.44f;
            return _cachedDiameter;
        }

        /// <summary>通れない範囲を、ワールド座標の頂点列で返す。</summary>
        public static IEnumerable<Vector2[]> WorldPaths(StageProp prop)
        {
            var collider = prop.footprint != null ? prop.footprint.Collider : null;
            if (collider == null)
            {
                yield break;
            }

            var t = collider.transform;
            for (var p = 0; p < collider.pathCount; p++)
            {
                var local = collider.GetPath(p);
                var world = new Vector2[local.Length];
                for (var k = 0; k < local.Length; k++)
                {
                    world[k] = t.TransformPoint(local[k] + collider.offset);
                }

                yield return world;
            }
        }

        public static bool BlocksEnemies(StageProp prop) =>
            prop.role == StagePropRole.Wall || prop.role == StagePropRole.DestructibleWall;

        private static NavigationGridData Build(NavigationField field)
        {
            field.EnsureBuilt();
            return field.Data;
        }

        private static bool IsFree(NavigationGridData grid, Vector2 world)
        {
            if (!grid.ContainsWorld(world))
            {
                return true;
            }

            var cell = grid.WorldToCell(world);
            return !grid.Blocked[grid.Index(cell.x, cell.y)];
        }

        private static Rect BoundsOf(Vector2[] path)
        {
            var min = path[0];
            var max = path[0];
            foreach (var p in path)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Near(Rect a, Rect b, float distance) =>
            a.xMin - distance <= b.xMax && b.xMin - distance <= a.xMax &&
            a.yMin - distance <= b.yMax && b.yMin - distance <= a.yMax;
    }
}
