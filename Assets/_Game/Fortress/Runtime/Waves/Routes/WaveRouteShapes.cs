using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵のルートの線の形を整える計算（Unity に頼らない計算。テストあり）。
    /// 経路（マス目の中心を半マスずつたどった点）はガタガタなので、点を間引いて（<see cref="Simplify"/>）、角を丸める（<see cref="Smooth"/>）。
    /// </summary>
    public static class WaveRouteShapes
    {
        /// <summary>まっすぐに近い所の点を間引く（ダグラス・ポイカー法）。epsilon より線から離れた点だけ残す。</summary>
        public static List<Vector2> Simplify(IReadOnlyList<Vector2> points, float epsilon)
        {
            var result = new List<Vector2>();
            if (points == null || points.Count == 0)
            {
                return result;
            }

            if (points.Count < 3)
            {
                result.AddRange(points);
                return result;
            }

            var keep = new bool[points.Count];
            keep[0] = keep[points.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, points.Count - 1));
            while (stack.Count > 0)
            {
                var (first, last) = stack.Pop();
                var farthest = -1;
                var maxDistance = epsilon;
                for (var i = first + 1; i < last; i++)
                {
                    var d = DistanceToSegment(points[i], points[first], points[last]);
                    if (d > maxDistance)
                    {
                        maxDistance = d;
                        farthest = i;
                    }
                }

                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    stack.Push((first, farthest));
                    stack.Push((farthest, last));
                }
            }

            for (var i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        /// <summary>角を丸める（チャイキン法）。両端の点はそのまま。</summary>
        public static List<Vector2> Smooth(IReadOnlyList<Vector2> points, int iterations)
        {
            var current = new List<Vector2>(points ?? new List<Vector2>());
            for (var k = 0; k < iterations && current.Count >= 3; k++)
            {
                var next = new List<Vector2>(current.Count * 2) { current[0] };
                for (var i = 0; i < current.Count - 1; i++)
                {
                    var a = current[i];
                    var b = current[i + 1];
                    next.Add(Vector2.Lerp(a, b, 0.25f));
                    next.Add(Vector2.Lerp(a, b, 0.75f));
                }

                next.Add(current[current.Count - 1]);
                current = next;
            }

            return current;
        }

        /// <summary>折れ線の長さ。</summary>
        public static float Length(IReadOnlyList<Vector2> points)
        {
            var length = 0f;
            for (var i = 1; i < (points?.Count ?? 0); i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        /// <summary>折れ線の始まりから distance 進んだ点と、そこでの向き（単位ベクトル）。長さを越えたら終点。</summary>
        public static Vector2 PointAt(IReadOnlyList<Vector2> points, float distance, out Vector2 direction)
        {
            direction = Vector2.up;
            if (points == null || points.Count == 0)
            {
                return Vector2.zero;
            }

            for (var i = 1; i < points.Count; i++)
            {
                var segment = points[i] - points[i - 1];
                var length = segment.magnitude;
                if (length <= 1e-5f)
                {
                    continue;
                }

                direction = segment / length;
                if (distance <= length)
                {
                    return points[i - 1] + direction * Mathf.Max(0f, distance);
                }

                distance -= length;
            }

            return points[points.Count - 1];
        }

        /// <summary>線の太さ（その道から来る敵の数が多いほど太い。1周の数）。</summary>
        public static float WidthFor(int enemies, float minWidth, float maxWidth)
        {
            if (enemies <= 0)
            {
                return minWidth;
            }

            // 5体で細い線、100体で一番太い線（対数でゆるやかに）。
            var t = Mathf.InverseLerp(Mathf.Log(5f), Mathf.Log(100f), Mathf.Log(Mathf.Max(1, enemies)));
            return Mathf.Lerp(minWidth, maxWidth, t);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 1e-10f)
            {
                return Vector2.Distance(p, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
