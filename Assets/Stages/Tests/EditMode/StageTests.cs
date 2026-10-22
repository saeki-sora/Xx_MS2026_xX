using NUnit.Framework;

namespace MS2026.Stages.Tests
{
    public sealed class StageTests
    {
        [Test]
        public void PickIndex_SkipsPreviousWhenAvoidingRepeat()
        {
            var eligible = new[] { true, true, false };
            for (var i = 0; i < 20; i++)
            {
                Assert.AreEqual(1, StageCatalog.PickIndex(eligible, 0, true, i / 20f), "前回 0 なら、選べるのは 1 だけ");
            }
        }

        [Test]
        public void PickIndex_AllowsRepeatWhenOnlyOneOrNotAvoiding()
        {
            Assert.AreEqual(0, StageCatalog.PickIndex(new[] { true, false }, 0, true, 0.5f), "1 つしか無ければ前回と同じでも選ぶ");
            Assert.AreEqual(0, StageCatalog.PickIndex(new[] { true, true }, 0, false, 0f), "続けて同じでもよい設定");
            Assert.AreEqual(1, StageCatalog.PickIndex(new[] { true, true }, 0, false, 0.99f));
        }

        [Test]
        public void PickIndex_NoneEligible()
        {
            Assert.AreEqual(-1, StageCatalog.PickIndex(new[] { false, false }, -1, true, 0.3f));
            Assert.AreEqual(-1, StageCatalog.PickIndex(null, -1, true, 0.3f));
        }

        [Test]
        public void PickIndex_CoversEveryChoice()
        {
            var eligible = new[] { true, false, true, true };
            Assert.AreEqual(0, StageCatalog.PickIndex(eligible, -1, true, 0f));
            Assert.AreEqual(2, StageCatalog.PickIndex(eligible, -1, true, 0.5f));
            Assert.AreEqual(3, StageCatalog.PickIndex(eligible, -1, true, 1f), "1 ちょうどでも範囲内");
        }

        [Test]
        public void Roulette_StopsExactlyOnChosenCard()
        {
            const int Cards = 3;
            var final = StageRouletteMath.FinalPosition(Cards, 2, 4);
            Assert.AreEqual(14f, final);
            Assert.AreEqual(final, StageRouletteMath.PositionAt(3.6f, 3.6f, final, 0.2f));
            Assert.AreEqual(2, StageRouletteMath.CenterSlot(final, Cards));
            Assert.AreEqual(0f, StageRouletteMath.PositionAt(0f, 3.6f, final, 0.2f), 1e-4f);
        }

        [Test]
        public void Roulette_SlowsDownAndOvershoots()
        {
            var final = StageRouletteMath.FinalPosition(2, 1, 4);
            var early = StageRouletteMath.PositionAt(0.4f, 4f, final, 0f) - StageRouletteMath.PositionAt(0.2f, 4f, final, 0f);
            var late = StageRouletteMath.PositionAt(3.2f, 4f, final, 0f) - StageRouletteMath.PositionAt(3.0f, 4f, final, 0f);
            Assert.Greater(early, late, "だんだんゆっくりになる");
            Assert.Greater(StageRouletteMath.PositionAt(3.8f, 4f, final, 0.3f), final, "止まる直前に行き過ぎる");
        }

        [Test]
        public void Roulette_SlotsWrapAround()
        {
            Assert.AreEqual(2, StageRouletteMath.SlotAt(0f, -1, 3));
            Assert.AreEqual(1, StageRouletteMath.SlotAt(3.4f, 1, 3));
            Assert.AreEqual(0, StageRouletteMath.CenterSlot(2.6f, 3));
        }
    }
}
