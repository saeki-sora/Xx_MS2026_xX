using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Net
{
    /// <summary>Host: あるClientへ、ある敵(番号)の位置を最後に送ったときの値。ズレの見込み(予想との差)を求めるのに使う。</summary>
    public struct SwarmSentState
    {
        public float2 pos;
        public float2 vel;
        public float time;
    }

    /// <summary>補正の優先度の重み(段階5)。SwarmNetworkHubのInspectorの値をまとめてジョブへ渡す。</summary>
    public struct SwarmPriorityWeights
    {
        /// <summary>そのClientの画面(＋余白)に映っている敵に足す重み。</summary>
        public float view;

        /// <summary>レーザーの近くの敵に足す重み。</summary>
        public float beam;

        /// <summary>コアの近くの敵に足す重み。</summary>
        public float core;

        /// <summary>ズレの見込みが0でも貯まる分(ワールド単位)。これがあるので、どの敵もいつかは送られる。</summary>
        public float baseError;

        /// <summary>レーザーの太さに足す「近く」の幅(ワールド単位)。</summary>
        public float beamMargin;

        /// <summary>コアからこの距離以内を「近く」とする(ワールド単位)。</summary>
        public float coreRadius;

        /// <summary>同じ敵を続けて送らない最短の間隔(秒)。</summary>
        public float minInterval;

        /// <summary>これ以上送っていない敵は、優先度に関係なく先に送る(秒)。</summary>
        public float maxAge;

        /// <summary>予想に使う経過時間の上限(秒)。長く送っていない敵の予想が際限なく外れないように。</summary>
        public float maxPredictSeconds;
    }

    /// <summary>
    /// 補正の優先度の計算(Unity API非依存・EditModeテスト対象)。
    /// 優先度は「大事さ × (ズレの見込み + baseError)」を時間で貯めたもの(ネットゲームの Priority Accumulator)。
    /// 大事さ = 1 + 画面内 + レーザー付近 + コア付近 の重み。ズレの見込み = 前回送った位置と速度から予想した位置と、今の位置の差。
    /// </summary>
    public static class SwarmCorrectionPriorityMath
    {
        /// <summary>選ばれない(続けて送らない間隔の内側)。</summary>
        public const float NotEligible = -1f;

        /// <summary>
        /// 長く送っていない敵の優先度の底上げ(どんな優先度よりも先に、古い順に送る)。普段の優先度は1秒で数百程度までしか貯まらない。
        /// floatの精度で古さの差が消えないよう、大きすぎない値にして経過時間を ForcedAgeScale 倍して足す。
        /// </summary>
        public const float ForcedBase = 1e5f;

        public const float ForcedAgeScale = 100f;

        public static float Importance(float2 position, float4 view, bool hasView, bool nearBeam, bool nearCore, in SwarmPriorityWeights weights)
        {
            var importance = 1f;
            if (hasView && position.x >= view.x && position.y >= view.y && position.x <= view.z && position.y <= view.w)
            {
                importance += weights.view;
            }

            if (nearBeam)
            {
                importance += weights.beam;
            }

            if (nearCore)
            {
                importance += weights.core;
            }

            return importance;
        }

        /// <summary>前回送った位置と速度から予想した今の位置と、実際の位置の差。</summary>
        public static float PredictionError(float2 position, in SwarmSentState sent, float age, float maxPredictSeconds)
        {
            var predicted = sent.pos + sent.vel * math.min(age, maxPredictSeconds);
            return math.length(position - predicted);
        }

        /// <summary>
        /// このフレームの選びやすさ。続けて送らない間隔の内側なら NotEligible、長く送っていなければ ForcedBase+経過時間×ForcedAgeScale、それ以外は貯めた優先度。
        /// </summary>
        public static float Score(float accumulated, float age, in SwarmPriorityWeights weights)
        {
            if (age < weights.minInterval)
            {
                return NotEligible;
            }

            return age >= weights.maxAge ? ForcedBase + age * ForcedAgeScale : accumulated;
        }

        /// <summary>点から線分までの距離の2乗。</summary>
        public static float DistanceSqToSegment(float2 p, float2 a, float2 b)
        {
            var ab = b - a;
            var lengthSq = math.lengthsq(ab);
            var t = lengthSq > 1e-8f ? math.saturate(math.dot(p - a, ab) / lengthSq) : 0f;
            return math.lengthsq(p - (a + ab * t));
        }
    }

    /// <summary>
    /// Host: 1人のClientについて、全員の優先度を貯め、このフレームの選びやすさ(scores、並び順=Storageの順)を出す。
    /// priority/sent は番号(netId)で引く(Storageの並びは毎フレーム詰め直されるため)。番号は重ならないので並列に書いてよい。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionPriorityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<SwarmBeam> beams;
        [ReadOnly] public NativeArray<float2> cores;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<SwarmSentState> sent;
        [NativeDisableParallelForRestriction] public NativeArray<float> priority;
        [WriteOnly] public NativeArray<float> scores;

        public SwarmPriorityWeights weights;
        public float4 view;
        public int hasView;
        public float now;
        public float dt;

        public void Execute(int i)
        {
            var id = netId[i];
            var p = pos[i];
            var s = sent[id];
            var age = now - s.time;

            var nearBeam = false;
            for (var b = 0; b < beams.Length && !nearBeam; b++)
            {
                var beam = beams[b];
                var reach = beam.halfWidth + weights.beamMargin;
                nearBeam = SwarmCorrectionPriorityMath.DistanceSqToSegment(p, beam.origin, beam.end) <= reach * reach;
            }

            var nearCore = false;
            var coreRadiusSq = weights.coreRadius * weights.coreRadius;
            for (var c = 0; c < cores.Length && !nearCore; c++)
            {
                nearCore = math.distancesq(p, cores[c]) <= coreRadiusSq;
            }

            var importance = SwarmCorrectionPriorityMath.Importance(p, view, hasView != 0, nearBeam, nearCore, weights);
            var error = SwarmCorrectionPriorityMath.PredictionError(p, s, age, weights.maxPredictSeconds);
            var accumulated = priority[id] + dt * importance * (error + weights.baseError);
            priority[id] = accumulated;
            scores[i] = SwarmCorrectionPriorityMath.Score(accumulated, age, weights);
        }
    }

    /// <summary>
    /// Host: 1人のClientについて、選びやすさ(scores)の高い順に最大 budget 体を選び、送る形に詰める。
    /// 選んだ敵は「最後に送った値」を記録し、貯めた優先度を0に戻す。
    /// 全体を並べ替えず、上から budget 番目の値をクイックセレクトで求めてから1回で集める(3万体でも軽い)。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionSelectJob : IJob
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<float2> vel;
        [ReadOnly] public NativeArray<float> scores;
        public NativeArray<float> scratch;
        public NativeArray<SwarmSentState> sent;
        public NativeArray<float> priority;
        public NativeList<SwarmNetCorrection> output;
        public NativeList<SwarmNetCorrectionV> outputWithVelocity;

        public int count;
        public int budget;
        public int withVelocity;
        public float now;
        public float positionScale;

        public void Execute()
        {
            output.Clear();
            outputWithVelocity.Clear();

            var eligible = 0;
            for (var i = 0; i < count; i++)
            {
                if (scores[i] >= 0f)
                {
                    scratch[eligible++] = scores[i];
                }
            }

            var take = math.min(budget, eligible);
            if (take <= 0)
            {
                return;
            }

            // 上から take 番目の値。これより大きい物は全て、等しい物は枠が残る分だけ選ぶ。
            var threshold = take >= eligible ? 0f : SelectKthLargest(scratch, eligible, take);
            var aboveCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (scores[i] > threshold)
                {
                    aboveCount++;
                }
            }

            var equalSlots = take - aboveCount;
            for (var i = 0; i < count; i++)
            {
                var score = scores[i];
                if (score < 0f || score < threshold)
                {
                    continue;
                }

                if (score <= threshold)
                {
                    if (equalSlots <= 0)
                    {
                        continue;
                    }

                    equalSlots--;
                }

                Emit(i);
            }
        }

        private void Emit(int i)
        {
            var id = netId[i];
            var p = pos[i];
            var v = vel[i];
            // 速度を送らなくても記録する(Clientも自分で動きを計算するので、予想には今の速度を使うのが近い)。
            sent[id] = new SwarmSentState { pos = p, vel = v, time = now };
            priority[id] = 0f;

            var x = (short)math.clamp(math.round(p.x * positionScale), short.MinValue, short.MaxValue);
            var y = (short)math.clamp(math.round(p.y * positionScale), short.MinValue, short.MaxValue);
            if (withVelocity != 0)
            {
                outputWithVelocity.Add(new SwarmNetCorrectionV
                {
                    Id = (ushort)id,
                    X = x,
                    Y = y,
                    Vx = (ushort)math.f32tof16(v.x),
                    Vy = (ushort)math.f32tof16(v.y)
                });
            }
            else
            {
                output.Add(new SwarmNetCorrection { Id = (ushort)id, X = x, Y = y });
            }
        }

        /// <summary>values[0..length) の中で k 番目に大きい値(k は1始まり)。values の並びは崩れる。</summary>
        public static float SelectKthLargest(NativeArray<float> values, int length, int k)
        {
            var target = k - 1;
            var left = 0;
            var right = length - 1;
            while (left < right)
            {
                // 中央の値を軸に、大きい物を左へ集める(Hoareの分割)。
                var pivot = values[left + ((right - left) >> 1)];
                var i = left;
                var j = right;
                while (i <= j)
                {
                    while (values[i] > pivot)
                    {
                        i++;
                    }

                    while (values[j] < pivot)
                    {
                        j--;
                    }

                    if (i <= j)
                    {
                        var swap = values[i];
                        values[i] = values[j];
                        values[j] = swap;
                        i++;
                        j--;
                    }
                }

                if (target <= j)
                {
                    right = j;
                }
                else if (target >= i)
                {
                    left = i;
                }
                else
                {
                    return values[target];
                }
            }

            return values[target];
        }
    }
}
