using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 多角形の塗りと線を描く UI の部品（練習の画面のギザギザの吹き出し・歯磨き粉のチューブ・札など）。
    /// 点は RectTransform の真ん中を原点にしたピクセル（上が +y）。毎フレーム描き直してよい（Clear → Add… → 自動で描き直し）。
    /// 塗りは凹んだ形でもよい（耳切りで三角形に分ける）。線は角をとがらせてつなぐ（とがりすぎる所は切る）。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PracticeShape : MaskableGraphic
    {
        private struct FillPart
        {
            public Vector2[] Points;
            public Color32 A;
            public Color32 B;
            public bool Vertical;
        }

        private struct StrokePart
        {
            public Vector2[] Points;
            public float Width;
            public Color32 Color;
            public bool Closed;
        }

        private readonly List<FillPart> _fills = new List<FillPart>();
        private readonly List<StrokePart> _strokes = new List<StrokePart>();
        private readonly List<int> _order = new List<int>();

        public void Clear()
        {
            _fills.Clear();
            _strokes.Clear();
            _order.Clear();
            SetVerticesDirty();
        }

        /// <summary>塗る（a → b のグラデーション。vertical なら上が a、下が b。そうでなければ左が a、右が b）。</summary>
        public void Fill(IList<Vector2> points, Color a, Color? b = null, bool vertical = true)
        {
            if (points == null || points.Count < 3)
            {
                return;
            }

            _order.Add(_fills.Count);
            _fills.Add(new FillPart { Points = ToArray(points), A = a, B = b ?? a, Vertical = vertical });
            SetVerticesDirty();
        }

        /// <summary>線を引く。</summary>
        public void Stroke(IList<Vector2> points, float width, Color color, bool closed)
        {
            if (points == null || points.Count < 2 || width <= 0f)
            {
                return;
            }

            _order.Add(-1 - _strokes.Count);
            _strokes.Add(new StrokePart { Points = ToArray(points), Width = width, Color = color, Closed = closed });
            SetVerticesDirty();
        }

        /// <summary>塗ってから線で囲む。</summary>
        public void FillStroke(IList<Vector2> points, Color fill, Color line, float width, Color? fill2 = null, bool vertical = true)
        {
            Fill(points, fill, fill2, vertical);
            Stroke(points, width, line, true);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (var id in _order)
            {
                if (id >= 0)
                {
                    AddFill(vh, _fills[id]);
                }
                else
                {
                    AddStroke(vh, _strokes[-1 - id]);
                }
            }
        }

        // ───────── 塗り ─────────

        private static readonly List<int> Indices = new List<int>();
        private static readonly List<int> Triangles = new List<int>();

        private void AddFill(VertexHelper vh, FillPart part)
        {
            var pts = part.Points;
            var n = pts.Length;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in pts)
            {
                var v = part.Vertical ? p.y : p.x;
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            var span = Mathf.Max(0.001f, max - min);
            var start = vh.currentVertCount;
            foreach (var p in pts)
            {
                var k = part.Vertical ? (max - p.y) / span : (p.x - min) / span;
                vh.AddVert(new Vector3(p.x, p.y), Color32.Lerp(part.A, part.B, k), Vector4.zero);
            }

            Triangulate(pts, Triangles);
            for (var i = 0; i + 2 < Triangles.Count; i += 3)
            {
                vh.AddTriangle(start + Triangles[i], start + Triangles[i + 1], start + Triangles[i + 2]);
            }
        }

        private static float SignedArea(Vector2[] pts)
        {
            var a = 0f;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                a += (pts[j].x * pts[i].y) - (pts[i].x * pts[j].y);
            }

            return a * 0.5f;
        }

        /// <summary>耳切りで三角形に分ける（凹んだ形も可。自分と交わる形は不可）。</summary>
        private static void Triangulate(Vector2[] pts, List<int> result)
        {
            result.Clear();
            Indices.Clear();
            var n = pts.Length;
            var ccw = SignedArea(pts) > 0f;
            for (var i = 0; i < n; i++)
            {
                Indices.Add(ccw ? i : n - 1 - i);
            }

            var guard = 0;
            while (Indices.Count > 3 && guard++ < n * n)
            {
                var clipped = false;
                for (var i = 0; i < Indices.Count; i++)
                {
                    var ia = Indices[(i + Indices.Count - 1) % Indices.Count];
                    var ib = Indices[i];
                    var ic = Indices[(i + 1) % Indices.Count];
                    var a = pts[ia];
                    var b = pts[ib];
                    var c = pts[ic];
                    if (Cross(b - a, c - b) <= 0f)
                    {
                        continue;
                    }

                    var inside = false;
                    foreach (var other in Indices)
                    {
                        if (other == ia || other == ib || other == ic)
                        {
                            continue;
                        }

                        if (InTriangle(pts[other], a, b, c))
                        {
                            inside = true;
                            break;
                        }
                    }

                    if (inside)
                    {
                        continue;
                    }

                    result.Add(ia);
                    result.Add(ib);
                    result.Add(ic);
                    Indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    break;
                }
            }

            if (Indices.Count == 3)
            {
                result.Add(Indices[0]);
                result.Add(Indices[1]);
                result.Add(Indices[2]);
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => (a.x * b.y) - (a.y * b.x);

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Cross(b - a, p - a);
            var d2 = Cross(c - b, p - b);
            var d3 = Cross(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }

        // ───────── 線 ─────────

        private void AddStroke(VertexHelper vh, StrokePart part)
        {
            var pts = part.Points;
            var n = pts.Length;
            var hw = part.Width * 0.5f;
            var start = vh.currentVertCount;
            for (var i = 0; i < n; i++)
            {
                var p = pts[i];
                Vector2 dirIn, dirOut;
                var hasPrev = part.Closed || i > 0;
                var hasNext = part.Closed || i < n - 1;
                dirIn = hasPrev ? (p - pts[(i - 1 + n) % n]).normalized : Vector2.zero;
                dirOut = hasNext ? (pts[(i + 1) % n] - p).normalized : Vector2.zero;
                if (!hasPrev)
                {
                    dirIn = dirOut;
                }

                if (!hasNext)
                {
                    dirOut = dirIn;
                }

                var nIn = new Vector2(-dirIn.y, dirIn.x);
                var nOut = new Vector2(-dirOut.y, dirOut.x);
                var miter = (nIn + nOut).normalized;
                if (miter.sqrMagnitude < 1e-6f)
                {
                    miter = nOut;
                }

                var len = hw / Mathf.Max(0.35f, Vector2.Dot(miter, nOut));
                vh.AddVert(new Vector3(p.x + (miter.x * len), p.y + (miter.y * len)), part.Color, Vector4.zero);
                vh.AddVert(new Vector3(p.x - (miter.x * len), p.y - (miter.y * len)), part.Color, Vector4.zero);
            }

            var segs = part.Closed ? n : n - 1;
            for (var i = 0; i < segs; i++)
            {
                var a = start + (i * 2);
                var b = start + (((i + 1) % n) * 2);
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
        }

        private static Vector2[] ToArray(IList<Vector2> points)
        {
            var array = new Vector2[points.Count];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = points[i];
            }

            return array;
        }

        // ───────── 形を作る ─────────

        /// <summary>角の丸い四角（中心 c、大きさ size、角の半径 r）。</summary>
        public static List<Vector2> RoundRect(Vector2 c, Vector2 size, float r, int segments = 5)
        {
            var list = new List<Vector2>();
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            r = Mathf.Min(r, hx, hy);
            Vector2[] centers = { new Vector2(hx - r, hy - r), new Vector2(-hx + r, hy - r), new Vector2(-hx + r, -hy + r), new Vector2(hx - r, -hy + r) };
            for (var k = 0; k < 4; k++)
            {
                for (var s = 0; s <= segments; s++)
                {
                    var a = ((k * 90f) + (s * 90f / segments)) * Mathf.Deg2Rad;
                    list.Add(c + centers[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            return list;
        }

        /// <summary>平行四辺形（中心 c、大きさ size、傾き skew＝上の辺の右ずれ）。</summary>
        public static List<Vector2> Parallelogram(Vector2 c, Vector2 size, float skew)
        {
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            return new List<Vector2>
            {
                c + new Vector2(-hx + skew, hy),
                c + new Vector2(hx, hy),
                c + new Vector2(hx - skew, -hy),
                c + new Vector2(-hx, -hy)
            };
        }

        /// <summary>円（または円弧。度で、0 = 上、時計回り）。</summary>
        public static List<Vector2> Arc(Vector2 c, float radius, float fromDeg, float toDeg, int segments)
        {
            var list = new List<Vector2>();
            for (var i = 0; i <= segments; i++)
            {
                var a = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
                list.Add(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius);
            }

            return list;
        }

        /// <summary>星形。</summary>
        public static List<Vector2> Star(Vector2 c, float outer, float inner)
        {
            var list = new List<Vector2>();
            for (var i = 0; i < 10; i++)
            {
                var a = i * 36f * Mathf.Deg2Rad;
                var r = i % 2 == 0 ? outer : inner;
                list.Add(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r);
            }

            return list;
        }
    }
}
