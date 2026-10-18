using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【描画データ作成の前半・並列】詰め直し後の敵ごとに、アニメーションのコマ番号・向いている角度・並べ替えキーを計算する。
    /// 向き(スプライトシートの行)は、画面の回転に合わせられるよう描画中のカメラごとにシェーダー(SwarmSpriteCommon.hlsl)で決める。
    /// キー = 敵の種類 × 256 + 画面の上方向(sortAxis)の位置のバケット（画面の上＝奥ほど先に描く。sortAxis=(0,1)ならY座標）。
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
        public float2 sortAxis;

        // 爆弾の敵の点滅（SwarmBombMath）。コアまでの距離で点滅が速くなる。
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> goals;
        public int goalCount;
        public float time;

        /// <summary>ネット対戦のClientで、計算上の位置に足して描く「見た目だけのずらし」(SwarmSnapshotFollowJob参照。新しい写真が届いた瞬間の先読みの外れを、数フレームかけて目立たずに消す)。</summary>
        [ReadOnly] public NativeArray<float2> renderOffset;
        public int applyRenderOffset;

        // 群れのうねり（つやの波）用の「混み具合」。詰め直し前の空間ハッシュ（keepList で詰め直し前の番号に戻す）の、まわり3×3マスの敵の数から求める。
        // Host でも Client でも詰め直しのために空間ハッシュを作っているので、全PCで同じように求まる（見た目だけ）。
        [ReadOnly] public NativeArray<int> keepList;
        [ReadOnly] public NativeArray<int> cellOf;
        [ReadOnly] public NativeArray<int> cellStart;
        public int gridWidth;
        public int gridHeight;
        /// <summary>まわり3×3マスにこの数の敵がいると「混み具合1」。0以下なら求めない（常に0）。</summary>
        public float crowdFullCount;

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
                var t = (math.dot(p, sortAxis) - yRange[0]) / range;
                bucket = math.clamp((int)((1f - t) * (SwarmLimits.SortBuckets - 1)), 0, SwarmLimits.SortBuckets - 1);
            }

            keyOf[i] = typeIdx[i] * SwarmLimits.SortBuckets + bucket;

            var frame = (int)math.floor(animTime[i]) % math.max(1, tp.frameCount);

            // ワールドでの向き(ラジアン, 0=+X 反時計回り)。シートのどの行を使うかはシェーダーが画面の向きと合わせて決める。
            var f = facing[i];
            var angle = math.atan2(f.y, f.x);

            var draw = applyRenderOffset != 0 ? p + renderOffset[i] : p;
            var blink = tp.explodes != 0 ? Blink(p, tp, netId[i]) : 0f;
            tmpInstances[i] = new SwarmInstance
            {
                a = new float4(draw.x, draw.y, tp.spriteSize * (1f + tp.blinkSwell * blink), math.saturate(flash[i])),
                b = new float4(frame, angle, Crowd(i), blink * tp.blinkStrength)
            };
        }

        private float Crowd(int i)
        {
            if (crowdFullCount <= 0f)
            {
                return 0f;
            }

            var cell = cellOf[keepList[i]];
            var cx = cell % gridWidth;
            var cy = cell / gridWidth;
            var n = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                var y = cy + dy;
                if (y < 0 || y >= gridHeight)
                {
                    continue;
                }

                for (var dx = -1; dx <= 1; dx++)
                {
                    var x = cx + dx;
                    if (x < 0 || x >= gridWidth)
                    {
                        continue;
                    }

                    var c = y * gridWidth + x;
                    n += cellStart[c + 1] - cellStart[c];
                }
            }

            // 自分自身は数えない。
            return math.saturate((n - 1) / crowdFullCount);
        }

        private float Blink(float2 p, SwarmTypeParams tp, int id)
        {
            var distance = float.MaxValue;
            for (var g = 0; g < goalCount; g++)
            {
                distance = math.min(distance, math.distance(p, goals[g]));
            }

            var phase = SwarmBombMath.BlinkPhase(time, SwarmBombMath.Seed(id), distance, tp.blinkPerSecond, tp.blinkSpeedUp,
                tp.blinkSpeedUpDistance);
            return SwarmBombMath.BlinkPulse(phase);
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
