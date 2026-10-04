using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【詰め直しの前半】倒された敵・コア到達で消える敵を判定し、生き残る敵の番号を「空間ハッシュのセル順」で
    /// keepListに並べる。これにより、次フレーム以降は近くの敵がメモリ上でも近くに並び、近傍探索が速くなる。
    /// コア到達時の扱いは arrivalMode（消える／張り付く）で切り替わる。
    /// counters: [0]=生存数 [1]=撃破数 [2]=到達数 [3]=消えた敵の数(removedIds/removedReasonsに記録) / yRange: [0]=最小Y [1]=最大Y（描画のYソート用）
    ///
    /// ネット対戦のClient(replicaMode=1)では、自分の判断では消さない(体力・コア到達を見ない)。Hostが消した敵の番号
    /// (removeIds)だけを消す。コアに着いた敵は、張り付くモードなら張り付き、消えるモードならHostからの通知を待つ。
    /// </summary>
    [BurstCompile]
    public struct SwarmCompactScanJob : IJob
    {
        [ReadOnly] public NativeArray<int> order;
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<float> hp;
        [ReadOnly] public NativeArray<int> goalOf;
        [ReadOnly] public NativeArray<int> typeIdx;
        public NativeArray<int> state;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeHashSet<int> removeIds;
        public int count;
        public int arrivalMode;
        public int replicaMode;

        public NativeArray<int> keepList;
        public NativeArray<int> counters;
        public NativeArray<float> goalDamage;
        public NativeArray<int> attached;
        public NativeArray<int> killsByType;
        public NativeArray<float> yRange;
        public NativeArray<int> removedIds;
        public NativeArray<byte> removedReasons;

        public void Execute()
        {
            counters[1] = 0;
            counters[2] = 0;
            counters[3] = 0;
            for (var g = 0; g < goalDamage.Length; g++)
            {
                goalDamage[g] = 0f;
                attached[g] = 0;
            }

            for (var t = 0; t < killsByType.Length; t++)
            {
                killsByType[t] = 0;
            }

            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var write = 0;

            for (var k = 0; k < count; k++)
            {
                var read = order[k];
                var type = typeIdx[read];
                var id = netId[read];
                var goal = goalOf[read];

                if (replicaMode == 1)
                {
                    if (removeIds.Contains(id))
                    {
                        Record(id, EnemyRemovalReason.Removed);
                        continue;
                    }

                    if (state[read] == 0 && goal >= 0 && arrivalMode == (int)SwarmArrivalMode.LingerAndAttack)
                    {
                        state[read] = 1;
                    }
                }
                else
                {
                    if (hp[read] <= 0f)
                    {
                        counters[1] = counters[1] + 1;
                        killsByType[type] = killsByType[type] + 1;
                        Record(id, EnemyRemovalReason.Died);
                        continue;
                    }

                    if (state[read] == 0 && goal >= 0)
                    {
                        counters[2] = counters[2] + 1;
                        if (arrivalMode == (int)SwarmArrivalMode.VanishAndDamage)
                        {
                            goalDamage[goal] = goalDamage[goal] + types[type].damageToCore;
                            Record(id, EnemyRemovalReason.ReachedCore);
                            continue;
                        }

                        state[read] = 1;
                    }
                }

                if (state[read] == 1 && goal >= 0)
                {
                    attached[goal] = attached[goal] + 1;
                }

                var y = pos[read].y;
                minY = math.min(minY, y);
                maxY = math.max(maxY, y);
                keepList[write] = read;
                write++;
            }

            counters[0] = write;
            yRange[0] = write > 0 ? minY : 0f;
            yRange[1] = write > 0 ? maxY : 0f;
        }

        private void Record(int id, EnemyRemovalReason reason)
        {
            var n = counters[3];
            removedIds[n] = id;
            removedReasons[n] = (byte)reason;
            counters[3] = n + 1;
        }
    }

    /// <summary>【詰め直しの後半】keepListの順に、生き残った敵の全データを並列に別の配列へ詰め直す。</summary>
    [BurstCompile]
    public struct SwarmCompactCopyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> keepList;
        [ReadOnly] public NativeArray<int> counters;

        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<float2> vel;
        [ReadOnly] public NativeArray<float2> facing;
        [ReadOnly] public NativeArray<float> hp;
        [ReadOnly] public NativeArray<float> animTime;
        [ReadOnly] public NativeArray<float> flash;
        [ReadOnly] public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<int> goalOf;
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> correction;

        [WriteOnly] public NativeArray<float2> posOut;
        [WriteOnly] public NativeArray<float2> velOut;
        [WriteOnly] public NativeArray<float2> facingOut;
        [WriteOnly] public NativeArray<float> hpOut;
        [WriteOnly] public NativeArray<float> animTimeOut;
        [WriteOnly] public NativeArray<float> flashOut;
        [WriteOnly] public NativeArray<float> speedScaleOut;
        [WriteOnly] public NativeArray<int> typeIdxOut;
        [WriteOnly] public NativeArray<int> stateOut;
        [WriteOnly] public NativeArray<int> goalOfOut;
        [WriteOnly] public NativeArray<int> netIdOut;
        [WriteOnly] public NativeArray<float2> correctionOut;

        public void Execute(int j)
        {
            if (j >= counters[0])
            {
                return;
            }

            var s = keepList[j];
            posOut[j] = pos[s];
            velOut[j] = vel[s];
            facingOut[j] = facing[s];
            hpOut[j] = hp[s];
            animTimeOut[j] = animTime[s];
            flashOut[j] = flash[s];
            speedScaleOut[j] = speedScale[s];
            typeIdxOut[j] = typeIdx[s];
            stateOut[j] = state[s];
            goalOfOut[j] = goalOf[s];
            netIdOut[j] = netId[s];
            correctionOut[j] = correction[s];
        }
    }
}
