using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆弾の敵の点滅の計算（Burst のジョブからも呼ぶ、状態を持たない計算だけ）。
    /// 点滅の「位相」= 時刻×速さ ＋ コアへの近さの分。コアに近づくと位相が早く進む＝点滅が速くなる。
    /// 位相を時刻×(距離で変わる速さ)にすると、時刻が大きいほど距離の小さな変化で位相が飛ぶので、近さの分は足し算にしている
    /// （近づく速さ v のとき、増える点滅は v×speedUp÷距離 回/秒。止まっていれば元の速さ）。
    /// </summary>
    public static class SwarmBombMath
    {
        /// <summary>これより近い距離は同じ扱いにする（0 で割らないため）。</summary>
        public const float MinDistance = 0.5f;

        /// <summary>点滅の位相（回数。整数を越えるたびに1回光る）。seed は個体ごとのずらし（0〜1）。</summary>
        public static float BlinkPhase(float time, float seed, float distanceToCore, float perSecond, float speedUp, float speedUpDistance)
        {
            var d = math.max(distanceToCore, MinDistance);
            var far = math.max(speedUpDistance, MinDistance);
            var near = d < far ? speedUp * math.log(far / d) : 0f;
            return time * perSecond + near + seed;
        }

        /// <summary>位相から光り具合（0〜1）。周期の頭でパッと光って、すぐ消える（チカッ、チカッ）。</summary>
        public static float BlinkPulse(float phase)
        {
            var f = phase - math.floor(phase);
            var fade = math.saturate(1f - f * 2f);
            return fade * fade * fade;
        }

        /// <summary>通信用の番号から、個体ごとの点滅のずらし（0〜1）。同じ番号ならどのPCでも同じ値になる。</summary>
        public static float Seed(int netId)
        {
            var x = netId * 0.6180339887f;
            return x - math.floor(x);
        }
    }
}
