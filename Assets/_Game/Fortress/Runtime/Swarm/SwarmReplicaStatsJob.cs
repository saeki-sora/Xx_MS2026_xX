using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 計測用(ネット対戦のClient)。SwarmSnapshotFollowJob が書いた「新しい写真が届いた瞬間の先読みの外れ」を集計する。
    /// stats: [0]=測った数 [1]=外れの合計 [2]=最大 [3]=滑らせずにその場で合わせた(瞬間移動した)数。
    /// </summary>
    [BurstCompile]
    public struct SwarmReplicaStatsJob : IJob
    {
        public const int StatCount = 4;

        [ReadOnly] public NativeArray<float> errors;
        public NativeArray<float> stats;
        public int count;
        public float snapDistance;

        public void Execute()
        {
            float measured = 0f, sum = 0f, max = 0f, snaps = 0f;
            for (var i = 0; i < count; i++)
            {
                var e = errors[i];
                if (e < 0f)
                {
                    continue;
                }

                measured += 1f;
                sum += e;
                max = math.max(max, e);
                if (e > snapDistance)
                {
                    snaps += 1f;
                }
            }

            stats[0] = measured;
            stats[1] = sum;
            stats[2] = max;
            stats[3] = snaps;
        }
    }
}
