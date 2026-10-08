using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>輪郭（閉じた頂点列）どうしの距離・内外判定など、すき間チェック用の純粋な計算。</summary>
    public static class FootprintGeometry
    {
        /// <summary>2つの輪郭の一番近い点どうしと、その距離。重なっていれば距離0。</summary>
        public static float ClosestPoints(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b, out Vector2 onA, out Vector2 onB)
        {
            onA = a.Count > 0 ? a[0] : Vector2.zero;
            onB = b.Count > 0 ? b[0] : Vector2.zero;
            if (a.Count == 0 || b.Count == 0)
            {
                return float.PositiveInfinity;
            }

            if (Contains(a, b[0]) || Contains(b, a[0]))
            {
                onA = onB = Contains(a, b[0]) ? b[0] : a[0];
                return 0f;
            }

            var best = float.PositiveInfinity;
            for (var i = 0; i < a.Count; i++)
            {
                var a0 = a[i];
                var a1 = a[(i + 1) % a.Count];
                for (var j = 0; j < b.Count; j++)
                {
                    var b0 = b[j];
                    var b1 = b[(j + 1) % b.Count];
                    var d = SegmentDistance(a0, a1, b0, b1, out var pa, out var pb);
                    if (d < best)
                    {
                        best = d;
                        onA = pa;
                        onB = pb;
                    }
                }
            }

            return best;
        }

        /// <summary>点が輪郭の内側にあるか（偶奇判定）。</summary>
        public static bool Contains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>2本の線分の最短距離と、そのときの両端の点。</summary>
        public static float SegmentDistance(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1, out Vector2 onP, out Vector2 onQ)
        {
            if (SegmentsIntersect(p0, p1, q0, q1, out var hit))
            {
                onP = onQ = hit;
                return 0f;
            }

            var best = float.PositiveInfinity;
            var bestP = p0;
            var bestQ = q0;
            Consider(p0, q0, q1, true, ref best, ref bestP, ref bestQ);
            Consider(p1, q0, q1, true, ref best, ref bestP, ref bestQ);
            Consider(q0, p0, p1, false, ref best, ref bestP, ref bestQ);
            Consider(q1, p0, p1, false, ref best, ref bestP, ref bestQ);
            onP = bestP;
            onQ = bestQ;
            return best;
        }

        /// <summary>端点 point から相手の線分への最短を調べ、今までより近ければ記録する。</summary>
        private static void Consider(Vector2 point, Vector2 s0, Vector2 s1, bool pointIsOnP, ref float best, ref Vector2 onP, ref Vector2 onQ)
        {
            var closest = ClosestOnSegment(point, s0, s1);
            var d = (closest - point).magnitude;
            if (d < best)
            {
                best = d;
                onP = pointIsOnP ? point : closest;
                onQ = pointIsOnP ? closest : point;
            }
        }

        public static Vector2 ClosestOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-12f)
            {
                return a;
            }

            return a + ab * Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        }

        private static bool SegmentsIntersect(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1, out Vector2 hit)
        {
            hit = default;
            var r = p1 - p0;
            var s = q1 - q0;
            var denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-12f)
            {
                return false;
            }

            var qp = q0 - p0;
            var t = (qp.x * s.y - qp.y * s.x) / denominator;
            var u = (qp.x * r.y - qp.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
            {
                return false;
            }

            hit = p0 + r * t;
            return true;
        }
    }
}
