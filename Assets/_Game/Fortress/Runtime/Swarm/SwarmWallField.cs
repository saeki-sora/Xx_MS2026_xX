using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 通行不可セルから「壁までの符号付き距離」と、壁から離れる向き（勾配）を作る。
    /// 敵が壁にめり込んだとき、この場をたどって押し出す。破壊・再生で経路グリッドが変わるたびに作り直す。
    /// </summary>
    public static class SwarmWallField
    {
        private const float FarDistance = 1000f;

        public static void Build(NavigationGridData grid, NativeArray<float> sdf, NativeArray<float2> gradient)
        {
            var width = grid.Width;
            var height = grid.Height;
            var count = grid.CellCount;
            var cell = grid.CellSize;

            var toBlocked = new float[count];
            var toFree = new float[count];
            for (var i = 0; i < count; i++)
            {
                toBlocked[i] = grid.Blocked[i] ? 0f : FarDistance;
                toFree[i] = grid.Blocked[i] ? FarDistance : 0f;
            }

            Chamfer(toBlocked, width, height, cell);
            Chamfer(toFree, width, height, cell);

            for (var i = 0; i < count; i++)
            {
                sdf[i] = grid.Blocked[i]
                    ? -Mathf.Max(0f, toFree[i] - cell * 0.5f)
                    : Mathf.Min(FarDistance, toBlocked[i] - cell * 0.5f);
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var xa = Mathf.Max(0, x - 1);
                    var xb = Mathf.Min(width - 1, x + 1);
                    var ya = Mathf.Max(0, y - 1);
                    var yb = Mathf.Min(height - 1, y + 1);

                    var gx = sdf[y * width + xb] - sdf[y * width + xa];
                    var gy = sdf[yb * width + x] - sdf[ya * width + x];
                    gradient[y * width + x] = math.normalizesafe(new float2(gx, gy));
                }
            }
        }

        /// <summary>2パスのチャンファー距離変換（斜めは√2倍）。</summary>
        private static void Chamfer(float[] d, int width, int height, float cell)
        {
            var diagonal = cell * 1.4142135f;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    var v = d[i];
                    if (x > 0) v = Mathf.Min(v, d[i - 1] + cell);
                    if (y > 0)
                    {
                        v = Mathf.Min(v, d[i - width] + cell);
                        if (x > 0) v = Mathf.Min(v, d[i - width - 1] + diagonal);
                        if (x < width - 1) v = Mathf.Min(v, d[i - width + 1] + diagonal);
                    }

                    d[i] = v;
                }
            }

            for (var y = height - 1; y >= 0; y--)
            {
                for (var x = width - 1; x >= 0; x--)
                {
                    var i = y * width + x;
                    var v = d[i];
                    if (x < width - 1) v = Mathf.Min(v, d[i + 1] + cell);
                    if (y < height - 1)
                    {
                        v = Mathf.Min(v, d[i + width] + cell);
                        if (x < width - 1) v = Mathf.Min(v, d[i + width + 1] + diagonal);
                        if (x > 0) v = Mathf.Min(v, d[i + width - 1] + diagonal);
                    }

                    d[i] = v;
                }
            }
        }
    }
}
