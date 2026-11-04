using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class EdgeMistMathTests
    {
        [Test]
        public void CoreBehindCameraUsesScreenCentre()
        {
            Assert.AreEqual(new Vector2(0.5f, 0.5f), EdgeMistMath.CoreViewport(new Vector3(0.1f, 0.9f, -2f)));
        }

        [Test]
        public void CoreOutsideScreenIsKeptInside()
        {
            var core = EdgeMistMath.CoreViewport(new Vector3(-0.4f, 1.6f, 5f));
            Assert.AreEqual(EdgeMistMath.CoreMargin, core.x, 1e-5f);
            Assert.AreEqual(1f - EdgeMistMath.CoreMargin, core.y, 1e-5f);
        }

        [Test]
        public void RatioIsZeroAtCoreAndOneAtScreenEdge()
        {
            var core = new Vector2(0.5f, 0.5f);
            Assert.AreEqual(0f, EdgeMistMath.EdgeRatio(core, core, 3f), 1e-4f);
            Assert.AreEqual(1f, EdgeMistMath.EdgeRatio(new Vector2(1f, 0.5f), core, 3f), 1e-4f);
            Assert.AreEqual(1f, EdgeMistMath.EdgeRatio(new Vector2(0.5f, 0f), core, 3f), 1e-4f);
        }

        [Test]
        public void CornersAreRoundedNotCreased()
        {
            // 斜めの所は、四角い形（大きい方だけ）より少し濃い側へ丸くふくらむ。とても四角くすると大きい方だけに近づく。
            var core = new Vector2(0.5f, 0.5f);
            var diagonal = new Vector2(0.75f, 0.75f);
            Assert.AreEqual(0.5f * Mathf.Pow(2f, 1f / 3f), EdgeMistMath.EdgeRatio(diagonal, core, 3f), 1e-4f);
            Assert.AreEqual(0.5f, EdgeMistMath.EdgeRatio(diagonal, core, 60f), 0.01f);
        }

        [Test]
        public void FollowMovesTowardTargetWithoutOvershoot()
        {
            var a = new Vector2(0.5f, 0.98f);
            var b = new Vector2(0.5f, 0.5f);
            var step = EdgeMistMath.Follow(a, b, 8f, 1f / 60f);
            Assert.Less(step.y, a.y);
            Assert.Greater(step.y, b.y);
            Assert.AreEqual(b.y, EdgeMistMath.Follow(a, b, 8f, 5f).y, 1e-3f);
        }

        [Test]
        public void FarSideOfOffCentreCoreGetsWiderMist()
        {
            // コアが画面の下寄りなら、同じ「端から 0.1」の所でも、上（コアから遠い側）は割合が1に近い＝靄の中、下（コアに近い側）はまだ靄の外。
            var core = new Vector2(0.5f, 0.2f);
            var nearTop = EdgeMistMath.EdgeRatio(new Vector2(0.5f, 0.9f), core, 3f);
            var nearBottom = EdgeMistMath.EdgeRatio(new Vector2(0.5f, 0.1f), core, 3f);
            Assert.AreEqual(0.7f / 0.8f, nearTop, 1e-4f);
            Assert.AreEqual(0.5f, nearBottom, 1e-4f);
            Assert.Greater(nearTop, nearBottom);
        }
    }
}
