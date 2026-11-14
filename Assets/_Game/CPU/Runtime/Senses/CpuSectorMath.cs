using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 砲台のまわりを「向き」で区切った扇（セクター）の計算（決まりだけ。テストできるように物理や群衆には触らない）。
    /// 角度は度で、ワールドの右（+X）が 0、反時計回りが正（Unity の Z 回転と同じ向き）。
    /// </summary>
    public static class CpuSectorMath
    {
        /// <summary>扇の数の既定（6 度ずつ）。</summary>
        public const int DefaultSectorCount = 60;

        /// <summary>0〜360 に収めた角度。</summary>
        public static float Wrap360(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        /// <summary>向き（ベクトル）の角度。</summary>
        public static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        /// <summary>角度が入る扇の番号。</summary>
        public static int SectorOf(float degrees, int sectorCount)
        {
            var index = Mathf.FloorToInt(Wrap360(degrees) / 360f * sectorCount);
            return index >= sectorCount ? sectorCount - 1 : index;
        }

        /// <summary>扇の真ん中の角度。</summary>
        public static float CenterOf(int sector, int sectorCount) => (sector + 0.5f) * 360f / sectorCount;

        /// <summary>
        /// 回る向き（sign: 反時計回り +1、時計回り -1、回らない 0）に、今の向き aim から fromDeg〜toDeg 度先までの扇の重みの合計。
        /// 今の向きの扇（fromDeg ≤ 0 のとき）も含む。sign が 0 なら今の向きの扇だけ。
        /// </summary>
        public static float SumAhead(float[] weights, float aimDegrees, float sign, float fromDegrees, float toDegrees)
        {
            if (weights == null || weights.Length == 0 || toDegrees < fromDegrees)
            {
                return 0f;
            }

            var count = weights.Length;
            var width = 360f / count;
            var current = SectorOf(aimDegrees, count);
            if (Mathf.Abs(sign) < 0.5f)
            {
                return fromDegrees <= 0f ? weights[current] : 0f;
            }

            var sum = 0f;
            for (var index = 0; index < count; index++)
            {
                var enter = index == current ? 0f : AngleToEnter(aimDegrees, index, sign > 0f, width);
                if (enter >= fromDegrees && enter <= toDegrees)
                {
                    sum += weights[index];
                }
            }

            return sum;
        }

        /// <summary><see cref="SumAhead"/> と同じ範囲の扇のうち、いちばん重い扇の重み。</summary>
        public static float MaxAhead(float[] weights, float aimDegrees, float sign, float fromDegrees, float toDegrees)
        {
            if (weights == null || weights.Length == 0 || toDegrees < fromDegrees)
            {
                return 0f;
            }

            var count = weights.Length;
            var width = 360f / count;
            var current = SectorOf(aimDegrees, count);
            if (Mathf.Abs(sign) < 0.5f)
            {
                return fromDegrees <= 0f ? weights[current] : 0f;
            }

            var max = 0f;
            for (var index = 0; index < count; index++)
            {
                var enter = index == current ? 0f : AngleToEnter(aimDegrees, index, sign > 0f, width);
                if (enter >= fromDegrees && enter <= toDegrees && weights[index] > max)
                {
                    max = weights[index];
                }
            }

            return max;
        }

        /// <summary>回る向きに進んで、扇 index に入り始めるまでの角度（0〜360）。</summary>
        public static float AngleToEnter(float aimDegrees, int index, bool counterClockwise, float width)
        {
            return counterClockwise
                ? Wrap360(index * width - aimDegrees)
                : Wrap360(aimDegrees - (index + 1) * width);
        }

        /// <summary>
        /// 扇ごとの重みを足し込む。点 point が origin から range 以内で、その扇の障害物の距離（obstacleDistances。無ければ制限なし）より手前なら足す。
        /// 見える範囲（hasView のとき view の中）だけ。足したら true。
        /// </summary>
        public static bool Accumulate(float[] weights, float[] obstacleDistances, Vector2 origin, float range,
            bool hasView, in CpuView view, Vector2 point, float weight)
        {
            if (weights == null || !TryLocate(weights.Length, obstacleDistances, origin, range, hasView, view, point, out var sector, out _))
            {
                return false;
            }

            weights[sector] += weight;
            return true;
        }

        /// <summary>
        /// 点が「見えて・ビームが届く」なら、その扇の番号と距離を返す（<see cref="Accumulate"/> と同じ条件）。
        /// </summary>
        public static bool TryLocate(int sectorCount, float[] obstacleDistances, Vector2 origin, float range, bool hasView, in CpuView view,
            Vector2 point, out int sector, out float distance)
        {
            sector = -1;
            distance = 0f;
            var d = point - origin;
            var sqr = d.sqrMagnitude;
            if (sqr > range * range || sectorCount <= 0)
            {
                return false;
            }

            if (hasView && !view.Contains(point))
            {
                return false;
            }

            sector = SectorOf(AngleOf(d), sectorCount);
            if (obstacleDistances != null && sector < obstacleDistances.Length)
            {
                var limit = obstacleDistances[sector];
                if (sqr > limit * limit)
                {
                    sector = -1;
                    return false;
                }
            }

            distance = Mathf.Sqrt(sqr);
            return true;
        }
    }
}
