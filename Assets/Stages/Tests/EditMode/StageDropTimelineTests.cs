using NUnit.Framework;

namespace MS2026.Stages.Tests
{
    public sealed class StageDropTimelineTests
    {
        [Test]
        public void Evaluate_FallsFasterAndFasterThenLands()
        {
            var before = StageDropTimeline.Evaluate(-0.1f, 0.5f, 0.1f, 0.2f, 0.2f);
            Assert.AreEqual(1f, before.Height01, 1e-5f, "落ち始める前は一番上");
            Assert.IsFalse(before.Landed);

            var quarter = StageDropTimeline.Evaluate(0.125f, 0.5f, 0.1f, 0.2f, 0.2f);
            var half = StageDropTimeline.Evaluate(0.25f, 0.5f, 0.1f, 0.2f, 0.2f);
            var threeQuarter = StageDropTimeline.Evaluate(0.375f, 0.5f, 0.1f, 0.2f, 0.2f);
            Assert.AreEqual(0.75f, half.Height01, 1e-5f, "高さ = 1 − 進み²");
            Assert.Greater(half.Height01 - threeQuarter.Height01, quarter.Height01 - half.Height01, "後ほど速く落ちる");
            Assert.Greater(half.Stretch, 0f, "落ちる間は伸びる");

            var landed = StageDropTimeline.Evaluate(0.55f, 0.5f, 0.1f, 0.2f, 0.2f);
            Assert.IsTrue(landed.Landed);
            Assert.AreEqual(0f, landed.Height01, 1e-5f);
            Assert.Greater(landed.Squash, 0f, "着地の直後はつぶれている");
            Assert.LessOrEqual(landed.Squash, 0.2f);
        }

        [Test]
        public void Evaluate_ReturnsToRestAfterSquash()
        {
            var rest = StageDropTimeline.Evaluate(0.71f, 0.5f, 0.1f, 0.2f, 0.2f);
            Assert.IsTrue(rest.Landed);
            Assert.AreEqual(0f, rest.Squash, 1e-5f);
            Assert.AreEqual(0f, rest.Stretch, 1e-5f);
            Assert.AreEqual(0f, rest.Height01, 1e-5f);
        }

        [Test]
        public void PropDelays_FitInSpreadAndRespectMaxInterval()
        {
            var few = StageDropTimeline.PropDelays(3, 0.45f, 0.12f);
            CollectionAssert.AreEqual(new[] { 0f, 0.12f, 0.24f }, few, "少ないときは上限の間隔");

            var many = StageDropTimeline.PropDelays(10, 0.45f, 0.12f);
            Assert.AreEqual(0.45f, many[9], 1e-5f, "多いときは全体の秒数に収める");
            Assert.AreEqual(0.05f, many[1], 1e-5f);

            Assert.AreEqual(0, StageDropTimeline.PropDelays(0, 0.45f, 0.12f).Length);
            CollectionAssert.AreEqual(new[] { 0f }, StageDropTimeline.PropDelays(1, 0.45f, 0.12f));
        }

        [Test]
        public void ShuffledOrder_IsAPermutationAndRepeatable()
        {
            var a = StageDropTimeline.ShuffledOrder(8, 2026);
            var b = StageDropTimeline.ShuffledOrder(8, 2026);
            CollectionAssert.AreEqual(a, b, "同じ seed なら同じ順（全PCで同じ）");
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, a, "全部 1 回ずつ");
            Assert.AreEqual(0, StageDropTimeline.ShuffledOrder(0, 1).Length);
        }
    }
}
