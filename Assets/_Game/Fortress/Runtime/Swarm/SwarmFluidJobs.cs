using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 押し合いの方式「流体（圧力）」（<see cref="SwarmCrowdSolver.Fluid"/>）の計算部分（Unityの部品に触らない純粋な計算。テストあり）。
    /// 1体ずつ近くの敵を調べる代わりに、マスごとの「体で埋まっている割合（混み具合）」から、混んでいる所 → 空いている所へ押す。
    /// </summary>
    public static class SwarmFluidMath
    {
        /// <summary>混み具合のうち、これ以上は詰め込めない分（押し返す量）。<paramref name="rest"/> 以下なら0。</summary>
        public static float Excess(float occupancy, float rest)
        {
            return math.max(0f, occupancy - rest);
        }

        /// <summary>
        /// 1フレームに押す量（メートル）。押し返す量の勾配（1メートルあたり）の逆向きに押し、重い敵ほど動きにくい。
        /// 押す強さはマスの大きさとフレームの長さ（60fpsで1倍）に合わせる。<paramref name="maxStep"/> を超えないよう切り詰める。
        /// </summary>
        public static float2 Push(float2 excessGradient, float pressure, float cellSize, float frameScale, float mass, float maxStep)
        {
            var push = -excessGradient * (pressure * cellSize * cellSize * frameScale / math.max(0.05f, mass));
            var length = math.length(push);
            if (length > maxStep && length > 1e-6f)
            {
                push *= maxStep / length;
            }

            return push;
        }
    }

    /// <summary>
    /// 【流体】マスごとの混み具合 = そのマスにいる敵の体の面積の合計 ÷ マスの面積（空間ハッシュのマスをそのまま使う）。
    /// 1マスずつ並列に計算する（1体ずつ近くを調べるより、全体で1回なぞるだけなので軽い）。
    /// </summary>
    [BurstCompile]
    public struct SwarmOccupancyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [WriteOnly] public NativeArray<float> occupancy;
        public float invCellArea;
        public float personalSpace;

        public void Execute(int cell)
        {
            var start = cellStart[cell];
            var end = cellStart[cell + 1];
            var area = 0f;
            for (var k = start; k < end; k++)
            {
                var r = types[typeIdx[cellItems[k]]].radius * personalSpace;
                area += math.PI * r * r;
            }

            occupancy[cell] = area * invCellArea;
        }
    }

    /// <summary>
    /// 【流体】混み具合の勾配に沿って、混んでいる所から空いている所へ押す（predIn → predOut）。壁へのめり込みも押し戻す。
    /// コアに張り付いた敵は動かない（混み具合には数えるので、まわりの敵が押し返される）。
    /// 近くの重なりの仕上げは、この後の 1体ずつの押し合い（<see cref="SwarmSeparationJob"/>、回数は設定）が行う。
    /// </summary>
    [BurstCompile]
    public struct SwarmPressureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> predIn;
        [WriteOnly] public NativeArray<float2> predOut;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;

        [ReadOnly] public NativeArray<float> occupancy;
        public float2 gridOrigin;
        public float gridCell;
        public int gridW;
        public int gridH;

        [ReadOnly] public NativeArray<float> sdf;
        [ReadOnly] public NativeArray<float2> sdfGradient;
        public float2 navOrigin;
        public float navCell;
        public int navW;
        public int navH;

        public float pressure;
        public float restOccupancy;
        public float frameScale;
        public float maxCorrectionRatio;
        public float wallStiffness;

        public void Execute(int i)
        {
            var p = predIn[i];
            if (state[i] == 1)
            {
                predOut[i] = p;
                return;
            }

            var tp = types[typeIdx[i]];
            var h = gridCell * 0.5f;
            var dx = Excess(p + new float2(h, 0f)) - Excess(p - new float2(h, 0f));
            var dy = Excess(p + new float2(0f, h)) - Excess(p - new float2(0f, h));
            var gradient = new float2(dx, dy) / (2f * h);
            var sum = SwarmFluidMath.Push(gradient, pressure, gridCell, frameScale, tp.mass, tp.radius * maxCorrectionRatio);

            var wallDistance = SwarmMath.SampleScalar(sdf, p, navOrigin, navCell, navW, navH);
            if (wallDistance < tp.radius)
            {
                var normal = math.normalizesafe(SwarmMath.SampleVector(sdfGradient, 0, p, navOrigin, navCell, navW, navH));
                var push = math.min((tp.radius - wallDistance) * wallStiffness, navCell);
                sum += normal * push;
            }

            predOut[i] = p + sum;
        }

        private float Excess(float2 q)
        {
            return SwarmFluidMath.Excess(SwarmMath.SampleScalar(occupancy, q, gridOrigin, gridCell, gridW, gridH), restOccupancy);
        }
    }
}
