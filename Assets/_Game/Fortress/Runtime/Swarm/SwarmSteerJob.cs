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
        /// <summary>お供が場所へ寄る強さ（1m 離れていると 1秒に何m で寄るか）。</summary>
        private const float FollowGain = 4f;

        /// <summary>形を見るとき「出っ張り」とみなす、両側の壁の遠のき方の平均（m）。</summary>
        private const float ShapeBulgeMin = 0.05f;

        /// <summary>形を見るとき、左右の差がこれより小さければ（平らな壁の真ん中など）経路どおりにする（m）。</summary>
        private const float ShapeDiffMin = 0.15f;

        [ReadOnly] public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        [WriteOnly] public NativeArray<float2> predOut;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<int> netId;
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

        /// <summary>壁に沿った速さがこの割合を超えたら、経路と逆向きでも勢いのまま壁沿いに滑る。1 でオフ。</summary>
        public float wallMomentumSlide;

        /// <summary>真正面から当たった敵が、壁沿いに左右どこまで先の形を見るか（ワールド単位）。0 でオフ。</summary>
        public float wallShapeProbe;

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

        /// <summary>1 なら撤退中（ウェーブの目標を倒した後）: コアではなく retreatDirs の道で、画面の外へ速く逃げる。張り付いていた敵も動く。</summary>
        public int retreat;

        public float retreatSpeed;

        /// <summary>逃げる道（経路のマップと同じマス目。1つ分）。retreatHasField が 0 ならコアの反対へまっすぐ。</summary>
        [ReadOnly] public NativeArray<float2> retreatDirs;

        public int retreatHasField;

        /// <summary>盾持ちのお供の行き先（敵の番号 → 場所）。入っているお供は経路ではなく、ここへ向かう。</summary>
        [ReadOnly] public NativeHashMap<int, float2> followTargets;

        public float dt;

        /// <summary>今の時刻（秒）。螺旋の敵がどこまで回ったかを、敵ごとの番号と合わせて決める。</summary>
        public float time;

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

            if (retreat != 0)
            {
                var away = retreatHasField != 0
                    ? SwarmMath.SampleDirection(retreatDirs, 0, p, navOrigin, navCell, navW, navH)
                    : float2.zero;
                if (math.lengthsq(away) < 1e-6f)
                {
                    away = -NearestGoalDirection(p);
                }

                var terrainR = speedMul[SwarmMath.NearestIndex(p, navOrigin, navCell, navW, navH)];
                desired = away * (tp.maxSpeed * speedScale[i] * terrainR * retreatSpeed);
            }
            else if (tp.followsShield != 0 && state[i] == 0 && followTargets.TryGetValue(i, out var slot))
            {
                // 盾持ちのお供: 決まった場所へ。近づくほど遅くして、場所の上で止まる（押し合いでずれても戻る）。
                var to = slot - p;
                var distance = math.length(to);
                var speed = math.min(tp.maxSpeed * speedScale[i], distance * FollowGain);
                desired = distance > 1e-4f ? to / distance * speed : float2.zero;
            }
            else if (state[i] == 0)
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
                if (contact > 0f && (wallMomentumSlide < 1f || wallShapeProbe > 0f))
                {
                    flow = MomentumSlide(p, vel[i], flow);
                }

                var follow = math.lerp(obstacleAwareness, 1f, contact);
                var heading = math.normalizesafe(math.lerp(direct, flow, follow), flow);
                flow = math.lengthsq(direct) < 1e-6f ? flow : heading;

                var terrain = speedMul[SwarmMath.NearestIndex(p, navOrigin, navCell, navW, navH)];
                var crowd = math.saturate(CountNeighbors(i, p) / densityReference);
                var slow = math.max(minSpeedFactor, 1f - densitySlowdown * crowd);
                desired = flow * tp.maxSpeed;
                if (tp.spiralRadius > 0f)
                {
                    desired += SwarmMath.SpiralVelocity(tp, netId[i], time);
                }

                desired *= speedScale[i] * terrain * slow;
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

        /// <summary>
        /// 壁際で、ぶつかった勢いのまま壁沿いに滑る向きを返す（流体っぽさ）。経路は「いちばん短い道」なので、
        /// 斜面に当たっても坂を登って近い側へ回ろうとする。勢いが壁に沿ってはっきり片側へ向いていて、
        /// 経路がその逆向きに壁沿いを指すときだけ、勢いの方へ滑らせる。
        /// 真正面から当たった敵は、当たった所の形を見て、出っ張りの頂上からずれた側（壁が遠のく側）へ滑る。
        /// 経路が壁から離れる向きを指す所（障害物の角を回り終えた所）では経路に戻るので、行き止まりで止まり続けない。
        /// </summary>
        private float2 MomentumSlide(float2 p, float2 v, float2 flow)
        {
            var normal = math.normalizesafe(SwarmMath.SampleVector(sdfGradient, 0, p, navOrigin, navCell, navW, navH));
            if (math.dot(flow, normal) >= 0.5f || math.lengthsq(normal) < 0.5f)
            {
                return flow; // 経路が壁から離れる向き（角を回り終えた所）なら経路どおり
            }

            var tangent = new float2(-normal.y, normal.x);
            var speed = math.length(v);
            var along = math.dot(v, tangent);
            var side = 0f;

            if (wallMomentumSlide < 1f && speed > 1e-3f && math.abs(along) > wallMomentumSlide * speed)
            {
                side = math.sign(along); // 勢いが壁沿いに片側へ向いている
            }
            else if (wallShapeProbe > 0f)
            {
                // 真正面寄り: 壁沿いに左右少し先の「壁までの距離」を比べる。出っ張り（両側とも壁が遠のく）のときだけ、遠のき方の大きい側へ。
                // くぼみ（両側とも壁が近づく）では使わない。くぼみの真ん中へ集まって出られなくなるのを防ぐ。
                var ahead = SwarmMath.SampleScalar(sdf, p + tangent * wallShapeProbe, navOrigin, navCell, navW, navH);
                var behind = SwarmMath.SampleScalar(sdf, p - tangent * wallShapeProbe, navOrigin, navCell, navW, navH);
                var here = SwarmMath.SampleScalar(sdf, p, navOrigin, navCell, navW, navH);
                var bulge = (ahead + behind) * 0.5f - here;
                if (bulge > ShapeBulgeMin && math.abs(ahead - behind) > ShapeDiffMin)
                {
                    side = math.sign(ahead - behind);
                }
            }

            return side != 0f && math.dot(flow, tangent) * side < 0f ? tangent * side : flow;
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
