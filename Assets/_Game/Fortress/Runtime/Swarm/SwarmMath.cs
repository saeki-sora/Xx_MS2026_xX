using Unity.Collections;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>空間ハッシュ・経路グリッドの座標変換とサンプリング。全てBurstから呼べる純粋関数。</summary>
    public static class SwarmMath
    {
        public static int2 HashCell(float2 p, float2 origin, float invCell, int width, int height)
        {
            return new int2(
                math.clamp((int)math.floor((p.x - origin.x) * invCell), 0, width - 1),
                math.clamp((int)math.floor((p.y - origin.y) * invCell), 0, height - 1));
        }

        public static int NearestIndex(float2 p, float2 origin, float cell, int width, int height)
        {
            var x = math.clamp((int)math.floor((p.x - origin.x) / cell), 0, width - 1);
            var y = math.clamp((int)math.floor((p.y - origin.y) / cell), 0, height - 1);
            return y * width + x;
        }

        /// <summary>周囲4セルを双線形補間した値（正規化しない）。</summary>
        public static float2 SampleVector(
            NativeArray<float2> data, int offset, float2 p, float2 origin, float cell, int width, int height)
        {
            var fx = (p.x - origin.x) / cell - 0.5f;
            var fy = (p.y - origin.y) / cell - 0.5f;
            var x0 = (int)math.floor(fx);
            var y0 = (int)math.floor(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            var xa = math.clamp(x0, 0, width - 1);
            var xb = math.clamp(x0 + 1, 0, width - 1);
            var ya = math.clamp(y0, 0, height - 1);
            var yb = math.clamp(y0 + 1, 0, height - 1);

            var v00 = data[offset + ya * width + xa];
            var v10 = data[offset + ya * width + xb];
            var v01 = data[offset + yb * width + xa];
            var v11 = data[offset + yb * width + xb];
            return math.lerp(math.lerp(v00, v10, tx), math.lerp(v01, v11, tx), ty);
        }

        public static float2 SampleDirection(
            NativeArray<float2> data, int offset, float2 p, float2 origin, float cell, int width, int height)
        {
            return math.normalizesafe(SampleVector(data, offset, p, origin, cell, width, height));
        }

        public static float SampleScalar(
            NativeArray<float> data, float2 p, float2 origin, float cell, int width, int height)
        {
            var fx = (p.x - origin.x) / cell - 0.5f;
            var fy = (p.y - origin.y) / cell - 0.5f;
            var x0 = (int)math.floor(fx);
            var y0 = (int)math.floor(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            var xa = math.clamp(x0, 0, width - 1);
            var xb = math.clamp(x0 + 1, 0, width - 1);
            var ya = math.clamp(y0, 0, height - 1);
            var yb = math.clamp(y0 + 1, 0, height - 1);

            var v00 = data[ya * width + xa];
            var v10 = data[ya * width + xb];
            var v01 = data[yb * width + xa];
            var v11 = data[yb * width + xb];
            return math.lerp(math.lerp(v00, v10, tx), math.lerp(v01, v11, tx), ty);
        }
    }
}
