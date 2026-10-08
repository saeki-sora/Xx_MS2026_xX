using System;

namespace MS2026.UI.Game
{
    /// <summary>部屋（ロビー）の進み具合。ホストが決めて全員に配る。</summary>
    public enum LobbyPhase : byte
    {
        /// <summary>集まっているところ（準備OKを待つ）。</summary>
        Waiting = 0,

        /// <summary>開始のカウントダウン中。</summary>
        Countdown = 1,

        /// <summary>ゲームのシーンを読み込んでいる。</summary>
        Loading = 2,

        /// <summary>試合中（ゲームのシーンにいる）。</summary>
        InGame = 3,

        /// <summary>全員でロビーに戻っている途中。</summary>
        Returning = 4
    }

    /// <summary>
    /// 部屋にいる人の一覧（P1〜P4の席ごと）と部屋の状態。ホストが持ち、変わるたびに全員へ配る（<see cref="LobbyMessages"/>）。
    /// Unity にも通信にも頼らない純粋なデータなので、テストで中身を確かめられる。
    /// </summary>
    [Serializable]
    public sealed class LobbyRoster
    {
        public const int SeatCount = 4;
        public const int MaxNameLength = 12;

        [Serializable]
        public struct Seat
        {
            public bool present;
            public ulong clientId;
            public string name;
            public bool ready;
            public bool isHost;
            public ushort pingMs;
        }

        public readonly Seat[] seats = new Seat[SeatCount];
        public LobbyPhase phase;

        /// <summary>カウントダウンの残り秒（カウントダウン中だけ意味がある）。</summary>
        public float countdownRemaining;

        /// <summary>中身が変わるたびに増える（画面の更新や送信の判定に使う）。</summary>
        public int Version { get; private set; }

        public int PresentCount
        {
            get
            {
                var n = 0;
                foreach (var seat in seats)
                {
                    n += seat.present ? 1 : 0;
                }

                return n;
            }
        }

        public int ReadyCount
        {
            get
            {
                var n = 0;
                foreach (var seat in seats)
                {
                    n += seat.present && seat.ready ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>部屋にいる全員が準備OK（1人以上いるとき）。</summary>
        public bool AllReady => PresentCount > 0 && ReadyCount == PresentCount;

        /// <summary>開始できるか（待っている状態で、minPlayers 人以上いて、全員準備OK。force なら準備は問わない）。</summary>
        public bool CanStart(int minPlayers, bool force)
        {
            return phase == LobbyPhase.Waiting && PresentCount >= Math.Max(1, minPlayers) && (force || AllReady);
        }

        public void Clear()
        {
            for (var i = 0; i < SeatCount; i++)
            {
                seats[i] = default;
            }

            phase = LobbyPhase.Waiting;
            countdownRemaining = 0f;
            Touch();
        }

        /// <summary>席に人を入れる（名前は後から <see cref="SetName"/> で入ることもある）。</summary>
        public void Join(int seat, ulong clientId, string name, bool isHost)
        {
            if (!IsValid(seat))
            {
                return;
            }

            seats[seat] = new Seat { present = true, clientId = clientId, name = CleanName(name, seat), isHost = isHost };
            Touch();
        }

        public void Leave(int seat)
        {
            if (IsValid(seat) && seats[seat].present)
            {
                seats[seat] = default;
                Touch();
            }
        }

        public void SetName(int seat, string name)
        {
            if (!IsValid(seat) || !seats[seat].present)
            {
                return;
            }

            var clean = CleanName(name, seat);
            if (seats[seat].name != clean)
            {
                seats[seat].name = clean;
                Touch();
            }
        }

        public void SetReady(int seat, bool ready)
        {
            if (IsValid(seat) && seats[seat].present && seats[seat].ready != ready)
            {
                seats[seat].ready = ready;
                Touch();
            }
        }

        /// <summary>全員の準備OKを外す（ロビーに戻ってきたときなど）。</summary>
        public void ClearReady()
        {
            var changed = false;
            for (var i = 0; i < SeatCount; i++)
            {
                changed |= seats[i].ready;
                seats[i].ready = false;
            }

            if (changed)
            {
                Touch();
            }
        }

        /// <summary>通信の遅れを入れる。小さな揺れでは配り直さないよう、10ms以上変わったときだけ「変わった」とする。</summary>
        public bool SetPing(int seat, int pingMs)
        {
            if (!IsValid(seat) || !seats[seat].present)
            {
                return false;
            }

            var clamped = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, pingMs));
            if (Math.Abs(seats[seat].pingMs - clamped) < 10)
            {
                return false;
            }

            seats[seat].pingMs = clamped;
            Touch();
            return true;
        }

        public void SetPhase(LobbyPhase newPhase, float countdown = 0f)
        {
            if (phase != newPhase || Math.Abs(countdownRemaining - countdown) > 0.001f)
            {
                phase = newPhase;
                countdownRemaining = countdown;
                Touch();
            }
        }

        /// <summary>その通信相手の席（いなければ -1）。</summary>
        public int SeatOf(ulong clientId)
        {
            for (var i = 0; i < SeatCount; i++)
            {
                if (seats[i].present && seats[i].clientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>他の roster の中身をそのまま写す（参加側が、届いた内容で置き換えるときに使う）。</summary>
        public void CopyFrom(LobbyRoster other)
        {
            Array.Copy(other.seats, seats, SeatCount);
            phase = other.phase;
            countdownRemaining = other.countdownRemaining;
            Touch();
        }

        /// <summary>名前を整える（前後の空白を消し、長すぎれば切る。空なら「P1」など）。</summary>
        public static string CleanName(string name, int seat)
        {
            var trimmed = (name ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }

            return trimmed.Length > 0 ? trimmed : $"P{seat + 1}";
        }

        public static bool IsValid(int seat) => seat >= 0 && seat < SeatCount;

        private void Touch() => Version++;
    }
}
