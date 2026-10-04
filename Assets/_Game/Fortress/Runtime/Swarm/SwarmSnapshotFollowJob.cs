using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// ネット対戦のClient専用(写真方式、2026-10-05)。押し合いは計算せず、Hostから届いた最新の写真(全員が同じ瞬間の位置と速度)を
    /// 届くまでの遅れの分だけ先へ進めた位置(snapPos + snapVel × lead)に置く。全員が同じ瞬間から同じ流れで進むので、隣どうしが重ならない。
    ///
    /// 見た目が飛ばないよう、「前のフレームの動きをそのまま続けた位置」と新しい位置の差を描画だけのずらし(correction)に足し、
    /// ずらしは毎フレーム一部ずつ消す(新しい写真が届いた瞬間の先読みの外れを、数フレームかけて目立たずに直す)。
    /// 写真が届かない間は、続けた位置と新しい位置が一致するので、ずらしは増えない。
    /// 先読みに使う速度は、Hostと同じく種類の最高速度×1.6までに抑える(一斉投入の直後は押し合いで位置が大きく飛び、
    /// 写真の差から求めた速度が実際の動きより大きくなって、先読みが行き過ぎるため。2026-10-05の計測で1秒に約6万回の瞬間移動)。
    /// </summary>
    [BurstCompile]
    public struct SwarmSnapshotFollowJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> netId;
        [ReadOnly] public NativeArray<float2> snapPos;
        [ReadOnly] public NativeArray<float2> snapVel;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        public NativeArray<float2> correction;

        /// <summary>計測用。新しい写真が届いたフレームは、先読みの外れの大きさ。それ以外は-1。</summary>
        [WriteOnly] public NativeArray<float> errorOut;

        /// <summary>計測用(全員が偏りなく測られるので、抜き取り検査と同じ扱いにする)。</summary>
        [WriteOnly] public NativeArray<byte> auditOut;

        /// <summary>写真の時刻から、今表示する時刻までの秒数。</summary>
        public float lead;

        /// <summary>前回この処理をしてからの実時間(秒)。</summary>
        public float advance;

        public float blend;
        public float snapDistanceSq;
        public int newSnapshot;

        // Hostの SwarmFinalizeJob と同じ上限。
        private const float MaxSpeedFactor = 1.6f;

        public void Execute(int i)
        {
            var id = netId[i];
            var velocity = snapVel[id];
            var cap = types[typeIdx[i]].maxSpeed * MaxSpeedFactor;
            var speedSq = math.lengthsq(velocity);
            if (speedSq > cap * cap)
            {
                velocity *= cap / math.sqrt(speedSq);
            }

            var target = snapPos[id] + velocity * lead;
            var continued = pos[i] + vel[i] * advance;
            var jump = continued - target;

            var offset = correction[i] + jump;
            if (math.lengthsq(offset) > snapDistanceSq)
            {
                offset = float2.zero;
            }

            errorOut[i] = newSnapshot != 0 ? math.length(jump) : -1f;
            auditOut[i] = (byte)(newSnapshot != 0 ? 1 : 0);
            pos[i] = target;
            vel[i] = velocity;
            correction[i] = offset * (1f - blend);
        }
    }

    /// <summary>
    /// ネット対戦のClient専用(写真方式)。押し合いの計算(SwarmFinalizeJob)の代わりに、届いた速度から向き・アニメーション時間を進め、
    /// 被弾フラッシュを消していき、コアに着いたかを判定する(位置と速度は SwarmSnapshotFollowJob が決める)。
    /// </summary>
    [BurstCompile]
    public struct SwarmReplicaFinalizeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> pos;
        [ReadOnly] public NativeArray<float2> vel;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [ReadOnly] public NativeArray<float2> goals;
        public NativeArray<float2> facing;
        public NativeArray<float> animTime;
        public NativeArray<float> flash;
        public NativeArray<int> goalOf;
        public int goalCount;
        public float dt;
        public float flashDecay;
        public float arrivalRadius;

        public void Execute(int i)
        {
            var tp = types[typeIdx[i]];
            flash[i] = math.max(0f, flash[i] - dt * flashDecay);
            if (state[i] == 1)
            {
                return;
            }

            var v = vel[i];
            var speed = math.length(v);
            if (speed > 0.05f * tp.maxSpeed)
            {
                var target = v / speed;
                var turn = 1f - math.exp(-tp.turnSharpness * dt);
                facing[i] = math.normalizesafe(math.lerp(facing[i], target, turn), target);
            }

            animTime[i] += dt * tp.animFps * math.clamp(speed / math.max(tp.maxSpeed, 1e-3f), 0.25f, 1.5f);

            goalOf[i] = -1;
            var p = pos[i];
            for (var g = 0; g < goalCount; g++)
            {
                var reach = arrivalRadius + tp.radius;
                if (math.distancesq(p, goals[g]) < reach * reach)
                {
                    goalOf[i] = g;
                    break;
                }
            }
        }
    }
}
