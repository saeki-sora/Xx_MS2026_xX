using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    public static class SwarmLimits
    {
        public const int MaxTypes = 16;
        public const int MaxGoals = 16;
        public const int MaxBeams = 32;
        public const int SortBuckets = 256;
        public const int MaxPierceBuffer = 256;
    }

    /// <summary>コア（目的地）に着いた敵をどう扱うか。差し替え式なので、後から挙動を変えるときはここに値を足す。</summary>
    public enum SwarmArrivalMode
    {
        /// <summary>接触した敵は消え、コアにダメージを与える。</summary>
        VanishAndDamage = 0,

        /// <summary>接触した敵はコアに張り付き、秒間ダメージを与え続ける。</summary>
        LingerAndAttack = 1
    }

    /// <summary>ジョブから参照する、敵の種類ごとの数値（EnemyTypeDefinitionから毎フレーム作る）。</summary>
    public struct SwarmTypeParams
    {
        public float radius;
        public float mass;
        public float maxSpeed;
        public float acceleration;
        public float turnSharpness;
        public float animFps;
        public float spriteSize;
        public float damageToCore;
        public int profileIndex;
        public int frameCount;
        public int directionCount;
    }

    /// <summary>GPUに渡す1体分の描画データ（32バイト）。a=(x, y, 大きさ, 被弾フラッシュ) / b=(フレーム, 向き, 予備, 予備)。</summary>
    public struct SwarmInstance
    {
        public float4 a;
        public float4 b;
    }

    /// <summary>そのフレームに発射されているレーザー1本分。maxHitsが0なら貫通数無制限。</summary>
    public struct SwarmBeam
    {
        public float2 origin;
        public float2 end;
        public float halfWidth;
        public float damagePerSecond;
        public int maxHits;
    }

    /// <summary>計測用の統計。</summary>
    public struct SwarmStats
    {
        public int alive;
        public int capacity;
        public float simulationMs;
        public float jobLatencyMs;
        public float frameMs;
        public int totalSpawned;
        public int totalKilled;
        public int totalArrived;
        public int drawBatches;
        public int rejectedSpawns;
        public float navigationMs;
        public float renderMs;
        public int gcCollections;
        public int maxCellCount;
        public float pairCheckEstimate;
    }

    /// <summary>フレーム時間が閾値を超えた瞬間の内訳。「群衆の中が原因か、外（エディタ・GC・他スクリプト）が原因か」を切り分ける。</summary>
    public struct SwarmSpike
    {
        public float time;
        public float frameMs;
        public float navigationMs;
        public float simulationMs;
        public float renderMs;
        public int alive;
        public int maxCellCount;
        public bool navigationRebuilt;
        public bool gcOccurred;

        /// <summary>群衆システムの外で使われた時間（エディタ、GC、他のスクリプト、GPU待ち・vsyncなど）。</summary>
        public float OtherMs => Mathf.Max(0f, frameMs - navigationMs - simulationMs - renderMs);
    }
}
