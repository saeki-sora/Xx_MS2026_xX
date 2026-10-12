using System;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラクター選択の状態（4人分のカーソル・決定したか・残り時間）と、そのルール。
    /// ネット対戦ではホストだけが動かし、全員に丸ごと配る（<c>CharacterSelectMessages</c>）。
    /// Unity にも通信にも頼らない純粋なデータなので、テストで中身を確かめられる。
    /// </summary>
    public sealed class CharacterSelectBoard
    {
        public const int SeatCount = 4;

        public struct Seat
        {
            /// <summary>この席に人がいる（選択に参加している）。</summary>
            public bool active;

            /// <summary>カーソルがあるキャラの番号。</summary>
            public int cursor;

            /// <summary>キャラを決定した。</summary>
            public bool confirmed;
        }

        public readonly Seat[] seats = new Seat[SeatCount];

        /// <summary>選べるキャラの数。</summary>
        public int CharacterCount { get; private set; }

        /// <summary>true なら、決定済みのキャラを他の人も選べる。</summary>
        public bool AllowDuplicates { get; private set; }

        /// <summary>制限時間があるか（0 以下なら無し）。</summary>
        public bool HasTimeLimit { get; private set; }

        /// <summary>残り時間（秒）。<see cref="Tick"/> で減る。</summary>
        public float remaining;

        /// <summary>全員のキャラが決まった（次の画面へ進むところ）。</summary>
        public bool Finished { get; private set; }

        /// <summary>カーソル・決定・参加者などが変わるたびに増える（残り時間の減少では増えない）。</summary>
        public int Version { get; private set; }

        public void Reset(int characterCount, bool allowDuplicates, float timeLimitSeconds)
        {
            CharacterCount = Math.Max(0, characterCount);
            AllowDuplicates = allowDuplicates;
            HasTimeLimit = timeLimitSeconds > 0f;
            remaining = HasTimeLimit ? timeLimitSeconds : 0f;
            Finished = false;
            for (var i = 0; i < SeatCount; i++)
            {
                seats[i] = default;
            }

            Version++;
        }

        public static bool IsValidSeat(int seat) => seat >= 0 && seat < SeatCount;

        public bool IsValidCharacter(int character) => character >= 0 && character < CharacterCount;

        public void SetAllowDuplicates(bool allow)
        {
            if (AllowDuplicates == allow)
            {
                return;
            }

            AllowDuplicates = allow;
            if (!allow)
            {
                // 同じキャラを何人かが決定していたら、番号の小さい席の人だけ残し、ほかの人の決定を外す。
                for (var seat = 0; seat < SeatCount; seat++)
                {
                    if (seats[seat].active && seats[seat].confirmed && FirstOwnerBefore(seats[seat].cursor, seat) >= 0)
                    {
                        seats[seat].confirmed = false;
                    }
                }
            }

            Version++;
        }

        private int FirstOwnerBefore(int character, int seat)
        {
            for (var other = 0; other < seat; other++)
            {
                if (seats[other].active && seats[other].confirmed && seats[other].cursor == character)
                {
                    return other;
                }
            }

            return -1;
        }

        /// <summary>席に人が入った／抜けた。入った人のカーソルは、席の番号のキャラ（使われていれば次の空き）に置く。</summary>
        public bool SetActive(int seat, bool active)
        {
            if (!IsValidSeat(seat) || seats[seat].active == active)
            {
                return false;
            }

            if (active)
            {
                var start = CharacterCount > 0 ? seat % CharacterCount : 0;
                var cursor = IsLockedFor(start, seat) ? FindNextFree(seat, start) : start;
                seats[seat] = new Seat { active = true, cursor = cursor < 0 ? start : cursor, confirmed = false };
            }
            else
            {
                // 抜けた人が決定していたキャラは、他の人が選べるようになる。
                seats[seat].active = false;
                seats[seat].confirmed = false;
            }

            Version++;
            return true;
        }

        /// <summary>カーソルを次のキャラへ（決定済みで選べないキャラは飛ばす）。動いたら true。</summary>
        public bool MoveNext(int seat)
        {
            if (!CanAct(seat) || seats[seat].confirmed)
            {
                return false;
            }

            var next = FindNextFree(seat, seats[seat].cursor);
            if (next < 0 || next == seats[seat].cursor)
            {
                return false;
            }

            seats[seat].cursor = next;
            Version++;
            return true;
        }

        /// <summary>今のカーソルのキャラで決定する。決定できたら true。</summary>
        public bool Confirm(int seat)
        {
            if (!CanAct(seat) || seats[seat].confirmed || !IsValidCharacter(seats[seat].cursor) || IsLockedFor(seats[seat].cursor, seat))
            {
                return false;
            }

            seats[seat].confirmed = true;
            if (!AllowDuplicates)
            {
                // 同じキャラにカーソルを置いていた（まだ決めていない）人は、次の空いているキャラへ動かす。
                for (var other = 0; other < SeatCount; other++)
                {
                    if (other != seat && seats[other].active && !seats[other].confirmed && seats[other].cursor == seats[seat].cursor)
                    {
                        var next = FindNextFree(other, seats[other].cursor);
                        if (next >= 0)
                        {
                            seats[other].cursor = next;
                        }
                    }
                }
            }

            Version++;
            return true;
        }

        /// <summary>決定を取り消す。取り消せたら true。</summary>
        public bool Cancel(int seat)
        {
            if (!CanAct(seat) || !seats[seat].confirmed)
            {
                return false;
            }

            seats[seat].confirmed = false;
            Version++;
            return true;
        }

        /// <summary>残り時間を減らす（制限時間が無ければ何もしない）。</summary>
        public void Tick(float deltaSeconds)
        {
            if (HasTimeLimit && !Finished)
            {
                remaining = Math.Max(0f, remaining - deltaSeconds);
            }
        }

        public bool TimeUp => HasTimeLimit && remaining <= 0f;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var s in seats)
                {
                    count += s.active ? 1 : 0;
                }

                return count;
            }
        }

        public int ConfirmedCount
        {
            get
            {
                var count = 0;
                foreach (var s in seats)
                {
                    count += s.active && s.confirmed ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>参加している全員が決定した。</summary>
        public bool AllConfirmed => ActiveCount > 0 && ConfirmedCount == ActiveCount;

        /// <summary>
        /// 選択を終える（時間切れ・全員決定）。まだ決めていない人は今のカーソルのキャラで決定する。
        /// そのキャラが使われていれば空いているキャラにし、空きが無いときだけ同じキャラを使わせる（誰も選べずに止まらないように）。
        /// </summary>
        public void Finish()
        {
            if (Finished)
            {
                return;
            }

            for (var seat = 0; seat < SeatCount; seat++)
            {
                if (!seats[seat].active || seats[seat].confirmed)
                {
                    continue;
                }

                if (IsLockedFor(seats[seat].cursor, seat))
                {
                    var free = FindNextFree(seat, seats[seat].cursor);
                    if (free >= 0)
                    {
                        seats[seat].cursor = free;
                    }
                }

                seats[seat].confirmed = IsValidCharacter(seats[seat].cursor);
            }

            Finished = true;
            Version++;
        }

        /// <summary>そのキャラを決定した席（<paramref name="exceptSeat"/> 以外で最初の1人。いなければ -1）。</summary>
        public int ConfirmedOwner(int character, int exceptSeat = -1)
        {
            for (var seat = 0; seat < SeatCount; seat++)
            {
                if (seat != exceptSeat && seats[seat].active && seats[seat].confirmed && seats[seat].cursor == character)
                {
                    return seat;
                }
            }

            return -1;
        }

        /// <summary>その席の人から見て、そのキャラが「他の人が決定済みで選べない」か。</summary>
        public bool IsLockedFor(int character, int seat) => !AllowDuplicates && ConfirmedOwner(character, seat) >= 0;

        /// <summary>各席の決まったキャラの番号（参加していない・決めていない席は -1）。</summary>
        public int[] GetChosen()
        {
            var chosen = new int[SeatCount];
            for (var seat = 0; seat < SeatCount; seat++)
            {
                var s = seats[seat];
                chosen[seat] = s.active && s.confirmed && IsValidCharacter(s.cursor) ? s.cursor : -1;
            }

            return chosen;
        }

        /// <summary>届いた状態で丸ごと置き換える（参加側）。</summary>
        public void Apply(int characterCount, bool allowDuplicates, bool hasTimeLimit, float remainingSeconds, bool finished, Seat[] incoming)
        {
            CharacterCount = Math.Max(0, characterCount);
            AllowDuplicates = allowDuplicates;
            HasTimeLimit = hasTimeLimit;
            remaining = remainingSeconds;
            Finished = finished;
            for (var i = 0; i < SeatCount && i < incoming.Length; i++)
            {
                seats[i] = incoming[i];
            }

            Version++;
        }

        private bool CanAct(int seat) => IsValidSeat(seat) && seats[seat].active && !Finished && CharacterCount > 0;

        /// <summary>from の次から順に探して、その席の人が選べる最初のキャラ（無ければ -1。from 自身は最後に調べる）。</summary>
        private int FindNextFree(int seat, int from)
        {
            if (CharacterCount <= 0)
            {
                return -1;
            }

            for (var step = 1; step <= CharacterCount; step++)
            {
                var candidate = ((from + step) % CharacterCount + CharacterCount) % CharacterCount;
                if (!IsLockedFor(candidate, seat))
                {
                    return candidate;
                }
            }

            return -1;
        }
    }
}
