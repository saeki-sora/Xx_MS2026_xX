using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// コードで絵を描くための下書き用紙。円・カプセル（太い線）・楕円・多角形を「形の内側からの距離」で重ねていき（和集合）、
    /// 最後に距離から、ふちを1ピクセルぶんぼかした塗り・輪郭を作る。
    /// 座標は描く側の単位（例: 守護霊の高さ = 1）で渡し、y は下向き（デモの canvas と同じ）。
    /// 計算は形ごとの外枠の中だけで行うので、大きな絵でも軽い。
    /// </summary>
    internal sealed class GreenSdfCanvas
    {
        private const float Far = 1e6f;

        private readonly float[] _distance;
        private readonly float _scale;
        private readonly float _originX;
        private readonly float _originY;
        private readonly int _margin;
        private readonly List<Vector2> _points = new List<Vector2>(64);

        /// <param name="width">幅（ピクセル）。</param>
        /// <param name="height">高さ（ピクセル）。</param>
        /// <param name="scale">1 単位が何ピクセルか。</param>
        /// <param name="originX">原点のピクセル位置（左から）。</param>
        /// <param name="originY">原点のピクセル位置（上から）。</param>
        /// <param name="margin">形の外側に、何ピクセルまで距離を計算するか（輪郭の太さ＋2 くらい）。</param>
        public GreenSdfCanvas(int width, int height, float scale, float originX, float originY, int margin)
        {
            Width = width;
            Height = height;
            _scale = scale;
            _originX = originX;
            _originY = originY;
            _margin = Mathf.Max(2, margin);
            _distance = new float[width * height];
            for (var i = 0; i < _distance.Length; i++)
            {
                _distance[i] = Far;
            }
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>1 単位が何ピクセルか。</summary>
        public float Scale => _scale;

        /// <summary>ピクセル (x, 上からの y) の、形のふちからの距離（ピクセル。内側は負）。</summary>
        public float DistanceAt(int x, int yFromTop)
        {
            return _distance[yFromTop * Width + x];
        }

        public void Circle(Vector2 center, float radius)
        {
            var c = ToPixel(center);
            var r = radius * _scale;
            Raster(c - new Vector2(r, r), c + new Vector2(r, r), p => (p - c).magnitude - r);
        }

        /// <summary>太い線（両端が丸い）。</summary>
        public void Capsule(Vector2 from, Vector2 to, float radius)
        {
            var a = ToPixel(from);
            var b = ToPixel(to);
            var r = radius * _scale;
            var ab = b - a;
            var lengthSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            Raster(Vector2.Min(a, b) - new Vector2(r, r), Vector2.Max(a, b) + new Vector2(r, r), p =>
            {
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                return (p - (a + ab * t)).magnitude - r;
            });
        }

        /// <summary>2次ベジェ曲線に沿った太い線。</summary>
        public void CurveStroke(Vector2 from, Vector2 control, Vector2 to, float radius, int steps = 10)
        {
            var previous = from;
            for (var i = 1; i <= steps; i++)
            {
                var p = Quadratic(from, control, to, i / (float)steps);
                Capsule(previous, p, radius);
                previous = p;
            }
        }

        /// <summary>回した楕円（距離は近似）。</summary>
        public void Ellipse(Vector2 center, float radiusX, float radiusY, float angleRadians)
        {
            var c = ToPixel(center);
            var rx = Mathf.Max(radiusX * _scale, 0.01f);
            var ry = Mathf.Max(radiusY * _scale, 0.01f);
            var reach = Mathf.Max(rx, ry);
            var cos = Mathf.Cos(-angleRadians);
            var sin = Mathf.Sin(-angleRadians);
            Raster(c - new Vector2(reach, reach), c + new Vector2(reach, reach), p =>
            {
                var d = p - c;
                var q = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
                var k0 = new Vector2(q.x / rx, q.y / ry).magnitude;
                var k1 = new Vector2(q.x / (rx * rx), q.y / (ry * ry)).magnitude;
                return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(rx, ry);
            });
        }

        /// <summary>多角形（点は順に並べる。向きはどちらでもよい）。</summary>
        public void Polygon(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 3)
            {
                return;
            }

            _points.Clear();
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < points.Count; i++)
            {
                var p = ToPixel(points[i]);
                _points.Add(p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            var verts = _points;
            var n = verts.Count;
            Raster(min, max, p =>
            {
                var d = (p - verts[0]).sqrMagnitude;
                var sign = 1f;
                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    var e = verts[j] - verts[i];
                    var w = p - verts[i];
                    var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
                    d = Mathf.Min(d, b.sqrMagnitude);
                    var c1 = p.y >= verts[i].y;
                    var c2 = p.y < verts[j].y;
                    var c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    {
                        sign = -sign;
                    }
                }

                return sign * Mathf.Sqrt(d);
            });
        }

        public static Vector2 Quadratic(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            var m = 1f - t;
            return m * m * a + 2f * m * t * control + t * t * b;
        }

        /// <summary>2次ベジェ曲線を点の列にして list に足す（始点は足さない）。</summary>
        public static void AddQuadratic(List<Vector2> list, Vector2 from, Vector2 control, Vector2 to, int steps = 8)
        {
            for (var i = 1; i <= steps; i++)
            {
                list.Add(Quadratic(from, control, to, i / (float)steps));
            }
        }

        private Vector2 ToPixel(Vector2 p)
        {
            return new Vector2(_originX + p.x * _scale, _originY + p.y * _scale);
        }

        private void Raster(Vector2 min, Vector2 max, System.Func<Vector2, float> sdf)
        {
            var x0 = Mathf.Max(0, Mathf.FloorToInt(min.x) - _margin);
            var y0 = Mathf.Max(0, Mathf.FloorToInt(min.y) - _margin);
            var x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(max.x) + _margin);
            var y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(max.y) + _margin);
            for (var y = y0; y <= y1; y++)
            {
                var row = y * Width;
                for (var x = x0; x <= x1; x++)
                {
                    var d = sdf(new Vector2(x + 0.5f, y + 0.5f));
                    if (d < _distance[row + x])
                    {
                        _distance[row + x] = d;
                    }
                }
            }
        }
    }
}
