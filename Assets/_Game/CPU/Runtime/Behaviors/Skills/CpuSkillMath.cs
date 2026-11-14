using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// スキルの効きの見積もり（決まりだけ。テストできるように Unity のオブジェクトに触らない）。
    /// どれも「目が見た敵の重み」から出す、おおまかな数。
    /// </summary>
    public static class CpuSkillMath
    {
        /// <summary>
        /// 扇形に広がる攻撃（赤の極太レーザー）の当たる敵: 今の向きの前後 halfWidth 度と、撃っている間に回る sweep 度ぶん。
        /// </summary>
        public static float ConeValue(float[] weights, float aimDegrees, float sign, float halfWidthDegrees, float sweepDegrees)
        {
            if (weights == null || weights.Length == 0)
            {
                return 0f;
            }

            var width = 360f / weights.Length;
            var half = Mathf.Max(width * 0.5f, halfWidthDegrees);
            var forward = Mathf.Abs(sign) < 0.5f ? 1f : sign;

            // 回る向きの反対側の端から、回り終わる所の端まで。
            var start = aimDegrees - forward * half;
            return CpuSectorMath.SumAhead(weights, start, forward, 0f, half * 2f + Mathf.Max(0f, sweepDegrees));
        }

        /// <summary>まっすぐ飛ぶ攻撃（黄の矢・ふつうの判断）の当たる敵: 今の向きの扇と、両どなり半分ずつ。</summary>
        public static float LineValue(float[] weights, float aimDegrees)
        {
            if (weights == null || weights.Length == 0)
            {
                return 0f;
            }

            var count = weights.Length;
            var current = CpuSectorMath.SectorOf(aimDegrees, count);
            var left = weights[(current + 1) % count];
            var right = weights[(current - 1 + count) % count];
            return weights[current] + 0.5f * (left + right);
        }

        /// <summary>数を 0〜1 の効きにする（full でいっぱい）。</summary>
        public static float Normalize(float amount, float full) => Mathf.Clamp01(amount / Mathf.Max(0.01f, full));

        /// <summary>
        /// 撃つかどうか。待ち始めは good、待ち時間 patience を使い切ると min まで下がる「これなら撃つ」の目安と比べる。
        /// patience が 0 なら、min 以上ならすぐ撃つ。
        /// </summary>
        public static bool ShouldFire(float value, float waitedSeconds, float patienceSeconds, float good, float min)
        {
            var threshold = patienceSeconds <= 0f
                ? min
                : Mathf.Lerp(Mathf.Max(good, min), min, Mathf.Clamp01(waitedSeconds / patienceSeconds));
            return (value >= threshold && value > 0f) || (min <= 0f && waitedSeconds >= patienceSeconds);
        }

        /// <summary>
        /// 握っている長さで距離が決まるとき、目当ての距離に届くまで握る秒数（距離は min〜max を secondsToMax 秒でまっすぐ伸びるとみなす）。
        /// </summary>
        public static float HoldSecondsFor(float targetDistance, float minDistance, float maxDistance, float secondsToMax)
        {
            if (maxDistance <= minDistance + 0.01f || secondsToMax <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((targetDistance - minDistance) / (maxDistance - minDistance)) * secondsToMax;
        }
    }
}
