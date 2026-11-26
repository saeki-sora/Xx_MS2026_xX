namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の画面が「自分」の細かい様子を読む先。
    /// ロビーの練習では <see cref="PracticeTrainer"/>、ゲームの中の研修では <see cref="PracticeTutorialRunner"/>（本物の砲台・スキル）。
    /// </summary>
    public interface IPracticeLocalSource
    {
        /// <summary>「握り続ける」の進み（0〜1）。</summary>
        float HoldProgress01 { get; }

        /// <summary>スキルが出ている最中か。</summary>
        bool SkillActive { get; }

        /// <summary>オーバーヒートで撃てない間か。</summary>
        bool Overheated { get; }

        /// <summary>砲台の熱（0〜1。1 でオーバーヒート）。研修2のゲージに出す。</summary>
        float Heat01 { get; }

        /// <summary>研修3でスキルを使い切り、修了までの数秒を待っている間か（2026-11-19）。</summary>
        bool SkillUsedUp { get; }
    }
}
