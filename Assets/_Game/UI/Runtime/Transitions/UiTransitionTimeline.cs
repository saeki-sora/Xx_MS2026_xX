namespace MS2026.UI
{
    /// <summary>
    /// 画面切り替えの幕の時間の流れ（覆う → 待つ → 開く）を計算する純粋なクラス（テスト可能）。
    /// 「待つ」は保持時間が過ぎて、かつ「準備ができた」（シーンの読み込み完了など）まで続く。
    /// </summary>
    public sealed class UiTransitionTimeline
    {
        public enum Phase
        {
            Idle,
            Covering,
            Holding,
            Revealing
        }

        private float _cover;
        private float _hold;
        private float _reveal;
        private float _elapsed;

        public Phase Current { get; private set; } = Phase.Idle;

        /// <summary>覆っている割合（0〜1、緩急をかける前）。</summary>
        public float Progress { get; private set; }

        public bool IsRunning => Current != Phase.Idle;

        public void Start(float coverSeconds, float holdSeconds, float revealSeconds)
        {
            _cover = coverSeconds > 0f ? coverSeconds : 0.0001f;
            _hold = holdSeconds > 0f ? holdSeconds : 0f;
            _reveal = revealSeconds > 0f ? revealSeconds : 0.0001f;
            _elapsed = 0f;
            Progress = 0f;
            Current = Phase.Covering;
        }

        /// <summary>
        /// 時間を進める。戻り値: 今回「覆い終わった」なら CoveredNow、今回「開き終わった」なら FinishedNow。
        /// </summary>
        public (bool coveredNow, bool finishedNow) Tick(float deltaTime, bool ready)
        {
            var covered = false;
            var finished = false;
            _elapsed += deltaTime;

            switch (Current)
            {
                case Phase.Covering:
                    Progress = Clamp01(_elapsed / _cover);
                    if (_elapsed >= _cover)
                    {
                        Progress = 1f;
                        Current = Phase.Holding;
                        _elapsed = 0f;
                        covered = true;
                    }

                    break;
                case Phase.Holding:
                    Progress = 1f;
                    if (_elapsed >= _hold && ready)
                    {
                        Current = Phase.Revealing;
                        _elapsed = 0f;
                    }

                    break;
                case Phase.Revealing:
                    Progress = 1f - Clamp01(_elapsed / _reveal);
                    if (_elapsed >= _reveal)
                    {
                        Progress = 0f;
                        Current = Phase.Idle;
                        finished = true;
                    }

                    break;
            }

            return (covered, finished);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
