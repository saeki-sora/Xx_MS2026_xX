using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// NavigationGridDataとゴール群から、フロー・フィールド（全セルのゴールまでのコストと進行方向）を計算する。
    /// 8方向Dijkstra。斜め移動は角抜け（隣接する壁のかすめ）を禁止する。
    /// </summary>
    public static class FlowFieldSolver
    {
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private const float DiagonalLength = 1.41421356f;

        public static FlowFieldResult Solve(
            NavigationGridData grid,
            NavigationProfileSettings settings,
            IReadOnlyList<Vector2> goals,
            int goalClearCells)
        {
            var width = grid.Width;
            var height = grid.Height;
            var count = grid.CellCount;

            var blocked = BuildBlockedMap(grid, settings.hardInflateCells);
            var goalCells = new List<int>();
            foreach (var goal in goals)
            {
                var cell = grid.WorldToCellClamped(goal);
                goalCells.Add(grid.Index(cell.x, cell.y));
                ClearArea(blocked, width, height, cell.x, cell.y, goalClearCells);
            }

            var cost = BuildCostMap(grid, settings);
            var distance = new float[count];
            for (var i = 0; i < count; i++)
            {
                distance[i] = float.PositiveInfinity;
            }

            RunDijkstra(width, height, blocked, cost, goalCells, distance);
            var direction = BuildDirections(width, height, blocked, distance);
            return new FlowFieldResult(grid, distance, direction);
        }

        private static bool[] BuildBlockedMap(NavigationGridData grid, int inflate)
        {
            var result = (bool[])grid.Blocked.Clone();
            if (inflate <= 0)
            {
                return result;
            }

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid.Blocked[grid.Index(x, y)])
                    {
                        continue;
                    }

                    for (var oy = -inflate; oy <= inflate; oy++)
                    {
                        for (var ox = -inflate; ox <= inflate; ox++)
                        {
                            var nx = x + ox;
                            var ny = y + oy;
                            if (ox * ox + oy * oy <= inflate * inflate && grid.InBounds(nx, ny))
                            {
                                result[grid.Index(nx, ny)] = true;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static void ClearArea(bool[] blocked, int width, int height, int cx, int cy, int radius)
        {
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                for (var x = cx - radius; x <= cx + radius; x++)
                {
                    if (x >= 0 && y >= 0 && x < width && y < height)
                    {
                        blocked[y * width + x] = false;
                    }
                }
            }
        }

        private static float[] BuildCostMap(NavigationGridData grid, NavigationProfileSettings settings)
        {
            var count = grid.CellCount;
            var cost = new float[count];
            for (var i = 0; i < count; i++)
            {
                cost[i] = 1f + grid.ExtraCost[i];
            }

            var radius = settings.softClearanceCells;
            if (radius <= 0 || settings.softClearancePenalty <= 0f)
            {
                return cost;
            }

            var nearest = new float[count];
            for (var i = 0; i < count; i++)
            {
                nearest[i] = float.PositiveInfinity;
            }

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid.Blocked[grid.Index(x, y)])
                    {
                        continue;
                    }

                    for (var oy = -radius; oy <= radius; oy++)
                    {
                        for (var ox = -radius; ox <= radius; ox++)
                        {
                            var nx = x + ox;
                            var ny = y + oy;
                            if (!grid.InBounds(nx, ny))
                            {
                                continue;
                            }

                            var index = grid.Index(nx, ny);
                            nearest[index] = Mathf.Min(nearest[index], ox * ox + oy * oy);
                        }
                    }
                }
            }

            for (var i = 0; i < count; i++)
            {
                if (nearest[i] > radius * radius)
                {
                    continue;
                }

                var d = Mathf.Sqrt(nearest[i]);
                cost[i] += settings.softClearancePenalty * (radius + 1f - d) / (radius + 1f);
            }

            return cost;
        }

        private static void RunDijkstra(
            int width, int height, bool[] blocked, float[] cost, List<int> goalCells, float[] distance)
        {
            var heap = new MinHeap();
            foreach (var goal in goalCells)
            {
                distance[goal] = 0f;
                heap.Push(goal, 0f);
            }

            while (heap.Pop(out var index, out var key))
            {
                if (key > distance[index])
                {
                    continue;
                }

                var x = index % width;
                var y = index / width;

                for (var k = 0; k < 8; k++)
                {
                    var nx = x + Dx[k];
                    var ny = y + Dy[k];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    var neighbor = ny * width + nx;
                    if (blocked[neighbor])
                    {
                        continue;
                    }

                    var isDiagonal = k >= 4;
                    if (isDiagonal && (blocked[y * width + nx] || blocked[ny * width + x]))
                    {
                        continue;
                    }

                    var next = key + (isDiagonal ? DiagonalLength : 1f) * cost[neighbor];
                    if (next < distance[neighbor])
                    {
                        distance[neighbor] = next;
                        heap.Push(neighbor, next);
                    }
                }
            }
        }

        private static Vector2[] BuildDirections(int width, int height, bool[] blocked, float[] distance)
        {
            var direction = new Vector2[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    if (distance[index] == 0f)
                    {
                        continue;
                    }

                    // 通行不可（膨らませた範囲内）にいる場合は、脱出のため斜め制限を無視して最寄りの到達可能セルへ向かう。
                    var isInsideBlocked = blocked[index];
                    var best = float.PositiveInfinity;
                    var bestX = 0;
                    var bestY = 0;

                    for (var k = 0; k < 8; k++)
                    {
                        var nx = x + Dx[k];
                        var ny = y + Dy[k];
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        var neighborDistance = distance[ny * width + nx];
                        if (float.IsPositiveInfinity(neighborDistance))
                        {
                            continue;
                        }

                        if (!isInsideBlocked && k >= 4 && (blocked[y * width + nx] || blocked[ny * width + x]))
                        {
                            continue;
                        }

                        if (neighborDistance < best)
                        {
                            best = neighborDistance;
                            bestX = Dx[k];
                            bestY = Dy[k];
                        }
                    }

                    if (best < distance[index])
                    {
                        direction[index] = new Vector2(bestX, bestY).normalized;
                    }
                }
            }

            return direction;
        }

        private sealed class MinHeap
        {
            private int[] _items = new int[256];
            private float[] _keys = new float[256];
            private int _count;

            public void Push(int item, float key)
            {
                if (_count == _items.Length)
                {
                    Array.Resize(ref _items, _count * 2);
                    Array.Resize(ref _keys, _count * 2);
                }

                var i = _count++;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (_keys[parent] <= key)
                    {
                        break;
                    }

                    _items[i] = _items[parent];
                    _keys[i] = _keys[parent];
                    i = parent;
                }

                _items[i] = item;
                _keys[i] = key;
            }

            public bool Pop(out int item, out float key)
            {
                if (_count == 0)
                {
                    item = 0;
                    key = 0f;
                    return false;
                }

                item = _items[0];
                key = _keys[0];
                _count--;

                if (_count > 0)
                {
                    var lastItem = _items[_count];
                    var lastKey = _keys[_count];
                    var i = 0;
                    while (true)
                    {
                        var child = i * 2 + 1;
                        if (child >= _count)
                        {
                            break;
                        }

                        if (child + 1 < _count && _keys[child + 1] < _keys[child])
                        {
                            child++;
                        }

                        if (_keys[child] >= lastKey)
                        {
                            break;
                        }

                        _items[i] = _items[child];
                        _keys[i] = _keys[child];
                        i = child;
                    }

                    _items[i] = lastItem;
                    _keys[i] = lastKey;
                }

                return true;
            }
        }
    }
}
