using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    // 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で動く砲台が敵に押されるための問い合わせ。
    public sealed partial class SwarmSystem
    {
        /// <summary>
        /// 中心 center・半径 bodyRadius の丸に重なっている敵から受ける「押し出し」を返す。
        /// 1体ぶんの重なり（0〜1）× 押し出す向きを全部足したもの。敵は押し返さない（砲台だけが押される）。
        /// <see cref="StorageReadable"/> の中でだけ呼べる（それ以外はジョブが配列を使っている）。
        /// </summary>
        public Vector2 ComputeCrowdPush(Vector2 center, float bodyRadius)
        {
            var count = Storage != null ? Storage.Count : 0;
            if (count <= 0 || !_typeParams.IsCreated)
            {
                return Vector2.zero;
            }

            var result = new NativeArray<float2>(1, Allocator.TempJob);
            new SwarmCrowdPushJob
            {
                pos = Storage.pos,
                typeIdx = Storage.typeIdx,
                typeParams = _typeParams,
                count = count,
                center = center,
                bodyRadius = bodyRadius,
                result = result
            }.Run();

            var push = result[0];
            result.Dispose();
            return new Vector2(push.x, push.y);
        }
    }

    [BurstCompile]
    internal struct SwarmCrowdPushJob : IJob
    {
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> typeParams;
        public int count;
        public float2 center;
        public float bodyRadius;
        public NativeArray<float2> result;

        public void Execute()
        {
            var sum = float2.zero;
            var types = typeParams.Length;
            for (var i = 0; i < count; i++)
            {
                var type = typeIdx[i];
                var agentRadius = type >= 0 && type < types ? typeParams[type].radius : 0.3f;
                var reach = bodyRadius + agentRadius;
                var d = center - pos[i];
                var distSq = math.lengthsq(d);
                if (distSq >= reach * reach)
                {
                    continue;
                }

                var dist = math.sqrt(distSq);
                // ちょうど真上に重なっている敵は向きが決まらないので、番号で決めた向きへ逃がす（毎フレーム同じ向き）。
                var dir = dist > 1e-4f ? d / dist : new float2(math.cos(i * 2.399963f), math.sin(i * 2.399963f));
                var overlap01 = 1f - dist / reach;
                sum += dir * overlap01;
            }

            result[0] = sum;
        }
    }
}
