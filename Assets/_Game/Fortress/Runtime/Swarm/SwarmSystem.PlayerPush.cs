using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    // 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で動く砲台が、敵に押される・敵に触られて汚れるための問い合わせ。
    public sealed partial class SwarmSystem
    {
        /// <summary>
        /// 中心 center・半径 bodyRadius の丸に重なっている敵を調べる。
        /// 戻り値 = 重なりの合計（1体ぶんの重なり 0〜1 を足したもの。まわりを囲まれていても打ち消し合わない＝汚れの溜まり方に使う）。
        /// push = 押し出す向き × 重なりの合計（まわりを囲まれると打ち消し合う＝押される向きに使う）。敵は押し返さない。
        /// <see cref="StorageReadable"/> の中でだけ呼べる（それ以外はジョブが配列を使っている）。
        /// </summary>
        public float ComputeCrowdContact(Vector2 center, float bodyRadius, out Vector2 push)
        {
            push = Vector2.zero;
            var count = Storage != null ? Storage.Count : 0;
            if (count <= 0 || !_typeParams.IsCreated)
            {
                return 0f;
            }

            var result = new NativeArray<float3>(1, Allocator.TempJob);
            new SwarmCrowdContactJob
            {
                pos = Storage.pos,
                typeIdx = Storage.typeIdx,
                typeParams = _typeParams,
                count = count,
                center = center,
                bodyRadius = bodyRadius,
                result = result
            }.Run();

            var value = result[0];
            result.Dispose();
            push = new Vector2(value.x, value.y);
            return value.z;
        }
    }

    [BurstCompile]
    internal struct SwarmCrowdContactJob : IJob
    {
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> typeParams;
        public int count;
        public float2 center;
        public float bodyRadius;

        /// <summary>xy = 押し出しの合計、z = 重なりの合計。</summary>
        public NativeArray<float3> result;

        public void Execute()
        {
            var push = float2.zero;
            var overlap = 0f;
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
                push += dir * overlap01;
                overlap += overlap01;
            }

            result[0] = new float3(push.x, push.y, overlap);
        }
    }
}
