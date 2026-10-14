namespace MS2026.Fortress
{
    /// <summary>
    /// 1人分のコンボ（倒した数が windowSeconds 以内に続いた数）と、その試合の最大コンボを数える（Unity を使わないのでテストできる）。
    /// Host が4人分を持ち、リザルトの「最大コンボ」に使う。最初に見た数は「前から」として扱う（途中から数え始めてもコンボにしない）。
    /// </summary>
    public sealed class ComboTracker
    {
        private int _lastKills = -1;
        private float _lastKillTime;

        public float windowSeconds = 2.5f;

        /// <summary>今のコンボ。</summary>
        public int Combo { get; private set; }

        /// <summary>この試合の最大コンボ。</summary>
        public int Max { get; private set; }

        public void Reset()
        {
            _lastKills = -1;
            Combo = 0;
            Max = 0;
        }

        /// <summary>今までに倒した数（累計）と今の時刻を渡す。</summary>
        public void Update(int kills, float now)
        {
            if (_lastKills < 0 || kills < _lastKills)
            {
                _lastKills = kills;
                Combo = 0;
                return;
            }

            var delta = kills - _lastKills;
            _lastKills = kills;
            if (delta > 0)
            {
                if (Combo == 0 || now - _lastKillTime > windowSeconds)
                {
                    Combo = 0;
                }

                Combo += delta;
                _lastKillTime = now;
                if (Combo > Max)
                {
                    Max = Combo;
                }
            }
            else if (Combo > 0 && now - _lastKillTime > windowSeconds)
            {
                Combo = 0;
            }
        }
    }
}
