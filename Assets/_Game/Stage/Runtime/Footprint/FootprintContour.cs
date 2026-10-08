using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 塗られたマス目の境界をたどって閉じた輪郭（頂点列）にし、頂点を間引く。
    /// 外周は反時計回り、穴は時計回りになる。斜めにだけ接するマス同士は別の輪郭に分ける。
    /// </summary>
    public static class FootprintContour
    {
        private readonly struct Edge
        {
            public readonly Vector2Int From;
            public readonly Vector2Int To;

            public Edge(Vector2Int from, Vector2Int to)
            {
                From = from;
                To = to;
            }

            public Vector2Int Direction => To - From;
        }

        /// <summary>マス目の境界の輪郭を、ワールド（親から見た）座標の頂点列で返す。</summary>
        public static List<Vector2[]> Trace(FootprintGrid grid)
        {
            var outgoing = BuildBoundaryEdges(grid, out var edgeCount);
            var loops = new List<Vector2[]>();
            var used = new HashSet<(Vector2Int, Vector2Int)>();

            foreach (var pair in outgoing)
            {
                foreach (var start in pair.Value)
                {
                    if (used.Contains((start.From, start.To)))
                    {
                        continue;
                    }

                    var loop = FollowLoop(start, outgoing, used, edgeCount);
                    if (loop.Count >= 3)
                    {
                        loops.Add(ToWorld(grid, RemoveCollinear(loop)));
                    }
                }
            }

            return loops;
        }

        /// <summary>閉じた頂点列をダグラス・ピーカー法で間引く（tolerance以内のずれは無視）。</summary>
        public static Vector2[] Simplify(Vector2[] loop, float tolerance)
        {
            if (tolerance <= 0f || loop.Length <= 4)
            {
                return loop;
            }

            // 最初の点から一番遠い点で2つに分け、それぞれを開いた折れ線として間引く。
            var far = 0;
            var farDistance = -1f;
            for (var i = 1; i < loop.Length; i++)
            {
                var d = (loop[i] - loop[0]).sqrMagnitude;
                if (d > farDistance)
                {
                    farDistance = d;
                    far = i;
                }
            }

            var keep = new bool[loop.Length + 1];
            keep[0] = keep[far] = keep[loop.Length] = true;
            Vector2 At(int i) => loop[i % loop.Length];

            void Recurse(int a, int b)
            {
                if (b - a < 2)
                {
                    return;
                }

                var maxDistance = -1f;
                var index = -1;
                for (var i = a + 1; i < b; i++)
                {
                    var d = DistanceToSegment(At(i), At(a), At(b));
                    if (d > maxDistance)
                    {
                        maxDistance = d;
                        index = i;
                    }
                }

                if (maxDistance > tolerance)
                {
                    keep[index] = true;
                    Recurse(a, index);
                    Recurse(index, b);
                }
            }

            Recurse(0, far);
            Recurse(far, loop.Length);

            var result = new List<Vector2>();
            for (var i = 0; i < loop.Length; i++)
            {
                if (keep[i])
                {
                    result.Add(loop[i]);
                }
            }

            return result.Count >= 3 ? result.ToArray() : loop;
        }

        /// <summary>符号付き面積（反時計回りが正）。</summary>
        public static float SignedArea(IReadOnlyList<Vector2> loop)
        {
            var area = 0f;
            for (var i = 0; i < loop.Count; i++)
            {
                var a = loop[i];
                var b = loop[(i + 1) % loop.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return area * 0.5f;
        }

        private static Dictionary<Vector2Int, List<Edge>> BuildBoundaryEdges(FootprintGrid grid, out int edgeCount)
        {
            var outgoing = new Dictionary<Vector2Int, List<Edge>>();
            edgeCount = 0;

            void Add(Vector2Int from, Vector2Int to)
            {
                if (!outgoing.TryGetValue(from, out var list))
                {
                    list = new List<Edge>(1);
                    outgoing[from] = list;
                }

                list.Add(new Edge(from, to));
            }

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (!grid[x, y])
                    {
                        continue;
                    }

                    // 塗られたマスが進行方向の左側に来る向きで、空きマスとの境界に辺を張る（外周は反時計回り）。
                    if (!grid[x, y - 1]) { Add(new Vector2Int(x, y), new Vector2Int(x + 1, y)); edgeCount++; }
                    if (!grid[x + 1, y]) { Add(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)); edgeCount++; }
                    if (!grid[x, y + 1]) { Add(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1)); edgeCount++; }
                    if (!grid[x - 1, y]) { Add(new Vector2Int(x, y + 1), new Vector2Int(x, y)); edgeCount++; }
                }
            }

            return outgoing;
        }

        private static List<Vector2Int> FollowLoop(
            Edge start,
            Dictionary<Vector2Int, List<Edge>> outgoing,
            HashSet<(Vector2Int, Vector2Int)> used,
            int edgeCount)
        {
            var points = new List<Vector2Int>();
            var current = start;

            for (var guard = 0; guard <= edgeCount; guard++)
            {
                used.Add((current.From, current.To));
                points.Add(current.From);

                if (current.To == start.From)
                {
                    break;
                }

                if (!outgoing.TryGetValue(current.To, out var candidates) || !TryPickNext(current, candidates, used, out var next))
                {
                    break;
                }

                current = next;
            }

            return points;
        }

        /// <summary>分かれ道（斜めにだけ接するマス）では左折を優先し、輪郭が1点で交わらないようにする。</summary>
        private static bool TryPickNext(Edge current, List<Edge> candidates, HashSet<(Vector2Int, Vector2Int)> used, out Edge next)
        {
            var d = current.Direction;
            var left = new Vector2Int(-d.y, d.x);
            var right = new Vector2Int(d.y, -d.x);
            var preferred = new[] { left, d, right };

            foreach (var direction in preferred)
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.Direction == direction && !used.Contains((candidate.From, candidate.To)))
                    {
                        next = candidate;
                        return true;
                    }
                }
            }

            next = default;
            return false;
        }

        private static List<Vector2Int> RemoveCollinear(List<Vector2Int> loop)
        {
            var result = new List<Vector2Int>(loop.Count);
            for (var i = 0; i < loop.Count; i++)
            {
                var prev = loop[(i - 1 + loop.Count) % loop.Count];
                var point = loop[i];
                var next = loop[(i + 1) % loop.Count];
                var a = point - prev;
                var b = next - point;
                if (a.x * b.y - a.y * b.x != 0)
                {
                    result.Add(point);
                }
            }

            return result;
        }

        private static Vector2[] ToWorld(FootprintGrid grid, List<Vector2Int> loop)
        {
            var result = new Vector2[loop.Count];
            for (var i = 0; i < loop.Count; i++)
            {
                result[i] = grid.Origin + (Vector2)loop[i] * grid.CellSize;
            }

            return result;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f)
            {
                return (p - a).magnitude;
            }

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
