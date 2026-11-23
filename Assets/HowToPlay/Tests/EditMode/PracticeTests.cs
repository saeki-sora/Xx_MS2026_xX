using MS2026.Fortress;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.HowToPlay.Tests
{
    public sealed class PracticeTests
    {
        private const float Dt = 1f / 60f;

        private static PracticeEvents Run(PracticeTrainer trainer, bool gripping, float grip, float seconds, PracticeRules rules)
        {
            var events = PracticeEvents.None;
            for (var t = 0f; t < seconds; t += Dt)
            {
                events |= trainer.Tick(gripping, grip, Dt, rules);
            }

            return events;
        }

        // ───────── 練習を動かす係（新人研修 2026-11-12） ─────────

        private static PracticeTrainer NewTrainer()
        {
            var trainer = new PracticeTrainer();
            trainer.Reset();
            return trainer;
        }

        /// <summary>研修1・2 を終えて「泡を割る」まで進める。</summary>
        private static PracticeTrainer ToBubble(PracticeRules rules)
        {
            var trainer = NewTrainer();
            Run(trainer, true, 0.6f, rules.holdSeconds + 0.1f, rules);
            Run(trainer, false, 0f, 2.5f, rules);
            for (var i = 0; i < 1200 && trainer.Step == PracticeStep.Overheat; i++)
            {
                trainer.Tick(true, 0.3f, Dt, rules);
            }

            Run(trainer, false, 0f, 4f, rules);
            Assert.AreEqual(PracticeStep.Bubble, trainer.Step);
            return trainer;
        }

        [Test]
        public void 弱すぎる握りでは_握るが進まない()
        {
            var rules = new PracticeRules();
            var trainer = NewTrainer();
            Run(trainer, true, rules.firstGripThreshold * 0.5f, 1f, rules);
            Assert.AreEqual(PracticeStep.Grip, trainer.Step);

            var events = trainer.Tick(true, rules.firstGripThreshold, Dt, rules);
            Assert.AreEqual(PracticeStep.Hold, trainer.Step);
            Assert.IsTrue((events & PracticeEvents.StepAdvanced) != 0);
        }

        [Test]
        public void 握り続けて_離すと_研修1が終わる()
        {
            var rules = new PracticeRules();
            var trainer = NewTrainer();
            Run(trainer, true, 0.5f, rules.holdSeconds * 0.5f, rules);
            Assert.AreEqual(PracticeStep.Hold, trainer.Step, "まだ握り続けていない");
            Run(trainer, true, 0.5f, rules.holdSeconds * 0.6f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step);
            Run(trainer, true, 0.5f, 0.3f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step, "握ったままでは離したことにならない");
            trainer.Tick(false, 0f, Dt, rules);
            Assert.AreEqual(PracticeStep.Overheat, trainer.Step);
        }

        [Test]
        public void 研修2は_弱い握りでもオーバーヒートまで届き_離して冷ますと終わる()
        {
            var rules = new PracticeRules();
            var trainer = NewTrainer();
            Run(trainer, true, 0.6f, rules.holdSeconds + 0.1f, rules);
            Run(trainer, false, 0f, 2.5f, rules);
            Assert.AreEqual(PracticeStep.Overheat, trainer.Step);

            var events = Run(trainer, true, 0.2f, 1f / rules.overheatStepMinGainPerSecond + 0.2f, rules);
            Assert.IsTrue((events & PracticeEvents.Overheated) != 0, "弱い握りでもオーバーヒートする");
            Assert.AreEqual(PracticeStep.Cool, trainer.Step);
            Assert.IsFalse(trainer.Firing, "オーバーヒート中はビームが出ない");

            Run(trainer, false, 0f, 0.1f, rules);
            Assert.AreEqual(PracticeStep.Cool, trainer.Step, "すぐには冷えない");
            Run(trainer, false, 0f, 3f, rules);
            Assert.AreEqual(PracticeStep.Bubble, trainer.Step);
            Assert.IsTrue(trainer.BubbleVisible, "研修3の泡が出る");
        }

        [Test]
        public void 泡はビームが重なっている間だけ削れ_割れるとスキルがたまる()
        {
            var rules = new PracticeRules();
            var trainer = ToBubble(rules);
            var events = PracticeEvents.None;
            var hp = trainer.BubbleHp01;
            for (var i = 0; i < 60 * 30 && trainer.Step == PracticeStep.Bubble; i++)
            {
                var aimed = Mathf.Abs(Mathf.DeltaAngle(trainer.TurretDegrees, trainer.BubbleDegrees)) <= rules.bubbleHitDegrees;
                if (!aimed)
                {
                    Assert.AreEqual(hp, trainer.BubbleHp01, 1e-4f, "向きが合っていなければ削れない");
                }

                events |= trainer.Tick(true, 0.5f, Dt, rules);
                hp = trainer.BubbleHp01;
                if (trainer.Overheated)
                {
                    Run(trainer, false, 0f, 3f, rules);
                }
            }

            Assert.AreEqual(PracticeStep.Skill, trainer.Step);
            Assert.IsTrue((events & PracticeEvents.BubbleBroken) != 0);
            Assert.IsTrue(trainer.SkillReady);
        }

        [Test]
        public void スキルは一度離してから握ると出て_出し終わると全部できる()
        {
            var rules = new PracticeRules();
            var trainer = ToBubble(rules);
            for (var i = 0; i < 60 * 30 && trainer.Step == PracticeStep.Bubble; i++)
            {
                trainer.Tick(true, 0.5f, Dt, rules);
                if (trainer.Overheated)
                {
                    Run(trainer, false, 0f, 3f, rules);
                }
            }

            Assert.AreEqual(PracticeStep.Skill, trainer.Step);
            var events = Run(trainer, true, 0.5f, 0.5f, rules);
            Assert.IsTrue((events & PracticeEvents.SkillFired) == 0, "割ったときの握りのままでは出ない");
            Run(trainer, false, 0f, 0.1f, rules);
            events = trainer.Tick(true, 0.5f, Dt, rules);
            Assert.IsTrue((events & PracticeEvents.SkillFired) != 0);
            Assert.IsTrue(trainer.SkillActive);
            Run(trainer, false, 0f, rules.skillSeconds + 0.1f, rules);
            Assert.AreEqual(PracticeStep.Skill, trainer.Step, "使い切った直後はまだ（スキルを見届ける）");
            Assert.IsTrue(trainer.SkillUsedUp);
            Run(trainer, false, 0f, rules.afterSkillSeconds - 0.5f, rules);
            Assert.AreEqual(PracticeStep.Skill, trainer.Step, "決めた秒数より前は、まだ");
            Run(trainer, false, 0f, 0.6f, rules);
            Assert.AreEqual(PracticeStep.Done, trainer.Step, "使い切って決めた秒数が経つと修了");
        }

        [Test]
        public void 研修1ではオーバーヒートしない()
        {
            var rules = new PracticeRules();
            Assert.IsTrue(rules.noOverheatInLesson1, "最初から ON");
            var trainer = NewTrainer();
            // 研修1のあいだ、思いきり握りっぱなしにしても熱はたまらない。
            for (var i = 0; i < 60 * 10 && PracticeSteps.Lesson(trainer.Step) == 0; i++)
            {
                trainer.Tick(true, 1f, Dt, rules);
                Assert.IsFalse(trainer.Overheated, "研修1でオーバーヒートしてはいけない");
                Assert.AreEqual(0f, trainer.Heat01, 0.0001f);
            }
        }

        [Test]
        public void 研修1の間にオーバーヒートしても_研修2は一気に進まない()
        {
            // 「研修1ではオーバーヒートしない」を切ったとき（研修1で熱がたまる設定）。
            var rules = new PracticeRules { noOverheatInLesson1 = false };
            var trainer = NewTrainer();
            // 研修1: 思いきり握りっぱなしでオーバーヒートまで行く。
            var events = PracticeEvents.None;
            for (var i = 0; i < 60 * 10 && (events & PracticeEvents.Overheated) == 0; i++)
            {
                events |= trainer.Tick(true, 1f, Dt, rules);
            }

            Assert.IsTrue(trainer.Overheated);
            Assert.AreEqual(PracticeStep.Release, trainer.Step);

            // オーバーヒートのまま離す → 研修2に入るが、そのオーバーヒートでは進まない。
            Run(trainer, false, 0f, 0.2f, rules);
            Assert.AreEqual(PracticeStep.Overheat, trainer.Step, "研修1のオーバーヒートで研修2が終わってはいけない");
            Run(trainer, false, 0f, 3f, rules);
            Assert.AreEqual(PracticeStep.Overheat, trainer.Step);

            // 研修2に入ってから、もう一度オーバーヒートすれば進む。
            for (var i = 0; i < 60 * 10 && !trainer.Overheated; i++)
            {
                trainer.Tick(true, 1f, Dt, rules);
            }

            trainer.Tick(true, 1f, Dt, rules);
            Assert.AreEqual(PracticeStep.Cool, trainer.Step);
        }

        [Test]
        public void 研修の番号()
        {
            Assert.AreEqual(0, PracticeSteps.Lesson(PracticeStep.Release));
            Assert.AreEqual(1, PracticeSteps.Lesson(PracticeStep.Cool));
            Assert.AreEqual(2, PracticeSteps.Lesson(PracticeStep.Skill));
            Assert.AreEqual(3, PracticeSteps.Lesson(PracticeStep.Done));
            Assert.AreEqual(PracticeStep.Bubble, PracticeSteps.FirstOf(2));
        }

        [Test]
        public void 時間切れなら全部できたことにする()
        {
            var trainer = NewTrainer();
            trainer.Complete();
            Assert.IsTrue(trainer.IsDone);
            trainer.Tick(true, 1f, Dt, new PracticeRules());
            Assert.IsTrue(trainer.IsDone, "その後も戻らない");
            Assert.IsTrue(trainer.Firing, "ビームは出る");
        }

        // ───────── 全員分の様子 ─────────

        [Test]
        public void 課題の進みは戻らない()
        {
            var board = new PracticeBoard();
            board.Reset(20f);
            board.SetPresent(1, true);
            board.SetSeat(1, 0.5f, 0.2f, true, false, PracticeStep.Release);
            board.SetSeat(1, 0.5f, 0.2f, true, false, PracticeStep.Grip);
            Assert.AreEqual(PracticeStep.Release, board.seats[1].step);
        }

        [Test]
        public void いる全員ができたら_AllDone()
        {
            var board = new PracticeBoard();
            board.Reset(20f);
            Assert.IsFalse(board.AllDone, "だれもいなければできていない");
            board.SetPresent(0, true);
            board.SetPresent(2, true);
            board.SetSeat(0, 0f, 0f, false, false, PracticeStep.Done);
            Assert.IsFalse(board.AllDone);
            board.SetSeat(2, 0f, 0f, false, false, PracticeStep.Done);
            Assert.IsTrue(board.AllDone, "いない席（2P・4P）は数えない");
        }

        [Test]
        public void 終えると_いる人はみんなできたことになる()
        {
            var board = new PracticeBoard();
            board.Reset(20f);
            board.SetPresent(0, true);
            board.SetPresent(3, true);
            board.Finish();
            Assert.IsTrue(board.finished);
            Assert.AreEqual(PracticeStep.Done, board.seats[0].step);
            Assert.AreEqual(PracticeStep.Done, board.seats[3].step);
            Assert.AreEqual(PracticeStep.Grip, board.seats[1].step, "いない席はそのまま");

            board.SetSeat(0, 0f, 0f, false, false, PracticeStep.Grip);
            Assert.AreEqual(PracticeStep.Done, board.seats[0].step);
        }

        [Test]
        public void 残り時間は1秒の位が変わったときだけ変わったとする()
        {
            var board = new PracticeBoard();
            board.Reset(2f);
            var version = board.Version;
            board.Tick(0.5f);
            Assert.AreEqual(version, board.Version);
            board.Tick(0.6f);
            Assert.AreNotEqual(version, board.Version);
            board.Tick(5f);
            Assert.IsTrue(board.TimeUp);
            Assert.AreEqual(0f, board.remaining);
        }

        [Test]
        public void 時間制限0なら_時間切れにならない()
        {
            var board = new PracticeBoard();
            board.Reset(0f);
            board.Tick(999f);
            Assert.IsFalse(board.HasTimeLimit);
            Assert.IsFalse(board.TimeUp, "全員できるまで終わらない");
        }

        [Test]
        public void 細かい揺れでは変わったとしない()
        {
            var board = new PracticeBoard();
            board.Reset(20f);
            board.SetSeat(0, 0.5f, 0.5f, true, false, PracticeStep.Grip);
            var version = board.Version;
            board.SetSeat(0, 0.5f + 0.0005f, 0.5f, true, false, PracticeStep.Grip);
            Assert.AreEqual(version, board.Version);
            board.SetSeat(0, 0.6f, 0.5f, true, false, PracticeStep.Grip);
            Assert.AreNotEqual(version, board.Version);
        }

        [Test]
        public void 届いた中身では_自分の席は課題だけ受け取る()
        {
            var board = new PracticeBoard();
            board.Reset(20f);
            board.SetPresent(0, true);
            board.SetPresent(1, true);
            board.SetSeat(0, 0.9f, 0.4f, true, false, PracticeStep.Hold);
            var incoming = new PracticeBoard.Seat[PracticeBoard.SeatCount];
            incoming[0] = new PracticeBoard.Seat { grip = 10, heat = 10, step = PracticeStep.Done };
            incoming[1] = new PracticeBoard.Seat { grip = 200, heat = 100, gripping = true, step = PracticeStep.Release };
            board.Apply(12.5f, true, incoming, 0);

            Assert.AreEqual(PracticeBoard.ToByte(0.9f), board.seats[0].grip, "自分の握力はそのまま");
            Assert.AreEqual(PracticeStep.Done, board.seats[0].step, "課題の進みは受け取る");
            Assert.AreEqual(200, board.seats[1].grip);
            Assert.IsTrue(board.seats[1].present, "いるかどうかは部屋の一覧のまま");
            Assert.AreEqual(12.5f, board.remaining);
            Assert.IsTrue(board.finished);
        }
    
        // ───────── 本物のシーン（スキルは最後まで使うと修了・2026-11-19） ─────────

        private sealed class FakeWorld : IPracticeWorld
        {
            public bool IsReady => true;
            public TurretState LocalTurretState { get; set; } = TurretState.Idle;
            public float LocalHeat01 { get; set; }
            public bool LocalHasSkill { get; set; } = true;
            public bool LocalSkillReady { get; set; }
            public bool LocalSkillActive { get; set; }
            public int LocalSkillUses { get; set; }
            public bool LocalBrokeBall { get; set; }
            public int LocalKills { get; set; }
        }

        private static void Frames(PracticeTrainer trainer, int frames, PracticeRules rules)
        {
            for (var i = 0; i < frames; i++)
            {
                trainer.Tick(false, 0f, Dt, rules);
            }
        }

        /// <summary>本物のシーンの研修3（スキル）まで進める。</summary>
        private static PracticeTrainer WorldToSkill(FakeWorld world, PracticeRules rules)
        {
            var trainer = NewTrainer();
            PracticeTrainer.World = world;
            world.LocalTurretState = TurretState.Firing;
            Frames(trainer, 2, rules);
            world.LocalKills = rules.lesson1Kills;
            Frames(trainer, 2, rules);
            world.LocalTurretState = TurretState.Idle;
            Frames(trainer, 2, rules);
            world.LocalTurretState = TurretState.Overheated;
            Frames(trainer, 2, rules);
            world.LocalTurretState = TurretState.Idle;
            world.LocalHeat01 = 0f;
            Frames(trainer, 2, rules);
            world.LocalBrokeBall = true;
            world.LocalSkillReady = true;
            Frames(trainer, 2, rules);
            Assert.AreEqual(PracticeStep.Skill, trainer.Step);
            return trainer;
        }

        [Test]
        public void 青のボトル3本は_3本とも投げるまで研修3が終わらない()
        {
            var rules = new PracticeRules { afterSkillSeconds = 0f };
            var world = new FakeWorld();
            try
            {
                var trainer = WorldToSkill(world, rules);
                for (var bottle = 1; bottle <= 3; bottle++)
                {
                    // 握って構える（発動中）→ 離して投げる → 残りがあればすぐ「使える」に戻る。
                    world.LocalSkillReady = false;
                    world.LocalSkillActive = true;
                    world.LocalSkillUses++;
                    Frames(trainer, 3, rules);
                    world.LocalSkillActive = false;
                    world.LocalSkillReady = bottle < 3;
                    Frames(trainer, 3, rules);
                    if (bottle < 3)
                    {
                        Assert.AreEqual(PracticeStep.Skill, trainer.Step, $"{bottle}本目のあと、まだ終わらない");
                    }
                }

                Assert.AreEqual(PracticeStep.Done, trainer.Step, "3本とも投げたら修了");
            }
            finally
            {
                PracticeTrainer.World = null;
            }
        }

        [Test]
        public void 一瞬で終わるスキル_黄の矢_でも研修3は終わる()
        {
            var rules = new PracticeRules { afterSkillSeconds = 0f };
            var world = new FakeWorld();
            try
            {
                var trainer = WorldToSkill(world, rules);
                // 発動中のフレームを一度も見ないまま、使った回数だけ増えて、使える状態でもなくなる。
                world.LocalSkillReady = false;
                world.LocalSkillUses++;
                Frames(trainer, 2, rules);
                Assert.AreEqual(PracticeStep.Done, trainer.Step);
            }
            finally
            {
                PracticeTrainer.World = null;
            }
        }

        [Test]
        public void 赤や緑は_スキルの効果が続いている間は終わらない()
        {
            var rules = new PracticeRules { afterSkillSeconds = 0f };
            var world = new FakeWorld();
            try
            {
                var trainer = WorldToSkill(world, rules);
                world.LocalSkillReady = false;
                world.LocalSkillActive = true;
                world.LocalSkillUses++;
                Frames(trainer, 60, rules);
                Assert.AreEqual(PracticeStep.Skill, trainer.Step, "発動中はまだ");
                world.LocalSkillActive = false;
                Frames(trainer, 2, rules);
                Assert.AreEqual(PracticeStep.Done, trainer.Step);
            }
            finally
            {
                PracticeTrainer.World = null;
            }
        }
    }
}
