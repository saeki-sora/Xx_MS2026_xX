using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host側で、今回補正を送るグループ(番号 % groups == phase)の敵を集め、送る形(量子化した位置)に詰める。
    /// 以前はメインスレッドのC#で3万体を1体ずつ見ていたのを、Burstのジョブにした。
    /// SwarmSystem.StorageReadable の中(群衆の計算が止まっている間)で、Schedule してすぐ Complete する。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionGatherJob : IJob
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> pos;
        public int count;
        public int groups;
        public int phase;
        public NativeList<SwarmNetCorrection> output;

        /// <summary>選んだ敵のStorageでの位置(抜き取り検査で「最後に送った値」を記録するため)。</summary>
        public NativeList<int> indices;

        public void Execute()
        {
            output.Clear();
            indices.Clear();
            for (var i = 0; i < count; i++)
            {
                var id = netId[i];
                if (id % groups != phase)
                {
                    continue;
                }

                var p = pos[i];
                output.Add(new SwarmNetCorrection
                {
                    Id = (ushort)id,
                    X = SwarmNetQuantize.ToShort(p.x),
                    Y = SwarmNetQuantize.ToShort(p.y)
                });
                indices.Add(i);
            }
        }
    }
}
