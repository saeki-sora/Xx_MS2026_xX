using MS2026.CharacterSelect;
using NUnit.Framework;

namespace MS2026.CharacterSelect.Tests.EditMode
{
    public sealed class CharacterSelectTests
    {
        private static CharacterSelectBoard NewBoard(int characters, bool allowDuplicates, int players)
        {
            var board = new CharacterSelectBoard();
            board.Reset(characters, allowDuplicates, 0f);
            for (var seat = 0; seat < players; seat++)
            {
                board.SetActive(seat, true);
            }

            return board;
        }

        [Test]
        public void Board_StartsEachPlayerOnTheirOwnCharacter()
        {
            var board = NewBoard(4, false, 4);
            for (var seat = 0; seat < 4; seat++)
            {
                Assert.AreEqual(seat, board.seats[seat].cursor);
            }
        }

        [Test]
        public void Board_MoveNextWrapsAround()
        {
            var board = NewBoard(3, false, 1);
            Assert.IsTrue(board.MoveNext(0));
            Assert.IsTrue(board.MoveNext(0));
            Assert.AreEqual(2, board.seats[0].cursor);
            Assert.IsTrue(board.MoveNext(0));
            Assert.AreEqual(0, board.seats[0].cursor, "最後の次は最初に戻る");
        }

        [Test]
        public void Board_ExclusiveModeLocksConfirmedCharacter()
        {
            var board = NewBoard(4, false, 2);
            Assert.IsTrue(board.Confirm(0), "P1 がキャラ0で決定");
            board.seats[1].cursor = 3;
            Assert.IsTrue(board.MoveNext(1));
            Assert.AreEqual(1, board.seats[1].cursor, "3 の次の 0 は使われているので飛ばして 1");
            Assert.IsFalse(board.IsLockedFor(0, 0), "自分が決めたキャラは自分には使用中ではない");
            Assert.IsTrue(board.IsLockedFor(0, 1));
        }

        [Test]
        public void Board_ExclusiveModeMovesOthersOffTheConfirmedCharacter()
        {
            var board = NewBoard(4, false, 2);
            board.seats[1].cursor = 0;
            Assert.IsTrue(board.Confirm(0));
            Assert.AreEqual(1, board.seats[1].cursor, "同じキャラにいた P2 は次の空きへ");
        }

        [Test]
        public void Board_DuplicateModeAllowsSameCharacter()
        {
            var board = NewBoard(4, true, 2);
            board.seats[1].cursor = 0;
            Assert.IsTrue(board.Confirm(0));
            Assert.IsTrue(board.Confirm(1), "同じキャラ OK のときは2人とも決定できる");
            Assert.AreEqual(0, board.seats[1].cursor);
        }

        [Test]
        public void Board_TurningDuplicatesOffKeepsTheLowerSeat()
        {
            var board = NewBoard(4, true, 3);
            board.seats[1].cursor = 0;
            board.seats[2].cursor = 0;
            board.Confirm(0);
            board.Confirm(1);
            board.Confirm(2);
            board.SetAllowDuplicates(false);
            Assert.IsTrue(board.seats[0].confirmed);
            Assert.IsFalse(board.seats[1].confirmed);
            Assert.IsFalse(board.seats[2].confirmed);
        }

        [Test]
        public void Board_AllConfirmedOnlyCountsActiveSeats()
        {
            var board = NewBoard(4, false, 2);
            Assert.IsFalse(board.AllConfirmed);
            board.Confirm(0);
            Assert.IsFalse(board.AllConfirmed);
            board.Confirm(1);
            Assert.IsTrue(board.AllConfirmed, "参加していない P3・P4 は待たない");
            board.SetActive(2, true);
            Assert.IsFalse(board.AllConfirmed, "途中から入った人がいる");
        }

        [Test]
        public void Board_LeavingFreesTheCharacter()
        {
            var board = NewBoard(4, false, 2);
            board.Confirm(0);
            Assert.IsTrue(board.IsLockedFor(0, 1));
            board.SetActive(0, false);
            Assert.IsFalse(board.IsLockedFor(0, 1));
        }

        [Test]
        public void Board_CancelAndConfirmAgain()
        {
            var board = NewBoard(4, false, 1);
            Assert.IsTrue(board.Confirm(0));
            Assert.IsFalse(board.MoveNext(0), "決定中は動けない");
            Assert.IsTrue(board.Cancel(0));
            Assert.IsTrue(board.MoveNext(0));
            Assert.IsTrue(board.Confirm(0));
            Assert.AreEqual(1, board.GetChosen()[0]);
        }

        [Test]
        public void Board_FinishGivesEveryoneAFreeCharacter()
        {
            var board = NewBoard(4, false, 2);
            board.seats[1].cursor = 0;
            board.Confirm(0);
            board.seats[1].cursor = 0; // 届いた状態などで同じキャラにいる
            board.Finish();
            var chosen = board.GetChosen();
            Assert.AreEqual(0, chosen[0]);
            Assert.AreEqual(1, chosen[1], "使われていれば空いているキャラにする");
            Assert.AreEqual(-1, chosen[2]);
            Assert.IsFalse(board.MoveNext(0), "終わった後は動かない");
        }

        [Test]
        public void Detector_ShortPressFiresOnRelease()
        {
            var detector = new GripPressDetector();
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(false, 0.1f, 0.6f));
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 0.1f, 0.6f));
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 0.2f, 0.6f));
            Assert.AreEqual(GripPressDetector.Result.Short, detector.Tick(false, 0.1f, 0.6f));
        }

        [Test]
        public void Detector_LongPressFiresOnceWhileHeld()
        {
            var detector = new GripPressDetector();
            detector.Tick(false, 0.1f, 0.6f);
            detector.Tick(true, 0.1f, 0.6f);
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 0.3f, 0.6f));
            Assert.AreEqual(0.5f, detector.Progress, 1e-4f);
            Assert.AreEqual(GripPressDetector.Result.Long, detector.Tick(true, 0.3f, 0.6f));
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 1f, 0.6f));
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(false, 0.1f, 0.6f), "長く握った後に離しても「短く」にはならない");
        }

        [Test]
        public void Detector_MashingNeverBecomesLong()
        {
            // すばやい連打で「離して押し直す」が毎回1フレームの中に収まった（押しっぱなしに見える）場合。
            var detector = new GripPressDetector();
            detector.Tick(false, 0.016f, 0.6f);
            detector.Tick(true, 0.016f, 0.6f);
            var shorts = 0;
            for (var frame = 0; frame < 120; frame++)
            {
                var releases = frame % 6 == 5 ? 1 : 0;
                var result = detector.Tick(true, releases, 0.016f, 0.6f);
                Assert.AreNotEqual(GripPressDetector.Result.Long, result, $"{frame} フレーム目で長押しになった");
                shorts += result == GripPressDetector.Result.Short ? 1 : 0;
            }

            Assert.AreEqual(20, shorts, "連打の回数だけ「短く」になる");
        }

        [Test]
        public void Detector_HoldAfterMashStillConfirms()
        {
            var detector = new GripPressDetector();
            detector.Tick(false, 0.016f, 0.6f);
            detector.Tick(true, 0.016f, 0.6f);
            Assert.AreEqual(GripPressDetector.Result.Short, detector.Tick(true, 1, 0.016f, 0.6f), "離して押し直した");
            var fired = false;
            for (var frame = 0; frame < 40 && !fired; frame++)
            {
                fired = detector.Tick(true, 0, 0.016f, 0.6f) == GripPressDetector.Result.Long;
            }

            Assert.IsTrue(fired, "押し直した後、長く押し続ければ決定できる");
        }

        [Test]
        public void Detector_ResetDuringHoldNeedsRelease()
        {
            // 長押しの途中でカーソルが動かされた（ほかの人が先に READY）→ 取り消して、離すまで数えない。
            var detector = new GripPressDetector();
            detector.Tick(false, 0.016f, 0.6f);
            detector.Tick(true, 0.016f, 0.6f);
            detector.Tick(true, 0.4f, 0.6f);
            Assert.IsTrue(detector.IsPressing);
            detector.Reset();
            Assert.IsFalse(detector.IsPressing);
            for (var frame = 0; frame < 60; frame++)
            {
                Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 0.016f, 0.6f), "握り続けていても、移った先のキャラで決まらない");
            }

            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(false, 0.016f, 0.6f), "離しても「短く」にならない");
            detector.Tick(true, 0.016f, 0.6f);
            var fired = false;
            for (var frame = 0; frame < 40 && !fired; frame++)
            {
                fired = detector.Tick(true, 0.016f, 0.6f) == GripPressDetector.Result.Long;
            }

            Assert.IsTrue(fired, "握り直して長押しすれば決められる");
        }

        [Test]
        public void Board_OtherPlayerConfirmMovesHoverer()
        {
            // 2人が同じキャラ（0）にいて、P1 が先に決定 → P2 は次の空いているキャラへ動かされる（ここで P2 の長押しが取り消される）。
            var board = NewBoard(4, false, 2);
            board.seats[1].cursor = 0;
            Assert.IsTrue(board.Confirm(0));
            Assert.AreNotEqual(0, board.seats[1].cursor);
            Assert.IsFalse(board.seats[1].confirmed);
        }

        [Test]
        public void Squeeze_CountsQuickRegripsAsReleases()
        {
            var grip = new GripSqueezeTracker();
            var releases = 0;
            // なめらかにされた握力: 0.5〜0.9 の間を行ったり来たり（一度も「離した」強さ 0.2 まで下がらない）。
            var values = new[] { 0f, 0.5f, 0.9f, 0.9f, 0.55f, 0.5f, 0.9f, 0.9f, 0.55f, 0.5f, 0.9f };
            foreach (var v in values)
            {
                releases += grip.Tick(v, 0.35f, 0.2f, 0.25f);
            }

            Assert.AreEqual(2, releases, "2回ゆるめた");
            Assert.IsTrue(grip.IsPressed, "最後は握っている");
        }

        [Test]
        public void Squeeze_IgnoresGripHeldFromStart()
        {
            var grip = new GripSqueezeTracker();
            for (var i = 0; i < 10; i++)
            {
                grip.Tick(0.8f, 0.35f, 0.2f, 0.25f);
            }

            Assert.IsFalse(grip.IsPressed, "最初から握りっぱなしは数えない");
            grip.Tick(0.1f, 0.35f, 0.2f, 0.25f);
            grip.Tick(0.8f, 0.35f, 0.2f, 0.25f);
            Assert.IsTrue(grip.IsPressed);
        }

        [Test]
        public void Detector_IgnoresGripHeldFromBefore()
        {
            var detector = new GripPressDetector();
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(true, 2f, 0.6f), "最初から握っていた");
            Assert.AreEqual(GripPressDetector.Result.None, detector.Tick(false, 0.1f, 0.6f), "離しても何も起きない");
            detector.Tick(true, 0.1f, 0.6f);
            Assert.AreEqual(GripPressDetector.Result.Short, detector.Tick(false, 0.1f, 0.6f));
        }
    }
}
