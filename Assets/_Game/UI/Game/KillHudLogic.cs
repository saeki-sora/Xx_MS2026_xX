using System;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 撃破数の表示の決まり（コンボ・お祝い）。Unity を使わないのでテストできる（<see cref="FortressUiValues"/> が使う）。
    /// ・コンボ: 自分の撃破が windowSeconds 以内に続いた数。途切れたら 0
    /// ・お祝い: 全員の合計が区切りの数（100, 250, 500, 1000, …）を超えたら、その数を showSeconds 秒だけ出す
    /// 新しい試合（数が減った）では、どちらも何も出さずに数え直す。最初に見た数も「前から」として扱う（途中から入った人に急にお祝いが出ないように）。
    /// </summary>
    public sealed class KillHudLogic
    {
        private int _lastOwn = -1;
        private int _lastTotal = -1;
        private float _lastKillTime;
        private float _milestoneUntil = float.MinValue;

        public float comboWindowSeconds = 2.5f;
        public float milestoneShowSeconds = 2.2f;
        public int[] milestones = { 100, 250, 500, 1000 };

        /// <summary>最後の区切りより後は、この数ごとにお祝いする（0 なら最後の区切りまで）。</summary>
        public int milestoneStepAfterLast = 1000;

        /// <summary>今のコンボの数（0 = コンボ中でない）。</summary>
        public int Combo { get; private set; }

        /// <summary>コンボが途切れるまでの残り（1 = 倒した直後、0 = 途切れた）。</summary>
        public float ComboRemaining01 { get; private set; }

        /// <summary>最後に届いた区切りの数（まだなら 0）。</summary>
        public int Milestone { get; private set; }

        /// <summary>お祝いを出している間 true。</summary>
        public bool MilestoneShowing { get; private set; }

        public void Update(int ownKills, int totalKills, float now)
        {
            UpdateCombo(ownKills, now);
            UpdateMilestone(totalKills, now);
        }

        private void UpdateCombo(int ownKills, float now)
        {
            if (_lastOwn < 0 || ownKills < _lastOwn)
            {
                _lastOwn = ownKills;
                Combo = 0;
                ComboRemaining01 = 0f;
                return;
            }

            var delta = ownKills - _lastOwn;
            _lastOwn = ownKills;
            if (delta > 0)
            {
                if (Combo == 0 || now - _lastKillTime > comboWindowSeconds)
                {
                    Combo = 0;
                }

                Combo += delta;
                _lastKillTime = now;
            }
            else if (Combo > 0 && now - _lastKillTime > comboWindowSeconds)
            {
                Combo = 0;
            }

            ComboRemaining01 = Combo > 0 && comboWindowSeconds > 0f ? Clamp01(1f - (now - _lastKillTime) / comboWindowSeconds) : 0f;
        }

        private void UpdateMilestone(int totalKills, float now)
        {
            if (_lastTotal < 0 || totalKills < _lastTotal)
            {
                _lastTotal = totalKills;
                Milestone = LastReached(totalKills);
                _milestoneUntil = float.MinValue;
                MilestoneShowing = false;
                return;
            }

            _lastTotal = totalKills;
            var reached = LastReached(totalKills);
            if (reached > Milestone)
            {
                Milestone = reached;
                _milestoneUntil = now + milestoneShowSeconds;
            }

            MilestoneShowing = now < _milestoneUntil;
        }

        /// <summary>この数までに届いた一番大きい区切り（無ければ 0）。</summary>
        public int LastReached(int total)
        {
            var reached = 0;
            var last = 0;
            if (milestones != null)
            {
                foreach (var m in milestones)
                {
                    if (m > 0 && total >= m)
                    {
                        reached = Math.Max(reached, m);
                    }

                    last = Math.Max(last, m);
                }
            }

            if (milestoneStepAfterLast > 0 && total >= last + milestoneStepAfterLast)
            {
                reached = Math.Max(reached, last + (total - last) / milestoneStepAfterLast * milestoneStepAfterLast);
            }

            return reached;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
