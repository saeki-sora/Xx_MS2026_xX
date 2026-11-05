using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の全員分の様子（P1〜P4 の席ごと）と残り時間。ホストが持って全員に配り、画面（<see cref="PracticeView"/>）はこれだけを見て描く。
    /// 握力と熱は 0〜255 の段階で比べる（細かい揺れで「変わった」としない＝送りすぎない）。課題の進みは戻らない。
    /// </summary>
    public sealed class PracticeBoard
    {
        public const int SeatCount = 4;

        public struct Seat
        {
            public bool present;
            public byte grip;
            public byte heat;
            public bool gripping;
            public bool overheated;
            public PracticeStep step;

            public float Grip01 => grip / 255f;
            public float Heat01 => heat / 255f;
            public bool IsDone => step == PracticeStep.Done;
        }

        public readonly Seat[] seats = new Seat[SeatCount];

        /// <summary>残り秒（0 で時間切れ）。</summary>
        public float remaining;

        /// <summary>練習が終わった（全員できた、または時間切れ）。</summary>
        public bool finished;

        /// <summary>配る中身が変わるたびに増える。</summary>
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

        public int DoneCount
        {
            get
            {
                var n = 0;
                foreach (var seat in seats)
                {
                    n += seat.present && seat.IsDone ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>いる全員ができた（1人以上いるとき）。</summary>
        public bool AllDone => PresentCount > 0 && DoneCount == PresentCount;

        public bool TimeUp => remaining <= 0f;

        public void Reset(float timeLimitSeconds)
        {
            for (var i = 0; i < SeatCount; i++)
            {
                seats[i] = new Seat { present = seats[i].present };
            }

            remaining = Mathf.Max(0f, timeLimitSeconds);
            finished = false;
            Touch();
        }

        public void SetPresent(int seat, bool present)
        {
            if (IsValid(seat) && seats[seat].present != present)
            {
                seats[seat].present = present;
                Touch();
            }
        }

        /// <summary>その席の様子を入れる（課題は戻さない。終わった後は全員「できた」のまま）。</summary>
        public void SetSeat(int seat, float grip01, float heat01, bool gripping, bool overheated, PracticeStep step)
        {
            if (!IsValid(seat))
            {
                return;
            }

            var current = seats[seat];
            var next = current;
            next.grip = ToByte(grip01);
            next.heat = ToByte(heat01);
            next.gripping = gripping;
            next.overheated = overheated;
            next.step = step > current.step ? step : current.step;
            if (finished && next.present)
            {
                next.step = PracticeStep.Done;
            }

            if (next.grip != current.grip || next.heat != current.heat || next.gripping != current.gripping || next.overheated != current.overheated || next.step != current.step)
            {
                seats[seat] = next;
                Touch();
            }
        }

        /// <summary>残り時間を進める（1 秒の位が変わったら「変わった」とする）。</summary>
        public void Tick(float dt)
        {
            if (finished || remaining <= 0f)
            {
                return;
            }

            var before = Mathf.CeilToInt(remaining);
            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, dt));
            if (Mathf.CeilToInt(remaining) != before)
            {
                Touch();
            }
        }

        /// <summary>練習を終える（いる人はみんな「できた」にする）。</summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            for (var i = 0; i < SeatCount; i++)
            {
                if (seats[i].present)
                {
                    seats[i].step = PracticeStep.Done;
                }
            }

            Touch();
        }

        /// <summary>届いた中身で置き換える（参加側。exceptSeat の席＝自分は、自分の PC の方が新しいので課題だけ受け取る）。</summary>
        public void Apply(float newRemaining, bool newFinished, Seat[] incoming, int exceptSeat)
        {
            remaining = Mathf.Max(0f, newRemaining);
            finished = newFinished;
            for (var i = 0; i < SeatCount && i < incoming.Length; i++)
            {
                var present = seats[i].present;
                if (i == exceptSeat)
                {
                    if (incoming[i].step > seats[i].step)
                    {
                        seats[i].step = incoming[i].step;
                    }

                    continue;
                }

                seats[i] = incoming[i];
                seats[i].present = present;
            }

            Touch();
        }

        public static byte ToByte(float value01) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value01) * 255f);

        public static bool IsValid(int seat) => seat >= 0 && seat < SeatCount;

        private void Touch() => Version++;
    }
}
