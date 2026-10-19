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

        /// <summary>「当たった瞬間」を1フレームに記録できる最大数（上限を決めていないときもこれ以上は出さない）。</summary>
        public const int MaxHitReportsPerFrame = 256;

        /// <summary>1フレームに群衆へ届けられる爆発ダメージの最大数。超えた分は次のフレームに回す。</summary>
        public const int MaxBlastsPerFrame = 256;
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
        /// <summary>レーザーが当たる半径（0 なら radius）。</summary>
        public float hitRadius;
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

        // 爆弾の敵（EnemyBombSettings）。explodes=0 の種類では下の値は使わない。
        /// <summary>1 なら倒されたときに爆発する。</summary>
        public int explodes;
        /// <summary>1 ならコアに着いたときも自爆する（張り付くモードでも張り付かない）。</summary>
        public int explodeAtCore;
        public float blinkStrength;
        public float blinkPerSecond;
        public float blinkSpeedUp;
        public float blinkSpeedUpDistance;
        public float blinkSwell;
    }

    /// <summary>爆弾の敵が爆発した理由。</summary>
    public enum SwarmExplosionCause : byte
    {
        /// <summary>倒された（レーザー・ほかの爆発）。</summary>
        Killed = 0,

        /// <summary>コアにたどり着いて自爆した。</summary>
        ReachedCore = 1
    }

    /// <summary>爆弾の敵が爆発した記録1件分（詰め直しのジョブが書く）。演出と、Hostではまわりへのダメージの元になる。</summary>
    public struct SwarmExplosion
    {
        public float2 position;
        /// <summary>このシステム内の種類番号（どの EnemyTypeDefinition の爆弾か）。</summary>
        public int type;
        /// <summary>爆弾を倒した人（最後にダメージを与えたプレイヤー 0〜3）。不明・Clientでは -1。</summary>
        public int owner;
        public SwarmExplosionCause cause;
    }

    /// <summary>
    /// 群衆の敵が倒された記録1件分（演出用。詰め直しのジョブが書く）。Hostでは体力0になった敵、
    /// Clientでは Host から「倒された」と届いた敵の、消える直前の位置。
    /// </summary>
    public struct SwarmDeath
    {
        public float2 position;
        /// <summary>倒した人（最後にビームを当てたプレイヤー 0〜3）。不明なら -1。Clientでは自分のPCで見えていたビームから決める（見た目用）。</summary>
        public int owner;
        /// <summary>このシステム内の種類番号。</summary>
        public int type;
    }

    /// <summary>そのフレームに群衆の敵へ届く爆発のダメージ1件分（レーザー判定のジョブがまとめて与える）。</summary>
    public struct SwarmBlast
    {
        public float2 center;
        public float radius;
        public float damage;
        /// <summary>巻き込んで倒した敵を、だれの撃破にするか（爆弾を倒した人）。不明なら -1。</summary>
        public int owner;
    }

    /// <summary>GPUに渡す1体分の描画データ（32バイト）。a=(x, y, 大きさ, 被弾フラッシュ) / b=(フレーム, 向いている角度(ラジアン, ワールド), 混み具合0〜1(群れのうねり), 爆弾の点滅)。シートの行はシェーダーが画面の向きに合わせて決める。</summary>
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

        /// <summary>撃ったプレイヤーの番号(0-3)。分からなければ-1。当たった瞬間の記録（演出）に載る。</summary>
        public int owner;
    }

    /// <summary>
    /// レーザーが群衆の敵に「当たった瞬間」1件分（しばらく当たっていなかった敵＝被弾フラッシュが消えていた敵に触れた瞬間）。
    /// 演出用。各PCが自分の群衆の計算で記録するので、ネット対戦でも通信なしで全員の画面に出せる。
    /// </summary>
    public struct SwarmBeamHit
    {
        public float2 position;
        public int owner;
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

        // ネット対戦のClientで、直前のフレームに新しい写真が届いたときの先読みの外れ(計測用)。
        public int replicaCorrections;
        public float replicaErrorSum;
        public float replicaErrorMax;
        public int replicaSnaps;

        /// <summary>まだ追加していない(1フレームの上限を超えて次に回された)出現の数。</summary>
        public int pendingSpawns;
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
