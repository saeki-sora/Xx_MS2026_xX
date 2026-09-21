using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 押し合いの結果を位置・速度に確定し、向き・アニメーション時間・被弾フラッシュを更新し、
    /// コア（目的地）への到達を判定する。
    /// </summary>
    [BurstCompile]
    public struct SwarmFinalizeJob : IJobParallelFor
    {
        public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        public NativeArray<float2> facing;
        public NativeArray<float> animTime;
        public NativeArray<float> flash;
        public NativeArray<int> goalOf;
        [ReadOnly] public NativeArray<float2> pred;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<int> state;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [ReadOnly] public NativeArray<float2> goals;
        public int goalCount;

        public float dt;
        public float momentumTransfer;
        public float flashDecay;
        public float arrivalRadius;

        public void Execute(int i)
        {
            var tp = types[typeIdx[i]];
            flash[i] = math.max(0f, flash[i] - dt * flashDecay);

            if (state[i] == 1)
            {
                vel[i] = float2.zero;
                return;
            }

            var old = pos[i];
            var next = pred[i];
            var steered = vel[i];
            var actual = (next - old) / dt;

            var v = math.lerp(steered, actual, momentumTransfer);
            var speed = math.length(v);
            var cap = tp.maxSpeed * 1.6f;
            if (speed > cap)
            {
                v *= cap / speed;
                speed = cap;
            }

            vel[i] = v;
            pos[i] = next;

            if (speed > 0.05f * tp.maxSpeed)
            {
                var target = v / speed;
                var blend = 1f - math.exp(-tp.turnSharpness * dt);
                facing[i] = math.normalizesafe(math.lerp(facing[i], target, blend), target);
            }

            animTime[i] += dt * tp.animFps * math.clamp(speed / math.max(tp.maxSpeed, 1e-3f), 0.25f, 1.5f);

            goalOf[i] = -1;
            for (var g = 0; g < goalCount; g++)
            {
                var reach = arrivalRadius + tp.radius;
                if (math.distancesq(next, goals[g]) < reach * reach)
                {
                    goalOf[i] = g;
                    break;
                }
            }
        }
    }
}
