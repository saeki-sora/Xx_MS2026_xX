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
    /// counters: [0]=生存数 [1]=撃破数 [2]=到達数 [3]=消えた敵の数(removedIds/removedReasonsに記録) / yRange: [0]=最小 [1]=最大（描画の奥→手前の並べ替え用。sortAxis 方向の位置。sortAxis=(0,1) ならY座標）
    /// killsByPlayer: このフレームに各プレイヤー(0〜3)が倒した数（最後にダメージを与えた人。だれか不明な撃破は数えない）。
    ///
    /// ネット対戦のClient(replicaMode=1)では、自分の判断では消さない(体力・コア到達を見ない)。Hostが消した敵の番号
    /// (removeIds)だけを消す。コアに着いた敵は、張り付くモードなら張り付き、消えるモードならHostからの通知を待つ。
    ///
    /// 爆弾の敵(types[].explodes)が倒された・コアで自爆したときは explosions に記録する（演出と、Hostではまわりへのダメージ）。
    /// Clientでは、Hostで「倒された／コアに着いた」と知らされた番号(explodeIds: 番号→消えた理由)の爆弾を記録する（演出だけ）。
    /// コアで自爆する爆弾(explodeAtCore)は、張り付くモードでも張り付かずに消える。
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
        [ReadOnly] public NativeArray<int> lastHit;
        [ReadOnly] public NativeHashSet<int> removeIds;
        [ReadOnly] public NativeHashMap<int, byte> explodeIds;
        public int count;
        public int arrivalMode;
        public int replicaMode;

        public NativeArray<int> keepList;
        public NativeArray<int> counters;
        public NativeArray<float> goalDamage;
        public NativeArray<int> attached;
        public NativeArray<int> killsByType;
        public NativeArray<int> killsByPlayer;
        public NativeArray<float> yRange;

        /// <summary>並べ替えに使う「画面の上」方向(地面上の単位ベクトル)。画面を回した視点でも奥から描けるよう、描画中のカメラから決める(EnemyScreenAlign)。見た目だけに使う。</summary>
        public float2 sortAxis;
        public NativeArray<int> removedIds;
        public NativeArray<byte> removedReasons;
        public NativeList<SwarmExplosion> explosions;

        public void Execute()
        {
            explosions.Clear();
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

            for (var p = 0; p < killsByPlayer.Length; p++)
            {
                killsByPlayer[p] = 0;
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
                        if (types[type].explodes != 0 && explodeIds.TryGetValue(id, out var reason))
                        {
                            var atCore = reason == (byte)EnemyRemovalReason.ReachedCore;
                            if (!atCore || types[type].explodeAtCore != 0)
                            {
                                AddExplosion(read, type, -1, atCore ? SwarmExplosionCause.ReachedCore : SwarmExplosionCause.Killed);
                            }
                        }

                        Record(id, EnemyRemovalReason.Removed);
                        continue;
                    }

                    if (state[read] == 0 && goal >= 0 && arrivalMode == (int)SwarmArrivalMode.LingerAndAttack
                        && types[type].explodeAtCore == 0)
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
                        var killer = lastHit[read];
                        if (killer >= 0 && killer < killsByPlayer.Length)
                        {
                            killsByPlayer[killer] = killsByPlayer[killer] + 1;
                        }

                        if (types[type].explodes != 0)
                        {
                            AddExplosion(read, type, killer, SwarmExplosionCause.Killed);
                        }

                        Record(id, EnemyRemovalReason.Died);
                        continue;
                    }

                    if (state[read] == 0 && goal >= 0)
                    {
                        counters[2] = counters[2] + 1;
                        var selfDestruct = types[type].explodeAtCore != 0;
                        if (arrivalMode == (int)SwarmArrivalMode.VanishAndDamage || selfDestruct)
                        {
                            goalDamage[goal] = goalDamage[goal] + types[type].damageToCore;
                            if (selfDestruct)
                            {
                                AddExplosion(read, type, -1, SwarmExplosionCause.ReachedCore);
                            }

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

                var y = math.dot(pos[read], sortAxis);
                minY = math.min(minY, y);
                maxY = math.max(maxY, y);
                keepList[write] = read;
                write++;
            }

            counters[0] = write;
            yRange[0] = write > 0 ? minY : 0f;
            yRange[1] = write > 0 ? maxY : 0f;
        }

        private void AddExplosion(int index, int type, int owner, SwarmExplosionCause cause)
        {
            explosions.Add(new SwarmExplosion { position = pos[index], type = type, owner = owner, cause = cause });
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
        [ReadOnly] public NativeArray<int> lastHit;

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
        [WriteOnly] public NativeArray<int> lastHitOut;

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
            lastHitOut[j] = lastHit[s];
        }
    }
}
