using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>席替えのジャンプの、ある瞬間の姿（<see cref="FortressSeatMath.Evaluate"/> の答え）。</summary>
    public readonly struct SeatJumpPose
    {
        public static readonly SeatJumpPose Rest = new SeatJumpPose(1f, 0f, 0f, true, true);

        /// <summary>前の席から次の席までの進み具合（0 = 前の席、1 = 次の席）。</summary>
        public readonly float Progress01;

        /// <summary>浮いている高さ（0 = 地面、1 = いちばん高い所）。</summary>
        public readonly float Height01;

        /// <summary>着地でつぶれている量（0 = 元の形）。横に広がり、縦に縮む。</summary>
        public readonly float Squash;

        /// <summary>着地したか。</summary>
        public readonly bool Landed;

        /// <summary>つぶれも戻って、動きが全部終わったか。</summary>
        public readonly bool Finished;

        public SeatJumpPose(float progress01, float height01, float squash, bool landed, bool finished)
        {
            Progress01 = progress01;
            Height01 = height01;
            Squash = squash;
            Landed = landed;
            Finished = finished;
        }
    }

    /// <summary>
    /// 席替えの計算（Unity の物に頼らない。テストあり。2026-10-31）。
    /// 「席」はステージの砲台の目印（Turret_P1〜P4）の番号で、試合の始まりはプレイヤー p が席 p に座っている。
    /// 席替えが 1 回進むたびに、全員が画面で時計回り（設定で反時計回り）に 1 つ隣の席へ移る。
    /// </summary>
    public static class FortressSeatMath
    {
        /// <summary>
        /// 席を、中心（コア）の周りを時計回りに回る順に並べる（席 0 から始める）。
        /// ステージ1（上・右・下・左）は 0,1,2,3、ステージ2（左上・右上・左下・右下）は 0,1,3,2 になる。
        /// </summary>
        public static int[] ClockwiseOrder(Vector2[] seats, Vector2 center)
        {
            var count = seats?.Length ?? 0;
            var order = new int[count];
            if (count == 0)
            {
                return order;
            }

            var angles = new float[count];
            for (var i = 0; i < count; i++)
            {
                order[i] = i;
                var d = seats[i] - center;
                angles[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }

            // 席 0 から時計回りに何度回った所にあるか（0 以上 360 未満）で並べる。同じ角度なら番号順。
            var start = angles[0];
            Array.Sort(order, (a, b) =>
            {
                var da = Mathf.Repeat(start - angles[a], 360f);
                var db = Mathf.Repeat(start - angles[b], 360f);
                if (a == 0)
                {
                    da = 0f;
                }

                if (b == 0)
                {
                    db = 0f;
                }

                var compare = da.CompareTo(db);
                return compare != 0 ? compare : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>
        /// 席替えを shift 回進めたとき、プレイヤー player が座っている席。
        /// order は <see cref="ClockwiseOrder"/> の並び（無い・合わないときは番号順）。clockwise が false なら逆向きに回る。
        /// </summary>
        public static int SeatOf(int player, int shift, int[] order, bool clockwise)
        {
            var count = order?.Length ?? 0;
            var home = count > 0 ? Array.IndexOf(order, player) : -1;
            if (home < 0)
            {
                return player;
            }

            var step = clockwise ? shift : -shift;
            return order[Mod(home + step, count)];
        }

        /// <summary>
        /// 席替えの回数 from から to への、いちばん近い進み方（+1 = 1 つ進む、−1 = 1 つ戻る、0 = 同じ）。
        /// 2 つ以上離れているとき（途中から入った・テストで飛ばした）は <see cref="int.MinValue"/>（跳ばずにその場へ置く）。
        /// </summary>
        public static int StepBetween(int from, int to, int seatCount)
        {
            if (seatCount <= 0)
            {
                return 0;
            }

            var diff = Mod(to - from, seatCount);
            if (diff == 0)
            {
                return 0;
            }

            if (diff == 1)
            {
                return 1;
            }

            return diff == seatCount - 1 ? -1 : int.MinValue;
        }

        /// <summary>
        /// 跳び始めてから t 秒の姿。jumpSeconds で次の席へ（横はなめらかに、高さは山なり）、
        /// 着地したら squash だけつぶれて squashSeconds で戻る（開始時の「ドスン」と同じ戻り方）。
        /// </summary>
        public static SeatJumpPose Evaluate(float t, float jumpSeconds, float squash, float squashSeconds)
        {
            if (t <= 0f)
            {
                return new SeatJumpPose(0f, 0f, 0f, false, false);
            }

            var jump = Mathf.Max(0.0001f, jumpSeconds);
            if (t < jump)
            {
                var p = t / jump;
                return new SeatJumpPose(EaseInOutCubic(p), Mathf.Sin(Mathf.PI * p), 0f, false, false);
            }

            var u = squashSeconds > 0f ? (t - jump) / squashSeconds : 1f;
            if (u >= 1f || squash <= 0f)
            {
                return SeatJumpPose.Rest;
            }

            // 一気につぶれて、少しゆっくり戻る。
            var k = Mathf.Sin(Mathf.PI * u) * (1f - u * 0.5f);
            return new SeatJumpPose(1f, 0f, squash * k, true, false);
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static int Mod(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;
    }
}
