using MS2026.Stage.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageAlignMathTests
    {
        private static readonly Vector2[] Spans = { new Vector2(0f, 1f), new Vector2(3f, 5f), new Vector2(10f, 11f) };

        [Test]
        public void AlignDeltas_MovesEdgesToTheOutermostOrCenter()
        {
            CollectionAssert.AreEqual(new[] { 0f, -3f, -10f }, StageAlignMath.AlignDeltas(Spans, StageAlignEdge.Min));
            CollectionAssert.AreEqual(new[] { 10f, 6f, 0f }, StageAlignMath.AlignDeltas(Spans, StageAlignEdge.Max));
            var center = StageAlignMath.AlignDeltas(Spans, StageAlignEdge.Center);
            Assert.AreEqual(5.5f - 0.5f, center[0], 1e-5f);
            Assert.AreEqual(5.5f - 4f, center[1], 1e-5f);
        }

        [Test]
        public void DistributeDeltas_EqualGapsKeepEnds()
        {
            // 幅 1, 2, 1 → 全体 0〜11、すき間 = (11 − 4) / 2 = 3.5 → 真ん中は 4.5〜6.5。
            var d = StageAlignMath.DistributeDeltas(Spans);
            Assert.AreEqual(0f, d[0], 1e-5f);
            Assert.AreEqual(1.5f, d[1], 1e-5f);
            Assert.AreEqual(0f, d[2], 1e-5f);
        }

        [Test]
        public void DistributeDeltas_UsesCenterOrderAndNeedsThree()
        {
            var shuffled = new[] { new Vector2(10f, 11f), new Vector2(0f, 1f), new Vector2(3f, 5f) };
            var d = StageAlignMath.DistributeDeltas(shuffled);
            Assert.AreEqual(1.5f, d[2], 1e-5f);
            CollectionAssert.AreEqual(new[] { 0f, 0f }, StageAlignMath.DistributeDeltas(new[] { Vector2.zero, Vector2.one }));
        }
    }
}
