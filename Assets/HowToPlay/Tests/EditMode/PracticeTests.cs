using NUnit.Framework;

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

        // ───────── 練習を動かす係 ─────────

        [Test]
        public void 弱すぎる握りでは_握ってみるが進まない()
        {
            var rules = new PracticeRules();
            var trainer = new PracticeTrainer();
            trainer.Reset();
            Run(trainer, true, rules.firstGripThreshold * 0.5f, 1f, rules);
            Assert.AreEqual(PracticeStep.Grip, trainer.Step);

            var events = trainer.Tick(true, rules.firstGripThreshold, Dt, rules);
            Assert.AreEqual(PracticeStep.Squeeze, trainer.Step);
            Assert.IsTrue((events & PracticeEvents.StepAdvanced) != 0);
        }

        [Test]
        public void 思いきり握ると熱がたまり_オーバーヒートで撃てなくなる()
        {
            var rules = new PracticeRules();
            var trainer = new PracticeTrainer();
            trainer.Reset();
            Run(trainer, true, 1f, 0.5f / rules.heatGainPerSecond + 0.05f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step, "熱が半分たまったら「思いきり握る」はできた");
            Assert.IsTrue(trainer.Firing);

            var events = PracticeEvents.None;
            for (var i = 0; i < 600 && (events & PracticeEvents.Overheated) == 0; i++)
            {
                events |= trainer.Tick(true, 1f, Dt, rules);
            }

            Assert.IsTrue((events & PracticeEvents.Overheated) != 0);
            Assert.IsTrue(trainer.Overheated);
            Assert.IsFalse(trainer.Firing, "オーバーヒート中はビームが出ない");
            Assert.AreEqual(PracticeStep.Release, trainer.Step, "握ったままでは「離して冷ます」はできない");

            Run(trainer, true, 1f, rules.overheatSilenceSeconds + 0.05f, rules);
            Assert.IsFalse(trainer.Overheated, "決まった秒数で撃てるようになる");
        }

        [Test]
        public void 弱い握りでも握り続ければ_思いきり握るに届く()
        {
            var rules = new PracticeRules();
            var trainer = new PracticeTrainer();
            trainer.Reset();
            Run(trainer, true, 0.5f, 0.5f / (0.25f * rules.heatGainPerSecond) + 0.1f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step);
        }

        [Test]
        public void 離して熱が下がると_全部できる()
        {
            var rules = new PracticeRules();
            var trainer = new PracticeTrainer();
            trainer.Reset();
            Run(trainer, true, 1f, 0.8f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step);

            Run(trainer, false, 0f, 0.1f, rules);
            Assert.AreEqual(PracticeStep.Release, trainer.Step, "すぐには冷えない");

            Run(trainer, false, 0f, 2f, rules);
            Assert.AreEqual(PracticeStep.Done, trainer.Step);
            Assert.AreEqual(0f, trainer.Heat01, 1e-4f);
        }

        [Test]
        public void 時間切れなら全部できたことにする()
        {
            var trainer = new PracticeTrainer();
            trainer.Reset();
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
            board.SetSeat(0, 0.9f, 0.4f, true, false, PracticeStep.Squeeze);
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
    }
}
