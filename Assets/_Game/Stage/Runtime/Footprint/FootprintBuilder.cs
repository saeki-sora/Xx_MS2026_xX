using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 3Dモデルの三角形から「敵が通れない範囲（床の上の輪郭）」を作る。
    /// 1. 床から blockHeight までの高さに入っている部分だけを切り出し、真上から見てマス目に塗る
    /// 2. 穴埋め → 狭い隙間埋め → 外側へ広げる
    /// 3. 境界をたどって輪郭にし、頂点を間引く
    /// 座標は「背景オブジェクトの足元」から見たもの（XYが床、-Zが上）。
    /// </summary>
    public static class FootprintBuilder
    {
        /// <summary>マス目の一辺の最大数。これを超えるときはマスを粗くする（大きなモデルで固まらないように）。</summary>
        public const int MaxCellsPerSide = 512;

        private const int MarginCells = 2;

        /// <summary>作った結果の統計（ツールの表示用）。</summary>
        public struct Report
        {
            public int TrianglesUsed;
            public int CellsFilled;
            public float CellSize;
            public int VertexCount;
            public float Area;
        }

        public static List<Vector2[]> Build(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles,
            FootprintSettings settings,
            out Report report)
        {
            settings = settings.Sanitized();
            report = default;

            var clipped = ClipToSlab(vertices, triangles, settings.blockHeight);
            report.TrianglesUsed = clipped.Count;
            if (clipped.Count == 0)
            {
                return new List<Vector2[]>();
            }

            var grid = CreateGrid(clipped, settings);
            report.CellSize = grid.CellSize;

            foreach (var polygon in clipped)
            {
                grid.FillConvex(polygon);
            }

            if (settings.fillHoles)
            {
                grid.FillEnclosedHoles();
            }

            if (settings.closeGaps > 0f)
            {
                grid.Close(settings.closeGaps);
                if (settings.fillHoles)
                {
                    grid.FillEnclosedHoles();
                }
            }

            grid.Dilate(settings.padding);
            report.CellsFilled = grid.FilledCount;

            var result = new List<Vector2[]>();
            foreach (var loop in FootprintContour.Trace(grid))
            {
                var simplified = FootprintContour.Simplify(loop, settings.simplify);
                var area = FootprintContour.SignedArea(simplified);
                if (Mathf.Abs(area) < settings.minArea)
                {
                    continue;
                }

                result.Add(simplified);
                report.VertexCount += simplified.Length;
                report.Area += area;
            }

            return result;
        }

        /// <summary>各三角形を「高さ0未満は床に含める・blockHeightより上は捨てる」で切り、真上から見た凸多角形にする。</summary>
        private static List<List<Vector2>> ClipToSlab(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, float blockHeight)
        {
            var result = new List<List<Vector2>>();
            var input = new List<Vector3>(3);
            var output = new List<Vector3>(4);

            for (var t = 0; t + 2 < triangles.Count; t += 3)
            {
                input.Clear();
                input.Add(vertices[triangles[t]]);
                input.Add(vertices[triangles[t + 1]]);
                input.Add(vertices[triangles[t + 2]]);

                ClipBelowHeight(input, output, blockHeight);
                if (output.Count == 0)
                {
                    continue;
                }

                var flat = new List<Vector2>(output.Count);
                foreach (var p in output)
                {
                    flat.Add(new Vector2(p.x, p.y));
                }

                result.Add(flat);
            }

            return result;
        }

        /// <summary>高さ（= -z）が limit 以下の部分だけを残す（Sutherland–Hodgman を1平面で）。</summary>
        private static void ClipBelowHeight(List<Vector3> input, List<Vector3> output, float limit)
        {
            output.Clear();
            for (var i = 0; i < input.Count; i++)
            {
                var a = input[i];
                var b = input[(i + 1) % input.Count];
                var ha = -a.z;
                var hb = -b.z;
                var aInside = ha <= limit;
                var bInside = hb <= limit;

                if (aInside)
                {
                    output.Add(a);
                }

                if (aInside != bInside)
                {
                    var t = (limit - ha) / (hb - ha);
                    output.Add(Vector3.Lerp(a, b, t));
                }
            }
        }

        private static FootprintGrid CreateGrid(List<List<Vector2>> polygons, FootprintSettings settings)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var polygon in polygons)
            {
                foreach (var p in polygon)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }

            var grow = settings.padding + settings.closeGaps;
            var size = max - min + Vector2.one * (grow * 2f);
            var cell = Mathf.Max(settings.resolution, Mathf.Max(size.x, size.y) / (MaxCellsPerSide - MarginCells * 2));
            var margin = cell * MarginCells + grow;
            var origin = min - Vector2.one * margin;
            var width = Mathf.CeilToInt((max.x - min.x + margin * 2f) / cell) + 1;
            var height = Mathf.CeilToInt((max.y - min.y + margin * 2f) / cell) + 1;
            return new FootprintGrid(origin, cell, width, height);
        }
    }
}
