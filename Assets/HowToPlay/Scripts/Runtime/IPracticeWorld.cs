using MS2026.Fortress;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習のシーン（本物の砲台・敵・スマッシュボール・スキル）に置いた「自分」の様子。
    /// 練習の係（<see cref="PracticeTrainer"/>）はこれがあれば、自分で熱を計算せず、本物の砲台の様子で課題を進める。
    /// </summary>
    public interface IPracticeWorld
    {
        /// <summary>自分の砲台が見つかっていて、使える。</summary>
        bool IsReady { get; }

        TurretState LocalTurretState { get; }

        /// <summary>自分の砲台の熱（0〜1）。</summary>
        float LocalHeat01 { get; }

        /// <summary>自分のキャラにスキルがある。</summary>
        bool LocalHasSkill { get; }

        bool LocalSkillReady { get; }

        bool LocalSkillActive { get; }

        /// <summary>
        /// 自分のスキルが発動した回数（練習が始まってから。1フレームで終わるスキル（黄の矢）も数える）。
        /// 研修3は「使ったあと、使える状態にも使っている途中にも無い」で終わる（青のボトル3本は、3本とも投げるまで続く）。
        /// </summary>
        int LocalSkillUses { get; }

        /// <summary>練習が始まってから、自分がスマッシュボールを割ったことがある。</summary>
        bool LocalBrokeBall { get; }

        /// <summary>練習が始まってから、自分が倒した敵の数。</summary>
        int LocalKills { get; }
    }
}
