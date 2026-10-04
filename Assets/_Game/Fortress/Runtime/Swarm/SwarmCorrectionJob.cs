using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// ネット対戦のClient専用。Hostから届いた位置(targets)へ寄せる。2つのやり方がある(renderOnly)。
    ///
    /// renderOnly=1(既定。段階4): 計算上の位置は届いた位置にその場で合わせ、見た目が飛ばないよう、
    ///   ズレの分を「描画だけのずらし(correction)」として持つ。ずらしは毎フレーム一部ずつ消える(=見た目だけ滑らかに追いつく)。
    ///   計算上の位置がすぐHostと一致するので、押し合いが激しい場面でもズレが溜まり続けない。
    /// renderOnly=0(従来): ズレを「詰め残し(correction)」として覚え、計算上の位置へ毎フレーム一部ずつ足し込む。
    ///
    /// どちらも、ズレが大きすぎるとき(snapDistance超)は見た目ごとその場で合わせる。
    /// Hostの位置は届くまでの間に古くなっているので、その敵の今の速度×leadSeconds だけ先へ進めた位置を目標にする。
    /// Hostの速度も届いたとき(段階5、targets.zw が数値)は、その速度で先へ進め、速度もHostに合わせる(押し合いの混乱でも先読みが外れにくい)。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> netId;
        public NativeArray<float2> vel;

        /// <summary>
        /// 番号→Hostでの位置(xy)と速度(zw)。速度が届いていなければ z は NaN で、w は「抜き取り検査の補正なら1、それ以外は0」。
        /// </summary>
        [ReadOnly] public NativeHashMap<int, float4> targets;
        public NativeArray<float2> pos;
        public NativeArray<float2> correction;

        /// <summary>計測用。このフレームにHostの位置が届いた敵はそのズレの大きさ、届かなかった敵は-1。</summary>
        [WriteOnly] public NativeArray<float> errorOut;

        /// <summary>計測用。届いた位置が抜き取り検査の補正(優先度に関係なく選ばれた物)なら1。</summary>
        [WriteOnly] public NativeArray<byte> auditOut;

        public float blend;
        public float snapDistanceSq;
        public float leadSeconds;
        public int renderOnly;

        public void Execute(int i)
        {
            var current = pos[i];
            errorOut[i] = -1f;
            auditOut[i] = 0;

            if (renderOnly != 0)
            {
                var offset = correction[i];
                if (targets.TryGetValue(netId[i], out var target))
                {
                    auditOut[i] = IsAudit(target) ? (byte)1 : (byte)0;
                    var aim = Aim(i, target);

                    // 計測するのは「画面に描いている位置(計算上の位置+ずらし)」とHostとの差(従来方式と同じ基準で比べるため)。
                    errorOut[i] = math.length(current + offset - aim);
                    pos[i] = aim;

                    // 見た目の位置(計算上の位置+ずらし)は変えずに、計算上の位置だけHostへ合わせる。
                    offset += current - aim;
                    if (math.lengthsq(offset) > snapDistanceSq)
                    {
                        offset = float2.zero;
                    }
                }

                correction[i] = offset * (1f - blend);
                return;
            }

            if (targets.TryGetValue(netId[i], out var blendTarget))
            {
                auditOut[i] = IsAudit(blendTarget) ? (byte)1 : (byte)0;
                var error = Aim(i, blendTarget) - current;
                errorOut[i] = math.length(error);
                if (math.lengthsq(error) > snapDistanceSq)
                {
                    pos[i] = blendTarget.xy;
                    correction[i] = float2.zero;
                    return;
                }

                correction[i] = error;
            }

            var step = correction[i] * blend;
            pos[i] = current + step;
            correction[i] -= step;
        }

        private static bool IsAudit(float4 target)
        {
            return math.isnan(target.z) && target.w > 0.5f;
        }

        // 目標位置(届くまでの遅れの分だけ先へ進める)。Hostの速度が届いていれば、それで進めて速度も合わせる。
        private float2 Aim(int i, float4 target)
        {
            if (math.isnan(target.z))
            {
                return target.xy + vel[i] * leadSeconds;
            }

            vel[i] = target.zw;
            return target.xy + target.zw * leadSeconds;
        }
    }

    /// <summary>
    /// ネット対戦のClient専用。Hostから届いた補正(量子化された位置)から、番号→目標位置の表を作る。
    /// 以前はメインスレッドで1件ずつ作っていた(3万体・0.1秒間隔だと毎秒30万件)のを、ジョブに移した。
    /// 位置だけの補正は速度を NaN にする。同じ番号が重なれば、後から入れる物(速度つき → 抜き取り検査の順)を使う。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionTargetsJob : IJob
    {
        [ReadOnly] public NativeArray<MS2026.Fortress.Net.SwarmNetCorrection> corrections;
        [ReadOnly] public NativeArray<MS2026.Fortress.Net.SwarmNetCorrectionV> correctionsWithVelocity;
        [ReadOnly] public NativeArray<MS2026.Fortress.Net.SwarmNetCorrection> auditCorrections;
        public NativeHashMap<int, float4> targets;
        public float inverseScale;

        public void Execute()
        {
            targets.Clear();
            for (var i = 0; i < corrections.Length; i++)
            {
                var c = corrections[i];
                targets[c.Id] = new float4(c.X * inverseScale, c.Y * inverseScale, float.NaN, 0f);
            }

            for (var i = 0; i < correctionsWithVelocity.Length; i++)
            {
                var c = correctionsWithVelocity[i];
                targets[c.Id] = new float4(c.X * inverseScale, c.Y * inverseScale, math.f16tof32(c.Vx), math.f16tof32(c.Vy));
            }

            for (var i = 0; i < auditCorrections.Length; i++)
            {
                var c = auditCorrections[i];
                targets[c.Id] = new float4(c.X * inverseScale, c.Y * inverseScale, float.NaN, 1f);
            }
        }
    }

    /// <summary>
    /// 計測用。SwarmCorrectionJobが書いたズレを集計する。
    /// stats: [0]=届いた数 [1]=ズレの合計 [2]=最大 [3]=瞬間移動した数 [4]=そのうち自分の画面に映る数 [5]=画面に映る敵のズレの合計
    /// [6]=抜き取り検査の数 [7]=そのズレの合計 [8]=抜き取り検査のうち画面に映る数 [9]=そのズレの合計。
    /// 優先度つきの補正では「ズレていそうな敵」を選んで送るので、届いた全体のズレ([1])は大きめに出る。
    /// 優先度と関係なく選ばれる抜き取り検査のズレ([7])が、全員のズレの公平な見積もりになる。
    /// </summary>
    [BurstCompile]
    public struct SwarmCorrectionStatsJob : IJob
    {
        [ReadOnly] public NativeArray<float> errors;
        [ReadOnly] public NativeArray<byte> audit;
        [ReadOnly] public NativeArray<float2> pos;
        public NativeArray<float> stats;
        public int count;
        public float snapDistance;

        /// <summary>自分の画面に映る範囲(xMin, yMin, xMax, yMax)。hasView=0なら数えない。</summary>
        public float4 view;
        public int hasView;

        public void Execute()
        {
            float corrected = 0f, sum = 0f, max = 0f, snaps = 0f, viewCount = 0f, viewSum = 0f;
            float auditCount = 0f, auditSum = 0f, auditViewCount = 0f, auditViewSum = 0f;
            for (var i = 0; i < count; i++)
            {
                var e = errors[i];
                if (e < 0f)
                {
                    continue;
                }

                corrected += 1f;
                sum += e;
                max = math.max(max, e);
                if (e > snapDistance)
                {
                    snaps += 1f;
                }

                var p = pos[i];
                var inView = hasView != 0 && p.x >= view.x && p.y >= view.y && p.x <= view.z && p.y <= view.w;
                if (inView)
                {
                    viewCount += 1f;
                    viewSum += e;
                }

                if (audit[i] != 0)
                {
                    auditCount += 1f;
                    auditSum += e;
                    if (inView)
                    {
                        auditViewCount += 1f;
                        auditViewSum += e;
                    }
                }
            }

            stats[0] = corrected;
            stats[1] = sum;
            stats[2] = max;
            stats[3] = snaps;
            stats[4] = viewCount;
            stats[5] = viewSum;
            stats[6] = auditCount;
            stats[7] = auditSum;
            stats[8] = auditViewCount;
            stats[9] = auditViewSum;
        }
    }
}
