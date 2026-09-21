using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵同士の重なりと壁へのめり込みを押し戻す（Position Based Dynamics）。
    /// 重い敵ほど動かされにくく、コアに張り付いた敵は動かない壁として扱う。
    /// predIn → predOut に書くので、複数回繰り返すときは入出力を入れ替えて呼ぶ。
    /// </summary>
    [BurstCompile]
    public struct SwarmSeparationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> predIn;
        [WriteOnly] public NativeArray<float2> predOut;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;

        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        public float2 hashOrigin;
        public float hashInvCell;
        public int hashW;
        public int hashH;

        [ReadOnly] public NativeArray<float> sdf;
        [ReadOnly] public NativeArray<float2> sdfGradient;
        public float2 navOrigin;
        public float navCell;
        public int navW;
        public int navH;

        public float stiffness;
        public float personalSpace;
        public float maxCorrectionRatio;
        public float wallStiffness;
        public int maxNeighbors;

        private const float FixedMass = 1e6f;

        public void Execute(int i)
        {
            var p = predIn[i];
            var tp = types[typeIdx[i]];
            var isFixed = state[i] == 1;
            if (isFixed)
            {
                predOut[i] = p;
                return;
            }

            var sum = float2.zero;
            var cell = SwarmMath.HashCell(p, hashOrigin, hashInvCell, hashW, hashH);
            var budget = maxNeighbors > 0 ? maxNeighbors : int.MaxValue;

            for (var y = math.max(0, cell.y - 1); y <= math.min(hashH - 1, cell.y + 1) && budget > 0; y++)
            {
                for (var x = math.max(0, cell.x - 1); x <= math.min(hashW - 1, cell.x + 1) && budget > 0; x++)
                {
                    var c = y * hashW + x;
                    var start = cellStart[c];
                    var n = cellStart[c + 1] - start;
                    var offset = n > 0 ? i % n : 0;
                    for (var m = 0; m < n && budget > 0; m++)
                    {
                        var k = start + m + offset;
                        if (k >= start + n)
                        {
                            k -= n;
                        }

                        var j = cellItems[k];
                        if (j == i)
                        {
                            continue;
                        }

                        budget--;

                        var diff = p - predIn[j];
                        var distSq = math.lengthsq(diff);
                        var other = types[typeIdx[j]];
                        var minDist = (tp.radius + other.radius) * personalSpace;
                        if (distSq >= minDist * minDist)
                        {
                            continue;
                        }

                        var dist = math.sqrt(distSq);
                        float2 dir;
                        if (dist < 1e-4f)
                        {
                            // 完全に重なっているときは、ペアごとに決まる向きで散らす。
                            math.sincos(i * 12.9898f + j * 78.233f, out var s, out var co);
                            dir = new float2(co, s);
                        }
                        else
                        {
                            dir = diff / dist;
                        }

                        var otherMass = state[j] == 1 ? FixedMass : other.mass;
                        var share = otherMass / (tp.mass + otherMass);
                        sum += dir * ((minDist - dist) * share * stiffness);
                    }
                }
            }

            var maxCorrection = tp.radius * maxCorrectionRatio;
            var length = math.length(sum);
            if (length > maxCorrection)
            {
                sum *= maxCorrection / length;
            }

            var wallDistance = SwarmMath.SampleScalar(sdf, p, navOrigin, navCell, navW, navH);
            if (wallDistance < tp.radius)
            {
                var normal = math.normalizesafe(SwarmMath.SampleVector(sdfGradient, 0, p, navOrigin, navCell, navW, navH));
                var push = math.min((tp.radius - wallDistance) * wallStiffness, navCell);
                sum += normal * push;
            }

            predOut[i] = p + sum;
        }
    }
}
