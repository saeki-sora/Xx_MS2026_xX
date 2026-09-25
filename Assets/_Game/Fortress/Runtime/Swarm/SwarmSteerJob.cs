using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 各敵の「行きたい速度」を決めて速度を更新し、押し合い計算の出発点となる予測位置を出す。
    /// 進行方向=経路のフロー・フィールド、速度=混み具合で減速。
    /// </summary>
    [BurstCompile]
    public struct SwarmSteerJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        [WriteOnly] public NativeArray<float2> predOut;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;

        [ReadOnly] public NativeArray<float2> flowDirs;
        [ReadOnly] public NativeArray<float> speedMul;
        [ReadOnly] public NativeArray<float2> goals;
        public int goalCount;

        [ReadOnly] public NativeArray<float> sdf;
        [ReadOnly] public NativeArray<float2> sdfGradient;
        public float obstacleAwareness;
        public float wallContactRange;
        public float wallSlide;
        public float2 navOrigin;
        public float navCell;
        public int navW;
        public int navH;
        public int navCellCount;

        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        public float2 hashOrigin;
        public float hashInvCell;
        public int hashW;
        public int hashH;

        public float dt;
        public float densityRadiusSq;
        public float densityReference;
        public float densitySlowdown;
        public float minSpeedFactor;
        public int maxNeighbors;

        public void Execute(int i)
        {
            var p = pos[i];
            var tp = types[typeIdx[i]];
            var desired = float2.zero;

            if (state[i] == 0)
            {
                var flow = SwarmMath.SampleDirection(
                    flowDirs, tp.profileIndex * navCellCount, p, navOrigin, navCell, navW, navH);
                var direct = NearestGoalDirection(p);

                if (math.lengthsq(flow) < 1e-6f)
                {
                    flow = direct;
                }

                // 遠くでは障害物を無視してコアへ一直線、壁に近づくほど経路（回り込む向き）に従う。
                // こうすると壁にぶつかってから、押し合いながら壁沿いに流れて回り込む。
                var wallDistance = SwarmMath.SampleScalar(sdf, p, navOrigin, navCell, navW, navH);
                var contact = 1f - math.saturate((wallDistance - tp.radius) / wallContactRange);
                var follow = math.lerp(obstacleAwareness, 1f, contact);
                var heading = math.normalizesafe(math.lerp(direct, flow, follow), flow);
                flow = math.lengthsq(direct) < 1e-6f ? flow : heading;

                var terrain = speedMul[SwarmMath.NearestIndex(p, navOrigin, navCell, navW, navH)];
                var crowd = math.saturate(CountNeighbors(i, p) / densityReference);
                var slow = math.max(minSpeedFactor, 1f - densitySlowdown * crowd);
                desired = flow * (tp.maxSpeed * speedScale[i] * terrain * slow);
            }

            var blend = 1f - math.exp(-tp.acceleration * dt);
            var v = math.lerp(vel[i], desired, blend);

            // 壁に向かう速度成分を消して、壁に張り付かず滑らせる。
            var nearWall = SwarmMath.SampleScalar(sdf, p, navOrigin, navCell, navW, navH);
            if (nearWall < tp.radius * 1.5f)
            {
                var normal = math.normalizesafe(SwarmMath.SampleVector(sdfGradient, 0, p, navOrigin, navCell, navW, navH));
                var into = math.dot(v, normal);
                if (into < 0f)
                {
                    v -= normal * (into * wallSlide);
                }
            }

            vel[i] = v;
            predOut[i] = p + v * dt;
        }

        private float2 NearestGoalDirection(float2 p)
        {
            if (goalCount <= 0)
            {
                return float2.zero;
            }

            var best = goals[0];
            var bestDist = math.distancesq(p, best);
            for (var g = 1; g < goalCount; g++)
            {
                var d = math.distancesq(p, goals[g]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = goals[g];
                }
            }

            return math.normalizesafe(best - p);
        }

        private float CountNeighbors(int self, float2 p)
        {
            var cell = SwarmMath.HashCell(p, hashOrigin, hashInvCell, hashW, hashH);
            var count = 0;
            var budget = maxNeighbors > 0 ? maxNeighbors : int.MaxValue;

            for (var y = math.max(0, cell.y - 1); y <= math.min(hashH - 1, cell.y + 1) && budget > 0; y++)
            {
                for (var x = math.max(0, cell.x - 1); x <= math.min(hashW - 1, cell.x + 1) && budget > 0; x++)
                {
                    var c = y * hashW + x;
                    var start = cellStart[c];
                    var n = cellStart[c + 1] - start;
                    // 上限に達しても特定の敵ばかり調べないよう、セル内の走査開始位置を自分の番号でずらす。
                    var offset = n > 0 ? self % n : 0;
                    for (var m = 0; m < n && budget > 0; m++)
                    {
                        var k = start + m + offset;
                        if (k >= start + n)
                        {
                            k -= n;
                        }

                        var j = cellItems[k];
                        if (j == self)
                        {
                            continue;
                        }

                        budget--;
                        if (math.distancesq(p, pos[j]) < densityRadiusSq)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }
    }
}
