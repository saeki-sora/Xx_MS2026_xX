using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 床の上のマス目（塗られている＝通れない）。三角形を塗り、穴埋め・隙間埋め・広げるの加工をする。
    /// 純粋な計算クラス（UnityEngineはVector2/Mathfのみ使用）なので単体テストできる。
    /// </summary>
    public sealed class FootprintGrid
    {
        private const float Far = 1e6f;

        // 面のある形は、マスを少し縮めて重なりを調べる（輪郭がマス1つ分太らないように）。
        private const float CellProbeHalfExtent = 0.35f;

        // 細い形（真上から見ると線や細長い帯になる縦の面）は、マスいっぱいで調べて取りこぼさないようにする。
        private const float ThinProbeHalfExtent = 0.5f;

        public readonly Vector2 Origin;
        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        private bool[] _filled;

        public FootprintGrid(Vector2 origin, float cellSize, int width, int height)
        {
            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Height = height;
            _filled = new bool[width * height];
        }

        public bool this[int x, int y] => x >= 0 && y >= 0 && x < Width && y < Height && _filled[y * Width + x];

        public int FilledCount
        {
            get
            {
                var count = 0;
                foreach (var f in _filled)
                {
                    if (f)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public Vector2 CellCenter(int x, int y) => Origin + new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

        /// <summary>凸多角形（三角形を高さで切った形、最大5頂点）が重なるマスを塗る。面積ゼロ（線）でも塗る。</summary>
        public void FillConvex(IReadOnlyList<Vector2> polygon)
        {
            if (polygon.Count == 0)
            {
                return;
            }

            var min = polygon[0];
            var max = polygon[0];
            for (var i = 1; i < polygon.Count; i++)
            {
                min = Vector2.Min(min, polygon[i]);
                max = Vector2.Max(max, polygon[i]);
            }

            var x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - Origin.x) / CellSize));
            var y0 = Mathf.Max(0, Mathf.FloorToInt((min.y - Origin.y) / CellSize));
            var x1 = Mathf.Min(Width - 1, Mathf.FloorToInt((max.x - Origin.x) / CellSize));
            var y1 = Mathf.Min(Height - 1, Mathf.FloorToInt((max.y - Origin.y) / CellSize));
            var half = CellSize * (IsThin(polygon, CellSize) ? ThinProbeHalfExtent : CellProbeHalfExtent);

            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (!_filled[y * Width + x] && ConvexOverlapsBox(polygon, CellCenter(x, y), half))
                    {
                        _filled[y * Width + x] = true;
                    }
                }
            }
        }

        /// <summary>外側から辿り着けない空きマス（輪郭の内側の穴）を塗る。</summary>
        public void FillEnclosedHoles()
        {
            var reached = new bool[_filled.Length];
            var stack = new Stack<int>();

            void Seed(int x, int y)
            {
                var i = y * Width + x;
                if (!_filled[i] && !reached[i])
                {
                    reached[i] = true;
                    stack.Push(i);
                }
            }

            for (var x = 0; x < Width; x++)
            {
                Seed(x, 0);
                Seed(x, Height - 1);
            }

            for (var y = 0; y < Height; y++)
            {
                Seed(0, y);
                Seed(Width - 1, y);
            }

            while (stack.Count > 0)
            {
                var i = stack.Pop();
                var x = i % Width;
                var y = i / Width;
                if (x > 0) Seed(x - 1, y);
                if (x < Width - 1) Seed(x + 1, y);
                if (y > 0) Seed(x, y - 1);
                if (y < Height - 1) Seed(x, y + 1);
            }

            for (var i = 0; i < _filled.Length; i++)
            {
                if (!reached[i])
                {
                    _filled[i] = true;
                }
            }
        }

        /// <summary>塗られた範囲を distance だけ外へ広げる。</summary>
        public void Dilate(float distance)
        {
            if (distance <= 0f)
            {
                return;
            }

            var toFilled = DistanceTo(true);
            for (var i = 0; i < _filled.Length; i++)
            {
                _filled[i] = toFilled[i] <= distance + CellSize * 0.01f;
            }
        }

        /// <summary>塗られた範囲を distance だけ内へ縮める。</summary>
        public void Erode(float distance)
        {
            if (distance <= 0f)
            {
                return;
            }

            var toEmpty = DistanceTo(false);
            for (var i = 0; i < _filled.Length; i++)
            {
                _filled[i] = toEmpty[i] > distance + CellSize * 0.01f;
            }
        }

        /// <summary>幅 width より狭い隙間・くぼみを埋める（広げてから同じだけ縮める）。</summary>
        public void Close(float width)
        {
            var radius = width * 0.5f;
            Dilate(radius);
            Erode(radius);
        }

        /// <summary>各マスから「target状態のマス」までの近似距離（2パスのチャンファー変換、斜めは√2）。範囲外は空きとみなす。</summary>
        private float[] DistanceTo(bool target)
        {
            var d = new float[_filled.Length];
            for (var i = 0; i < d.Length; i++)
            {
                d[i] = _filled[i] == target ? 0f : Far;
            }

            var straight = CellSize;
            var diagonal = CellSize * 1.4142135f;
            var outside = target ? Far : 0f;

            float At(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? outside : d[y * Width + x];

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var i = y * Width + x;
                    var v = d[i];
                    v = Mathf.Min(v, At(x - 1, y) + straight);
                    v = Mathf.Min(v, At(x, y - 1) + straight);
                    v = Mathf.Min(v, At(x - 1, y - 1) + diagonal);
                    v = Mathf.Min(v, At(x + 1, y - 1) + diagonal);
                    d[i] = v;
                }
            }

            for (var y = Height - 1; y >= 0; y--)
            {
                for (var x = Width - 1; x >= 0; x--)
                {
                    var i = y * Width + x;
                    var v = d[i];
                    v = Mathf.Min(v, At(x + 1, y) + straight);
                    v = Mathf.Min(v, At(x, y + 1) + straight);
                    v = Mathf.Min(v, At(x + 1, y + 1) + diagonal);
                    v = Mathf.Min(v, At(x - 1, y + 1) + diagonal);
                    d[i] = v;
                }
            }

            return d;
        }

        /// <summary>多角形の「だいたいの幅」（面積÷一番長い辺）がマスより細いか。</summary>
        private static bool IsThin(IReadOnlyList<Vector2> polygon, float cellSize)
        {
            var area = 0f;
            var longest = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
                longest = Mathf.Max(longest, (b - a).magnitude);
            }

            return longest <= 0f || Mathf.Abs(area) * 0.5f / longest < cellSize;
        }

        /// <summary>分離軸による「凸多角形（線・点に潰れていても可）」と「軸に沿った正方形」の重なり判定。</summary>
        private static bool ConvexOverlapsBox(IReadOnlyList<Vector2> polygon, Vector2 center, float half)
        {
            var min = polygon[0];
            var max = polygon[0];
            for (var i = 1; i < polygon.Count; i++)
            {
                min = Vector2.Min(min, polygon[i]);
                max = Vector2.Max(max, polygon[i]);
            }

            if (max.x < center.x - half || min.x > center.x + half || max.y < center.y - half || min.y > center.y + half)
            {
                return false;
            }

            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var edge = b - a;
                if (edge.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                var axis = new Vector2(-edge.y, edge.x);
                var polyMin = float.PositiveInfinity;
                var polyMax = float.NegativeInfinity;
                for (var j = 0; j < polygon.Count; j++)
                {
                    var p = Vector2.Dot(polygon[j], axis);
                    polyMin = Mathf.Min(polyMin, p);
                    polyMax = Mathf.Max(polyMax, p);
                }

                var c = Vector2.Dot(center, axis);
                var r = half * (Mathf.Abs(axis.x) + Mathf.Abs(axis.y));
                if (polyMax < c - r || polyMin > c + r)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
