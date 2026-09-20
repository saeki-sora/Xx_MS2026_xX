using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// フロー・フィールドの計算結果。各セルからゴールまでのコストと、次に進むべき向きを保持し、
    /// 任意のワールド座標から滑らかな進行方向を引ける。
    /// </summary>
    public sealed class FlowFieldResult
    {
        private readonly float[] _distance;
        private readonly Vector2[] _direction;

        internal FlowFieldResult(NavigationGridData grid, float[] distance, Vector2[] direction)
        {
            Grid = grid;
            _distance = distance;
            _direction = direction;

            foreach (var d in distance)
            {
                if (!float.IsPositiveInfinity(d) && d > MaxFiniteDistance)
                {
                    MaxFiniteDistance = d;
                }
            }
        }

        public NavigationGridData Grid { get; }

        /// <summary>到達可能なセルの最大コスト。ヒートマップ表示の正規化に使う。</summary>
        public float MaxFiniteDistance { get; }

        public float DistanceAtCell(int x, int y)
        {
            return _distance[Grid.Index(x, y)];
        }

        public Vector2 DirectionAtCell(int x, int y)
        {
            return _direction[Grid.Index(x, y)];
        }

        /// <summary>ゴールまでの重み付きコスト。範囲外・到達不可ならPositiveInfinity。</summary>
        public float DistanceAt(Vector2 world)
        {
            if (!Grid.ContainsWorld(world))
            {
                return float.PositiveInfinity;
            }

            var cell = Grid.WorldToCell(world);
            return _distance[Grid.Index(cell.x, cell.y)];
        }

        public bool IsReachable(Vector2 world)
        {
            return !float.IsPositiveInfinity(DistanceAt(world));
        }

        /// <summary>周囲4セルの向きを補間した、滑らかな進行方向。方向が定まらなければzero。</summary>
        public Vector2 SampleDirection(Vector2 world)
        {
            var fx = (world.x - Grid.Origin.x) / Grid.CellSize - 0.5f;
            var fy = (world.y - Grid.Origin.y) / Grid.CellSize - 0.5f;
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            var sum = Vector2.zero;
            Accumulate(ref sum, x0, y0, (1f - tx) * (1f - ty));
            Accumulate(ref sum, x0 + 1, y0, tx * (1f - ty));
            Accumulate(ref sum, x0, y0 + 1, (1f - tx) * ty);
            Accumulate(ref sum, x0 + 1, y0 + 1, tx * ty);

            if (sum.sqrMagnitude > 1e-6f)
            {
                return sum.normalized;
            }

            var cell = Grid.WorldToCellClamped(world);
            return _direction[Grid.Index(cell.x, cell.y)];
        }

        /// <summary>startからゴールまでの経路を辿ってpointsに詰める。ゴールに着けばtrue。</summary>
        public bool TracePath(Vector2 start, List<Vector2> points, int maxSteps = 3000)
        {
            points.Clear();
            var current = start;
            points.Add(current);
            var step = Grid.CellSize * 0.5f;

            for (var i = 0; i < maxSteps; i++)
            {
                var cell = Grid.WorldToCellClamped(current);
                if (_distance[Grid.Index(cell.x, cell.y)] == 0f)
                {
                    points.Add(Grid.CellCenter(cell.x, cell.y));
                    return true;
                }

                var direction = SampleDirection(current);
                if (direction.sqrMagnitude < 1e-6f)
                {
                    return false;
                }

                current += direction * step;
                points.Add(current);
            }

            return false;
        }

        private void Accumulate(ref Vector2 sum, int x, int y, float weight)
        {
            x = Mathf.Clamp(x, 0, Grid.Width - 1);
            y = Mathf.Clamp(y, 0, Grid.Height - 1);
            sum += _direction[Grid.Index(x, y)] * weight;
        }
    }
}
