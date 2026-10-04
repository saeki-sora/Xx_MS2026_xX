using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// ネット対戦のClient専用。Hostから届いた位置(targets)へ寄せる。2つのやり方がある(renderOnly)。
    ///
    /// renderOnly=1(既定。段階4): 計算上の位置は届いた位置にその場で合わせ、見た目が飛ばないよう、
    ///   ズレの分を「描画だけのずらし(correction)」として持つ。ずらしは毎フレーム一部ずつ消える(=見た目だけ滑らかに追いつく)。
    ///   計算上の位置がすぐHostと一致するので、押し合いが激しい場面でもズレが溜まり続けない。
    /// renderOnly=0(従来): ズレを「詰め残し(correction)」として覚え、計算上の位置へ毎フレーム一部ずつ足し込む。
    ///
    /// どちらも、ズレが大きすぎるとき(snapDistance超)は見た目ごとその場で合わせる。
    /// Hostの位置は届くまでの間に古くなっているので、その敵の今の速度×leadSeconds だけ先へ進めた位置を目標にする。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> vel;
        [ReadOnly] public NativeHashMap<int, float2> targets;
        public NativeArray<float2> pos;
        public NativeArray<float2> correction;

        /// <summary>計測用。このフレームにHostの位置が届いた敵はそのズレの大きさ、届かなかった敵は-1。</summary>
        [WriteOnly] public NativeArray<float> errorOut;

        public float blend;
        public float snapDistanceSq;
        public float leadSeconds;
        public int renderOnly;

        public void Execute(int i)
        {
            var current = pos[i];
            errorOut[i] = -1f;

            if (renderOnly != 0)
            {
                var offset = correction[i];
                if (targets.TryGetValue(netId[i], out var target))
                {
                    var aim = target + vel[i] * leadSeconds;

                    // 計測するのは「画面に描いている位置(計算上の位置+ずらし)」とHostとの差(従来方式と同じ基準で比べるため)。
                    errorOut[i] = math.length(current + offset - aim);
                    pos[i] = aim;

                    // 見た目の位置(計算上の位置+ずらし)は変えずに、計算上の位置だけHostへ合わせる。
                    offset += current - aim;
                    if (math.lengthsq(offset) > snapDistanceSq)
                    {
                        offset = float2.zero;
                    }
                }

                correction[i] = offset * (1f - blend);
                return;
            }

            if (targets.TryGetValue(netId[i], out var blendTarget))
            {
                var error = blendTarget + vel[i] * leadSeconds - current;
                errorOut[i] = math.length(error);
                if (math.lengthsq(error) > snapDistanceSq)
                {
                    pos[i] = blendTarget;
                    correction[i] = float2.zero;
                    return;
                }

                correction[i] = error;
            }

            var step = correction[i] * blend;
            pos[i] = current + step;
            correction[i] -= step;
        }
    }

    /// <summary>計測用。SwarmCorrectionJobが書いたズレを集計する。stats: [0]=届いた数 [1]=ズレの合計 [2]=最大 [3]=瞬間移動した数。</summary>
    [BurstCompile]
    public struct SwarmCorrectionStatsJob : IJob
    {
        [ReadOnly] public NativeArray<float> errors;
        public NativeArray<float> stats;
        public int count;
        public float snapDistance;

        public void Execute()
        {
            float corrected = 0f, sum = 0f, max = 0f, snaps = 0f;
            for (var i = 0; i < count; i++)
            {
                var e = errors[i];
                if (e < 0f)
                {
                    continue;
                }

                corrected += 1f;
                sum += e;
                max = math.max(max, e);
                if (e > snapDistance)
                {
                    snaps += 1f;
                }
            }

            stats[0] = corrected;
            stats[1] = sum;
            stats[2] = max;
            stats[3] = snaps;
        }
    }
}
