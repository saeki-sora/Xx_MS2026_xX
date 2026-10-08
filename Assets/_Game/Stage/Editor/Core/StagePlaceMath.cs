using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// シーンで背景を直接動かすときの計算（Unity の物に触らない純関数。テストあり）:
    /// マス目・角度への吸着、他の物の端への吸着、カメラから見た「床への映り込み」、多角形までの距離。
    /// 床は XY 平面（z=0）、床から上は −Z。
    /// </summary>
    public static class StagePlaceMath
    {
        /// <summary>端への吸着の結果。X/Y それぞれ吸着したときだけ、その線の位置（ワールド座標）が入る。</summary>
        public struct EdgeSnapResult
        {
            public Vector2 Offset;
            public bool SnappedX;
            public bool SnappedY;
            public float GuideX;
            public float GuideY;
        }

        public static float SnapValue(float value, float step) =>
            step > 1e-5f ? Mathf.Round(value / step) * step : value;

        public static Vector2 SnapToGrid(Vector2 point, float step) =>
            new Vector2(SnapValue(point.x, step), SnapValue(point.y, step));

        /// <summary>
        /// 動かしている物の範囲（moving）を、他の物の範囲の端にそろえるためのずらし量。
        /// 「くっつける（自分の左端＝相手の右端）」と「そろえる（左端どうし）」の両方を候補にし、threshold 以内で一番近いものを選ぶ。
        /// </summary>
        public static EdgeSnapResult EdgeSnap(Rect moving, IReadOnlyList<Rect> others, float threshold)
        {
            var result = new EdgeSnapResult();
            var bestX = threshold;
            var bestY = threshold;
            foreach (var other in others)
            {
                // 縦にも横にも遠く離れた物には吸着しない（画面の反対側の物の端にそろってしまうのを防ぐ）。
                var nearY = moving.yMax + threshold >= other.yMin && moving.yMin - threshold <= other.yMax;
                var nearX = moving.xMax + threshold >= other.xMin && moving.xMin - threshold <= other.xMax;
                if (nearY)
                {
                    TryAxis(moving.xMin, moving.xMax, other.xMin, other.xMax, ref bestX, ref result.Offset.x, ref result.SnappedX, ref result.GuideX);
                }

                if (nearX)
                {
                    TryAxis(moving.yMin, moving.yMax, other.yMin, other.yMax, ref bestY, ref result.Offset.y, ref result.SnappedY, ref result.GuideY);
                }
            }

            return result;
        }

        private static void TryAxis(float min, float max, float otherMin, float otherMax, ref float best, ref float offset, ref bool snapped, ref float guide)
        {
            Consider(otherMax - min, otherMax, ref best, ref offset, ref snapped, ref guide); // 自分の左端を相手の右端へ（くっつける）
            Consider(otherMin - max, otherMin, ref best, ref offset, ref snapped, ref guide); // 自分の右端を相手の左端へ（くっつける）
            Consider(otherMin - min, otherMin, ref best, ref offset, ref snapped, ref guide); // 左端どうしをそろえる
            Consider(otherMax - max, otherMax, ref best, ref offset, ref snapped, ref guide); // 右端どうしをそろえる
        }

        private static void Consider(float delta, float line, ref float best, ref float offset, ref bool snapped, ref float guide)
        {
            if (Mathf.Abs(delta) <= best)
            {
                best = Mathf.Abs(delta);
                offset = delta;
                snapped = true;
                guide = line;
            }
        }

        /// <summary>
        /// カメラから点を見たとき、その視線の先で床（z=0）に当たる位置。「その点は床のどこに重なって見えるか」。
        /// 遠近なし（正投影）ならカメラの向きにそのまま落とす。床に届かない（視線が床と平行・カメラの後ろ）ならfalse。
        /// </summary>
        public static bool ProjectToFloor(Vector3 point, Vector3 cameraPosition, Vector3 cameraForward, bool orthographic, out Vector2 floor)
        {
            var direction = orthographic ? cameraForward : point - cameraPosition;
            if (direction.z <= 1e-5f)
            {
                floor = new Vector2(point.x, point.y);
                return false;
            }

            var t = -point.z / direction.z;
            var hit = point + direction * t;
            floor = new Vector2(hit.x, hit.y);
            return true;
        }

        /// <summary>点の集まりを囲む凸多角形（反時計回り）。3点未満ならそのまま返す。</summary>
        public static List<Vector2> ConvexHull(List<Vector2> points)
        {
            if (points.Count < 3)
            {
                return new List<Vector2>(points);
            }

            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>(sorted.Count + 1);

            for (var pass = 0; pass < 2; pass++)
            {
                var start = hull.Count;
                for (var i = 0; i < sorted.Count; i++)
                {
                    var p = pass == 0 ? sorted[i] : sorted[sorted.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }

                    hull.Add(p);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        /// <summary>点から多角形までの距離（中に入っていれば0）。closest は多角形の上で一番近い点（中なら点そのもの）。</summary>
        public static float DistanceToPolygon(IReadOnlyList<Vector2> polygon, Vector2 point, out Vector2 closest)
        {
            closest = point;
            if (polygon == null || polygon.Count == 0)
            {
                return float.PositiveInfinity;
            }

            if (polygon.Count >= 3 && FootprintGeometry.Contains(polygon, point))
            {
                return 0f;
            }

            var best = float.PositiveInfinity;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var onEdge = FootprintGeometry.ClosestOnSegment(point, a, b);
                var d = (onEdge - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    closest = onEdge;
                }
            }

            return Mathf.Sqrt(best);
        }

        /// <summary>点の集まりを囲む軸にそろった四角。</summary>
        public static Rect BoundsOf(IEnumerable<Vector2> points)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var any = false;
            foreach (var p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                any = true;
            }

            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
}
