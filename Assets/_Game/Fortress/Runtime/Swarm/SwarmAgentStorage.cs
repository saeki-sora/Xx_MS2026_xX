using System;
using Unity.Collections;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 全ての敵のデータを「配列の列（Structure of Arrays）」で保持する。
    /// 生きている敵は常に 0..Count-1 に詰まっている。毎フレームの詰め直しは「もう一組の配列（〜B）」へ
    /// 空間ハッシュのセル順に書き出し、終わったら入れ替える（Swap）。これで近い敵がメモリ上でも近くに並ぶ。
    /// </summary>
    public sealed class SwarmAgentStorage : IDisposable
    {
        public readonly int Capacity;

        public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        public NativeArray<float2> facing;
        public NativeArray<float> hp;
        public NativeArray<float> animTime;
        public NativeArray<float> flash;
        public NativeArray<float> speedScale;
        public NativeArray<int> typeIdx;
        public NativeArray<int> state;
        public NativeArray<int> goalOf;

        /// <summary>ネット対戦で1体を見分ける番号(0〜65535)。詰め直しでも敵と一緒に移動する。Hostが振り、Clientは届いた番号を使う。</summary>
        public NativeArray<int> netId;

        /// <summary>Client側で、Hostの位置とのズレのうち、まだ詰め終わっていない分。毎フレーム少しずつ位置へ足し込む。</summary>
        public NativeArray<float2> correction;

        // 詰め直し先（Swapで入れ替わる）
        public NativeArray<float2> posB;
        public NativeArray<float2> velB;
        public NativeArray<float2> facingB;
        public NativeArray<float> hpB;
        public NativeArray<float> animTimeB;
        public NativeArray<float> flashB;
        public NativeArray<float> speedScaleB;
        public NativeArray<int> typeIdxB;
        public NativeArray<int> stateB;
        public NativeArray<int> goalOfB;
        public NativeArray<int> netIdB;
        public NativeArray<float2> correctionB;

        // 押し合い計算の一時領域
        public NativeArray<float2> predA;
        public NativeArray<float2> predB;

        public int Count;

        public SwarmAgentStorage(int capacity)
        {
            Capacity = capacity;

            pos = Alloc<float2>(capacity);
            vel = Alloc<float2>(capacity);
            facing = Alloc<float2>(capacity);
            hp = Alloc<float>(capacity);
            animTime = Alloc<float>(capacity);
            flash = Alloc<float>(capacity);
            speedScale = Alloc<float>(capacity);
            typeIdx = Alloc<int>(capacity);
            state = Alloc<int>(capacity);
            goalOf = Alloc<int>(capacity);
            netId = Alloc<int>(capacity);
            correction = Alloc<float2>(capacity);

            posB = Alloc<float2>(capacity);
            velB = Alloc<float2>(capacity);
            facingB = Alloc<float2>(capacity);
            hpB = Alloc<float>(capacity);
            animTimeB = Alloc<float>(capacity);
            flashB = Alloc<float>(capacity);
            speedScaleB = Alloc<float>(capacity);
            typeIdxB = Alloc<int>(capacity);
            stateB = Alloc<int>(capacity);
            goalOfB = Alloc<int>(capacity);
            netIdB = Alloc<int>(capacity);
            correctionB = Alloc<float2>(capacity);

            predA = Alloc<float2>(capacity);
            predB = Alloc<float2>(capacity);
        }

        public bool TryAdd(float2 position, int type, float health, float speed, float animStart, float2 face, int id)
        {
            if (Count >= Capacity)
            {
                return false;
            }

            var i = Count++;
            pos[i] = position;
            vel[i] = float2.zero;
            facing[i] = face;
            hp[i] = health;
            animTime[i] = animStart;
            flash[i] = 0f;
            speedScale[i] = speed;
            typeIdx[i] = type;
            state[i] = 0;
            goalOf[i] = -1;
            netId[i] = id;
            correction[i] = float2.zero;
            return true;
        }

        public void Clear()
        {
            Count = 0;
        }

        /// <summary>詰め直し先の配列を、現在の配列として入れ替える。ジョブが全て完了している間にだけ呼ぶこと。</summary>
        public void Swap()
        {
            Exchange(ref pos, ref posB);
            Exchange(ref vel, ref velB);
            Exchange(ref facing, ref facingB);
            Exchange(ref hp, ref hpB);
            Exchange(ref animTime, ref animTimeB);
            Exchange(ref flash, ref flashB);
            Exchange(ref speedScale, ref speedScaleB);
            Exchange(ref typeIdx, ref typeIdxB);
            Exchange(ref state, ref stateB);
            Exchange(ref goalOf, ref goalOfB);
            Exchange(ref netId, ref netIdB);
            Exchange(ref correction, ref correctionB);
        }

        public void Dispose()
        {
            Free(ref pos);
            Free(ref vel);
            Free(ref facing);
            Free(ref hp);
            Free(ref animTime);
            Free(ref flash);
            Free(ref speedScale);
            Free(ref typeIdx);
            Free(ref state);
            Free(ref goalOf);
            Free(ref netId);
            Free(ref correction);

            Free(ref posB);
            Free(ref velB);
            Free(ref facingB);
            Free(ref hpB);
            Free(ref animTimeB);
            Free(ref flashB);
            Free(ref speedScaleB);
            Free(ref typeIdxB);
            Free(ref stateB);
            Free(ref goalOfB);
            Free(ref netIdB);
            Free(ref correctionB);

            Free(ref predA);
            Free(ref predB);
        }

        private static void Exchange<T>(ref NativeArray<T> a, ref NativeArray<T> b) where T : struct
        {
            var temp = a;
            a = b;
            b = temp;
        }

        private static NativeArray<T> Alloc<T>(int count) where T : struct
        {
            return new NativeArray<T>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void Free<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }
    }
}
