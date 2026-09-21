using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【描画データ作成の前半・並列】詰め直し後の敵ごとに、アニメーションのコマ番号・向き・並べ替えキーを計算する。
    /// キー = 敵の種類 × 256 + Y座標のバケット（Yが大きい＝奥ほど先に描く）。
    /// </summary>
    [BurstCompile]
    public struct SwarmPrepInstanceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<float2> facing;
        [ReadOnly] public NativeArray<float> animTime;
        [ReadOnly] public NativeArray<float> flash;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [ReadOnly] public NativeArray<int> counters;
        [ReadOnly] public NativeArray<float> yRange;
        public int ySort;

        [WriteOnly] public NativeArray<SwarmInstance> tmpInstances;
        [WriteOnly] public NativeArray<int> keyOf;

        public void Execute(int i)
        {
            if (i >= counters[0])
            {
                return;
            }

            var p = pos[i];
            var tp = types[typeIdx[i]];

            var bucket = 0;
            if (ySort != 0)
            {
                var range = math.max(yRange[1] - yRange[0], 1e-4f);
                var t = (p.y - yRange[0]) / range;
                bucket = math.clamp((int)((1f - t) * (SwarmLimits.SortBuckets - 1)), 0, SwarmLimits.SortBuckets - 1);
            }

            keyOf[i] = typeIdx[i] * SwarmLimits.SortBuckets + bucket;

            var frame = (int)math.floor(animTime[i]) % math.max(1, tp.frameCount);

            var f = facing[i];
            var angle = math.atan2(f.y, f.x);
            if (angle < 0f)
            {
                angle += math.PI * 2f;
            }

            var directions = math.max(1, tp.directionCount);
            var dir = directions == 1
                ? 0
                : (int)math.floor(angle / (math.PI * 2f / directions) + 0.5f) % directions;

            tmpInstances[i] = new SwarmInstance
            {
                a = new float4(p.x, p.y, tp.spriteSize, math.saturate(flash[i])),
                b = new float4(frame, dir, 1f, 0f)
            };
        }
    }

    /// <summary>
    /// 【描画データ作成の後半】キーでカウンティングソートして、種類ごとにまとまった描画配列を作る。
    /// typeStart[t]〜typeStart[t+1] が種類tの描画範囲になる。
    /// </summary>
    [BurstCompile]
    public struct SwarmPrepSortJob : IJob
    {
        [ReadOnly] public NativeArray<int> counters;
        [ReadOnly] public NativeArray<SwarmInstance> tmpInstances;
        [ReadOnly] public NativeArray<int> keyOf;

        public NativeArray<SwarmInstance> instances;
        public NativeArray<int> typeStart;
        public NativeArray<int> keyStart;
        public NativeArray<int> keyCursor;

        public void Execute()
        {
            var count = counters[0];
            var keyCount = SwarmLimits.MaxTypes * SwarmLimits.SortBuckets;

            for (var k = 0; k <= keyCount; k++)
            {
                keyStart[k] = 0;
            }

            for (var i = 0; i < count; i++)
            {
                var key = keyOf[i];
                keyStart[key + 1] = keyStart[key + 1] + 1;
            }

            for (var k = 0; k < keyCount; k++)
            {
                keyStart[k + 1] = keyStart[k + 1] + keyStart[k];
                keyCursor[k] = keyStart[k];
            }

            for (var t = 0; t < SwarmLimits.MaxTypes; t++)
            {
                typeStart[t] = keyStart[t * SwarmLimits.SortBuckets];
            }

            typeStart[SwarmLimits.MaxTypes] = count;

            for (var i = 0; i < count; i++)
            {
                var key = keyOf[i];
                var slot = keyCursor[key];
                keyCursor[key] = slot + 1;
                instances[slot] = tmpInstances[i];
            }
        }
    }
}
