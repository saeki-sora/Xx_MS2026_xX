using System;
using System.Collections.Generic;

namespace MS2026.Fortress
{
    /// <summary>
    /// ステージの仕掛け（歯車・動く障害物）の計算だけ（2026-10-29）。時刻（<see cref="GimmickClock.Time"/>）から形を決めるので、
    /// 同じ時刻なら全員のPC・エディタのプレビューで同じ形になる。UnityEngine を使わない（テストしやすいように）。
    /// </summary>
    public static class GimmickMath
    {
        public const float MinSeconds = 0.05f;

        /// <summary>なめらかに動き出して、なめらかに止まる（0〜1 → 0〜1）。</summary>
        public static float Ease(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// 「止まっては回る」歯車の角度（度。0 から増えていく。向きは呼ぶ側で掛ける）。
        /// firstTurnAt 秒に1回目を回り始め、turnSeconds かけて stepDegrees 回り、waitSeconds 止まって、また回る。
        /// turnIndex = 何回目の回転か（0 から。まだ1回も回っていなければ -1）、turning = 今回っている途中か。
        /// </summary>
        public static float StepAngle(float t, float firstTurnAt, float waitSeconds, float turnSeconds, float stepDegrees,
            out bool turning, out int turnIndex)
        {
            turning = false;
            turnIndex = -1;
            if (t < firstTurnAt)
            {
                return 0f;
            }

            var turn = Math.Max(MinSeconds, turnSeconds);
            var period = turn + Math.Max(0f, waitSeconds);
            var local = (double)t - firstTurnAt;
            var n = Math.Floor(local / period);
            var within = (float)(local - n * period);
            turnIndex = (int)Math.Min(n, int.MaxValue);
            var steps = n;
            if (within < turn)
            {
                turning = true;
                steps += Ease(within / turn);
            }
            else
            {
                steps += 1.0;
            }

            return (float)Wrap(steps * stepDegrees);
        }

        /// <summary>「ずっと回る」歯車の角度（度）。startAt 秒から degreesPerSecond で回る。</summary>
        public static float ContinuousAngle(float t, float startAt, float degreesPerSecond)
        {
            if (t <= startAt)
            {
                return 0f;
            }

            return (float)Wrap(((double)t - startAt) * degreesPerSecond);
        }

        /// <summary>かみ合った歯車の角度: 相手と反対向きに、歯の数の比で回る。</summary>
        public static float FollowAngle(float driverAngle, int driverTeeth, int teeth)
        {
            var ratio = Math.Max(1, driverTeeth) / (double)Math.Max(1, teeth);
            return (float)Wrap(-driverAngle * ratio);
        }

        /// <summary>角度を ±360 の中に収める（何回も回った角度でも小数が崩れないように）。</summary>
        public static double Wrap(double degrees)
        {
            var r = degrees % 360.0;
            return r;
        }

        /// <summary>角度の差（-180〜180）。</summary>
        public static float DeltaAngle(float from, float to)
        {
            var d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        // ───────── 動く障害物 ─────────

        /// <summary>止まる所1つ分（出発点からのずれ・回す角度・ここへ動く秒数・ここで止まる秒数）。</summary>
        public struct Stop
        {
            public float X;
            public float Y;
            public float Angle;
            public float MoveSeconds;
            public float WaitSeconds;

            public Stop(float x, float y, float angle, float moveSeconds, float waitSeconds)
            {
                X = x;
                Y = y;
                Angle = angle;
                MoveSeconds = moveSeconds;
                WaitSeconds = waitSeconds;
            }
        }

        /// <summary>ある時刻の動く障害物の様子。</summary>
        public struct MoverPose
        {
            public float X;
            public float Y;
            public float Angle;

            /// <summary>動いている途中か。</summary>
            public bool Moving;

            /// <summary>何番目の移動か（0 から。まだ動いていなければ -1）。</summary>
            public int LegIndex;

            /// <summary>今向かっている（止まっているならいる）止まる所の番号。</summary>
            public int TargetStop;
        }

        /// <summary>1回分の移動（from の止まる所から to へ、move 秒かけて動き、wait 秒止まる）。</summary>
        public struct Leg
        {
            public int From;
            public int To;
            public float Move;
            public float Wait;
            public float Length => Move + Wait;
        }

        /// <summary>
        /// 移動の並び（1周分）を作る。loop: 0 = 最初に戻ってくり返す、1 = 行ったり来たり、2 = 1回だけ。
        /// 止まる所が2つ未満なら空（動かない）。
        /// </summary>
        public static void BuildLegs(IReadOnlyList<Stop> stops, int loop, List<Leg> legs)
        {
            legs.Clear();
            if (stops == null || stops.Count < 2)
            {
                return;
            }

            for (var i = 1; i < stops.Count; i++)
            {
                legs.Add(MakeLeg(stops, i - 1, i, stops[i].MoveSeconds, stops[i].WaitSeconds));
            }

            if (loop == 0)
            {
                legs.Add(MakeLeg(stops, stops.Count - 1, 0, stops[0].MoveSeconds, stops[0].WaitSeconds));
            }
            else if (loop == 1)
            {
                for (var i = stops.Count - 1; i > 0; i--)
                {
                    // 戻るときも、行きと同じ秒数でその区間を動く。止まる秒数は着いた所の物。
                    legs.Add(MakeLeg(stops, i, i - 1, stops[i].MoveSeconds, stops[i - 1].WaitSeconds));
                }
            }
        }

        private static Leg MakeLeg(IReadOnlyList<Stop> stops, int from, int to, float move, float wait)
        {
            return new Leg
            {
                From = from,
                To = to,
                Move = Math.Max(MinSeconds, move),
                Wait = Math.Max(0f, wait)
            };
        }

        /// <summary>
        /// 動く障害物の、ある時刻の様子。firstMoveAt 秒までは出発点（止まる所 0）。
        /// legs は <see cref="BuildLegs"/> で作った物。repeat が false（1回だけ）なら、最後に着いた所で止まったまま。
        /// </summary>
        public static MoverPose EvaluateMover(float t, float firstMoveAt, IReadOnlyList<Stop> stops, IReadOnlyList<Leg> legs, bool repeat)
        {
            var pose = new MoverPose { LegIndex = -1, TargetStop = 0 };
            if (stops == null || stops.Count == 0)
            {
                return pose;
            }

            SetAt(ref pose, stops[0]);
            if (legs == null || legs.Count == 0 || t < firstMoveAt)
            {
                return pose;
            }

            var cycle = 0.0;
            foreach (var leg in legs)
            {
                cycle += leg.Length;
            }

            var local = (double)t - firstMoveAt;
            var round = 0.0;
            if (repeat && cycle > 0.0)
            {
                round = Math.Floor(local / cycle);
                local -= round * cycle;
            }

            for (var i = 0; i < legs.Count; i++)
            {
                var leg = legs[i];
                pose.LegIndex = (int)Math.Min(round * legs.Count + i, int.MaxValue);
                pose.TargetStop = leg.To;
                if (local < leg.Move)
                {
                    var k = Ease((float)(local / leg.Move));
                    var a = stops[leg.From];
                    var b = stops[leg.To];
                    pose.X = a.X + (b.X - a.X) * k;
                    pose.Y = a.Y + (b.Y - a.Y) * k;
                    pose.Angle = a.Angle + (b.Angle - a.Angle) * k;
                    pose.Moving = true;
                    return pose;
                }

                local -= leg.Move;
                if (local < leg.Wait || i == legs.Count - 1)
                {
                    SetAt(ref pose, stops[leg.To]);
                    return pose;
                }

                local -= leg.Wait;
            }

            SetAt(ref pose, stops[legs[legs.Count - 1].To]);
            return pose;
        }

        private static void SetAt(ref MoverPose pose, Stop stop)
        {
            pose.X = stop.X;
            pose.Y = stop.Y;
            pose.Angle = stop.Angle;
            pose.Moving = false;
        }

        // ───────── いつ動くか（エディタの時間割に使う） ─────────

        /// <summary>「止まっては回る」歯車が回っている時間帯を、0〜horizon 秒の中で集める（最大 maxCount 個）。</summary>
        public static void StepIntervals(float firstTurnAt, float waitSeconds, float turnSeconds, float horizon,
            List<(float start, float end)> result, int maxCount = 400)
        {
            var turn = Math.Max(MinSeconds, turnSeconds);
            var period = turn + Math.Max(0f, waitSeconds);
            for (var start = (double)firstTurnAt; start < horizon && result.Count < maxCount; start += period)
            {
                result.Add(((float)start, (float)Math.Min(horizon, start + turn)));
            }
        }

        /// <summary>動く障害物が動いている時間帯を、0〜horizon 秒の中で集める（最大 maxCount 個）。</summary>
        public static void MoverIntervals(float firstMoveAt, IReadOnlyList<Leg> legs, bool repeat, float horizon,
            List<(float start, float end)> result, int maxCount = 400)
        {
            if (legs == null || legs.Count == 0)
            {
                return;
            }

            var t = (double)firstMoveAt;
            while (t < horizon && result.Count < maxCount)
            {
                var cycleStart = t;
                foreach (var leg in legs)
                {
                    if (t >= horizon || result.Count >= maxCount)
                    {
                        break;
                    }

                    result.Add(((float)t, (float)Math.Min(horizon, t + leg.Move)));
                    t += leg.Length;
                }

                if (!repeat || t <= cycleStart)
                {
                    break;
                }
            }
        }
    }
}
