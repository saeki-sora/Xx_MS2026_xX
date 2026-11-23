using System;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習（新人研修）の課題。上から順に進む（通信では 1 バイトで送るので、Done がいちばん大きい値のままにすること）。
    /// 研修1 = Grip・Hold・Release ／ 研修2 = Overheat・Cool ／ 研修3 = Bubble・Skill。
    /// </summary>
    public enum PracticeStep : byte
    {
        /// <summary>研修1「握る」の途中。</summary>
        Grip = 0,

        /// <summary>研修1「握り続けて敵を倒す」の途中（練習のシーンでは敵を決めた数倒す。絵のフィールドでは決めた秒数撃つ）。</summary>
        Hold = 1,

        /// <summary>研修1「離す」の途中。</summary>
        Release = 2,

        /// <summary>研修2「握り続けてオーバーヒート」の途中。</summary>
        Overheat = 3,

        /// <summary>研修2「離して冷ます」の途中。</summary>
        Cool = 4,

        /// <summary>研修3「泡を割る」の途中。</summary>
        Bubble = 5,

        /// <summary>研修3「もう一度握ってスキル」の途中（泡は割れて、スキルがたまっている）。</summary>
        Skill = 6,

        /// <summary>全部できた。</summary>
        Done = 7
    }

    /// <summary>課題と「研修の何番目か」の対応。</summary>
    public static class PracticeSteps
    {
        public const int LessonCount = 3;

        /// <summary>その課題が入っている研修（0〜2。全部できたら 3）。</summary>
        public static int Lesson(PracticeStep step)
        {
            switch (step)
            {
                case PracticeStep.Grip:
                case PracticeStep.Hold:
                case PracticeStep.Release:
                    return 0;
                case PracticeStep.Overheat:
                case PracticeStep.Cool:
                    return 1;
                case PracticeStep.Bubble:
                case PracticeStep.Skill:
                    return 2;
                default:
                    return LessonCount;
            }
        }

        /// <summary>研修の最初の課題。</summary>
        public static PracticeStep FirstOf(int lesson)
        {
            switch (lesson)
            {
                case 0: return PracticeStep.Grip;
                case 1: return PracticeStep.Overheat;
                case 2: return PracticeStep.Bubble;
                default: return PracticeStep.Done;
            }
        }

        /// <summary>研修の中の課題の数。</summary>
        public static int TaskCount(int lesson) => lesson == 0 ? 3 : lesson == 1 ? 2 : lesson == 2 ? 2 : 0;

        /// <summary>終わった研修の数（0〜3）。</summary>
        public static int LessonsDone(PracticeStep step) => Lesson(step);
    }

    /// <summary><see cref="PracticeTrainer.Tick"/> で起きたこと。</summary>
    [Flags]
    public enum PracticeEvents
    {
        None = 0,

        /// <summary>課題が1つ進んだ。</summary>
        StepAdvanced = 1,

        /// <summary>オーバーヒートした。</summary>
        Overheated = 2,

        /// <summary>泡が割れた（スキルがたまった）。</summary>
        BubbleBroken = 4,

        /// <summary>スキルを出した。</summary>
        SkillFired = 8
    }

    /// <summary>
    /// 自分の PC の練習を動かす係（握力 → 熱・オーバーヒート → 砲台の向き・泡・スキル → 課題の進み）。Unity にも通信にも頼らないのでテストで確かめられる。
    /// 熱は本番の砲台（LaserTurret）と同じく「握っている間 握力² でたまり、離すと下がる。いっぱいになると数秒撃てない」。
    /// 砲台は本番と同じく勝手に回り、研修3の泡は「砲台の向きの少し先」に出る。ビームの向きが泡に重なっている間だけ削れる。
    /// 画面（<see cref="PracticeScreen"/>）は自分の席の砲台の向き・泡・スキルを <see cref="Local"/> から読む。
    /// </summary>
    public sealed class PracticeTrainer : IPracticeLocalSource
    {
        private float _silence;
        private float _hold;
        private bool _wasGripping;
        private bool _needRelease;

        // 研修2「オーバーヒート」は、研修2に入ってから新しくオーバーヒートしたときだけ進める（研修1の間のオーバーヒートで、研修2まで一気に進まないように）。
        private bool _overheatArmed;

        /// <summary>いちばん最近 <see cref="Reset"/> された係（＝このPCの練習）。画面が自分の砲台を描くのに使う。</summary>
        public static PracticeTrainer Local { get; private set; }

        /// <summary>
        /// 練習のシーン（本物の砲台・敵・スキル）が読み込まれていれば、その「自分」の様子（2026-11-13）。
        /// あれば熱やオーバーヒートは本物の砲台の値を使い、泡とスキルも本物の出来事で進める。無ければ今までどおり自分で計算する。
        /// </summary>
        public static IPracticeWorld World { get; set; }

        public PracticeStep Step { get; private set; }

        /// <summary>握力（0〜1）。</summary>
        public float Grip01 { get; private set; }

        /// <summary>握っているか（センサーの「握った」判定）。</summary>
        public bool Gripping { get; private set; }

        /// <summary>熱（0〜1。1 でオーバーヒート）。</summary>
        public float Heat01 { get; private set; }

        /// <summary>研修3でスキルを使い切り、修了までの数秒（<see cref="PracticeRules.afterSkillSeconds"/>）を待っている間。</summary>
        public bool SkillUsedUp => _afterSkill >= 0f && Step == PracticeStep.Skill;

        // スキルを使い切ってからの秒数（-1 = まだ）。
        private float _afterSkill = -1f;

        /// <summary>スキルを使い切ったあと、決めた秒数が経ったら修了にする。</summary>
        private void WaitAfterSkill(float dt, PracticeRules rules)
        {
            _afterSkill = _afterSkill < 0f ? 0f : _afterSkill + dt;
            if (_afterSkill >= rules.afterSkillSeconds)
            {
                Step = PracticeStep.Done;
            }
        }

        /// <summary>オーバーヒートで撃てない間。</summary>
        public bool Overheated { get; private set; }

        /// <summary>砲台の向き（度。0 = 画面の上、時計回りに増える）。</summary>
        public float TurretDegrees { get; private set; }

        /// <summary>泡が出ているか（研修3）。</summary>
        public bool BubbleVisible { get; private set; }

        /// <summary>泡の向き（度。砲台から見て）。</summary>
        public float BubbleDegrees { get; private set; }

        /// <summary>泡の残り（1 → 0 で割れる）。</summary>
        public float BubbleHp01 { get; private set; }

        /// <summary>スキルがたまっている（泡を割った後、まだ出していない）。</summary>
        public bool SkillReady { get; private set; }

        /// <summary>スキルが出ている残り秒（0 なら出ていない）。</summary>
        public float SkillRemaining { get; private set; }

        /// <summary>「握り続ける」の進み（0〜1）。練習のシーンでは倒した数の割合。</summary>
        public float HoldProgress01 { get; private set; }

        /// <summary>練習のシーンで、研修1の間に倒した数（決めた数で止まる）。</summary>
        public int KillsInLesson1 { get; private set; }

        public bool IsDone => Step == PracticeStep.Done;

        public bool SkillActive => SkillRemaining > 0f;

        /// <summary>ビームが出ているか。</summary>
        public bool Firing => Gripping && !Overheated;

        public void Reset()
        {
            Step = PracticeStep.Grip;
            Grip01 = 0f;
            Gripping = false;
            Heat01 = 0f;
            Overheated = false;
            TurretDegrees = 0f;
            BubbleVisible = false;
            BubbleDegrees = 0f;
            BubbleHp01 = 1f;
            SkillReady = false;
            SkillRemaining = 0f;
            HoldProgress01 = 0f;
            _silence = 0f;
            _hold = 0f;
            _wasGripping = false;
            _needRelease = false;
            _skillUsesAtStart = int.MaxValue;
            _afterSkill = -1f;
            _overheatArmed = false;
            Local = this;
        }

        /// <summary>全部できたことにする（時間切れのとき）。熱やビームはそのまま動く。</summary>
        public void Complete()
        {
            Step = PracticeStep.Done;
            BubbleVisible = false;
        }

        /// <summary>1フレーム進める。gripping = センサーの「握った」判定、grip = 握力（0〜1）。</summary>
        public PracticeEvents Tick(bool gripping, float grip, float dt, PracticeRules rules)
        {
            rules ??= new PracticeRules();
            // 本物の砲台で判定するのは「このPCの自分」の係だけ（エディタで試すときの自動の2P〜4Pは、今までどおり自分で計算する）。
            var world = ReferenceEquals(this, Local) ? World : null;
            if (world != null && world.IsReady)
            {
                return TickWorld(world, grip, dt, rules);
            }

            dt = Mathf.Max(0f, dt);
            Grip01 = Mathf.Clamp01(grip);
            Gripping = gripping && Grip01 > 0f;
            var pressed = Gripping && !_wasGripping;
            _wasGripping = Gripping;

            var events = PracticeEvents.None;

            // スキルが出ている間は熱がたまらない（本番のスキルと同じく、ふつうのビームとは別物）。
            if (SkillRemaining > 0f)
            {
                SkillRemaining = Mathf.Max(0f, SkillRemaining - dt);
            }

            if (Overheated)
            {
                _silence -= dt;
                Heat01 = Mathf.Max(0f, Heat01 - rules.heatCoolingPerSecond * dt);
                if (_silence <= 0f)
                {
                    Overheated = false;
                }
            }
            else if (Gripping && SkillRemaining <= 0f)
            {
                var gain = Grip01 * Grip01 * rules.heatGainPerSecond;
                if (Step == PracticeStep.Overheat)
                {
                    gain = Mathf.Max(gain, rules.overheatStepMinGainPerSecond);
                }

                if (rules.noOverheatInLesson1 && PracticeSteps.Lesson(Step) == 0)
                {
                    // 研修1ではオーバーヒートしない（熱がたまらない）。
                    gain = 0f;
                }

                Heat01 += gain * dt;
                if (Heat01 >= 1f)
                {
                    Heat01 = 1f;
                    Overheated = true;
                    _silence = rules.overheatSilenceSeconds;
                    events |= PracticeEvents.Overheated;
                }
            }
            else if (!Gripping)
            {
                Heat01 = Mathf.Max(0f, Heat01 - rules.heatCoolingPerSecond * dt);
            }

            // 砲台は勝手に回る（撃っている間は少しゆっくり）。
            var speed = rules.turretDegreesPerSecond * (Firing ? rules.firingTurnScale : 1f);
            TurretDegrees = Mathf.Repeat(TurretDegrees + speed * dt, 360f);

            var before = Step;
            switch (Step)
            {
                case PracticeStep.Grip:
                    if (Gripping && Grip01 >= rules.firstGripThreshold)
                    {
                        Step = PracticeStep.Hold;
                        _hold = 0f;
                    }

                    break;
                case PracticeStep.Hold:
                    if (Firing)
                    {
                        _hold += dt;
                    }

                    HoldProgress01 = Mathf.Clamp01(_hold / rules.holdSeconds);
                    if (_hold >= rules.holdSeconds)
                    {
                        Step = PracticeStep.Release;
                    }

                    break;
                case PracticeStep.Release:
                    if (!Gripping)
                    {
                        Step = PracticeStep.Overheat;
                        _overheatArmed = false;
                    }

                    break;
                case PracticeStep.Overheat:
                    if (!Overheated)
                    {
                        _overheatArmed = true;
                    }
                    else if (_overheatArmed)
                    {
                        Step = PracticeStep.Cool;
                    }

                    break;
                case PracticeStep.Cool:
                    if (!Gripping && !Overheated && Heat01 <= rules.cooledHeat01)
                    {
                        Step = PracticeStep.Bubble;
                        SpawnBubble(rules);
                    }

                    break;
                case PracticeStep.Bubble:
                    if (!BubbleVisible)
                    {
                        SpawnBubble(rules);
                    }

                    if (Firing && Mathf.Abs(Mathf.DeltaAngle(TurretDegrees, BubbleDegrees)) <= rules.bubbleHitDegrees)
                    {
                        BubbleHp01 = Mathf.Max(0f, BubbleHp01 - dt / rules.bubbleBreakSeconds);
                    }

                    if (BubbleHp01 <= 0f)
                    {
                        BubbleVisible = false;
                        SkillReady = true;
                        _needRelease = Gripping;
                        Step = PracticeStep.Skill;
                        events |= PracticeEvents.BubbleBroken;
                    }

                    break;
                case PracticeStep.Skill:
                    if (_needRelease && !Gripping)
                    {
                        _needRelease = false;
                    }

                    if (SkillReady && !_needRelease && (pressed || Gripping))
                    {
                        SkillReady = false;
                        SkillRemaining = rules.skillSeconds;
                        events |= PracticeEvents.SkillFired;
                    }
                    else if (!SkillReady && SkillRemaining <= 0f)
                    {
                        WaitAfterSkill(dt, rules);
                    }

                    break;
            }

            if (Step != before)
            {
                events |= PracticeEvents.StepAdvanced;
            }

            return events;
        }

        // 研修3（スキル）に来たときの、スキルを使った回数。これより増えて、スキルが使える状態にも使っている途中にも無ければ終わり。
        private int _skillUsesAtStart = int.MaxValue;

        /// <summary>練習のシーンの本物の砲台・スキルで進める。</summary>
        private PracticeEvents TickWorld(IPracticeWorld world, float grip, float dt, PracticeRules rules)
        {
            dt = Mathf.Max(0f, dt);
            var state = world.LocalTurretState;
            var wasOverheated = Overheated;
            var wasSkill = SkillRemaining > 0f;
            Grip01 = Mathf.Clamp01(grip);
            Gripping = state == TurretState.Charging || state == TurretState.Firing;
            Overheated = state == TurretState.Overheated;
            Heat01 = Mathf.Clamp01(world.LocalHeat01);
            SkillRemaining = world.LocalSkillActive ? 1f : 0f;
            SkillReady = world.LocalSkillReady;
            BubbleVisible = false;
            _wasGripping = Gripping;

            var events = PracticeEvents.None;
            if (Overheated && !wasOverheated)
            {
                events |= PracticeEvents.Overheated;
            }

            if (SkillRemaining > 0f && !wasSkill)
            {
                events |= PracticeEvents.SkillFired;
            }

            var before = Step;
            switch (Step)
            {
                case PracticeStep.Grip:
                    if (Gripping)
                    {
                        Step = PracticeStep.Hold;
                        _hold = 0f;
                    }

                    break;
                case PracticeStep.Hold:
                {
                    // 本物の敵を、決めた数倒すまで。
                    var need = Mathf.Max(1, rules.lesson1Kills);
                    KillsInLesson1 = Mathf.Min(world.LocalKills, need);
                    HoldProgress01 = KillsInLesson1 / (float)need;
                    if (world.LocalKills >= need)
                    {
                        Step = PracticeStep.Release;
                    }

                    break;
                }

                case PracticeStep.Release:
                    if (!Gripping)
                    {
                        Step = PracticeStep.Overheat;
                        _overheatArmed = false;
                    }

                    break;
                case PracticeStep.Overheat:
                    if (!Overheated)
                    {
                        _overheatArmed = true;
                    }
                    else if (_overheatArmed)
                    {
                        Step = PracticeStep.Cool;
                    }

                    break;
                case PracticeStep.Cool:
                    if (!Gripping && !Overheated && Heat01 <= rules.cooledHeat01)
                    {
                        Step = PracticeStep.Bubble;
                    }

                    break;
                case PracticeStep.Bubble:
                    if (!world.LocalHasSkill)
                    {
                        // スキルの無いキャラ（設定前など）は、泡を割ったら終わり。
                        if (world.LocalBrokeBall)
                        {
                            Step = PracticeStep.Done;
                        }
                    }
                    else if (world.LocalBrokeBall || world.LocalSkillReady || world.LocalSkillActive)
                    {
                        Step = PracticeStep.Skill;
                        _skillUsesAtStart = world.LocalSkillUses - (world.LocalSkillActive ? 1 : 0);
                        events |= PracticeEvents.BubbleBroken;
                    }

                    break;
                case PracticeStep.Skill:
                    if (!world.LocalHasSkill)
                    {
                        Step = PracticeStep.Done;
                    }
                    else if (_afterSkill >= 0f || (world.LocalSkillUses > _skillUsesAtStart && !world.LocalSkillActive && !world.LocalSkillReady))
                    {
                        // スキルを最後まで使い切った（青はボトル3本とも投げた・赤や緑は効果が終わった）。決めた秒数のあとで修了。
                        WaitAfterSkill(dt, rules);
                    }

                    break;
            }

            if (Step != before)
            {
                events |= PracticeEvents.StepAdvanced;
            }

            return events;
        }

        private void SpawnBubble(PracticeRules rules)
        {
            BubbleVisible = true;
            BubbleHp01 = 1f;
            BubbleDegrees = Mathf.Repeat(TurretDegrees + rules.bubbleAheadDegrees, 360f);
        }
    }
}
