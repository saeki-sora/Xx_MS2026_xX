using MS2026.Stage.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class SunPadMathTests
    {
        [TestCase(-35f, 55f)]
        [TestCase(0f, 30f)]
        [TestCase(90f, 45f)]
        [TestCase(-150f, 20f)]
        [TestCase(180f, 10f)]
        public void RoundTrip(float direction, float elevation)
        {
            var pad = SunPadMath.ToPad(direction, elevation);
            var (d, e) = SunPadMath.FromPad(pad, 0f);
            Assert.AreEqual(elevation, e, 0.51f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(direction, d), 0.51f);
        }

        [Test]
        public void TopOfPadMeansLightFromScreenTop()
        {
            var pad = SunPadMath.ToPad(0f, 45f);
            Assert.AreEqual(0f, pad.x, 1e-5f);
            Assert.Less(pad.y, 0f, "UIの y は下が正なので、画面の上は負");

            // 太陽の向きの計算（StageLightingSettings.SunRotation）と同じく、正の向きは画面の右から。
            Assert.Greater(SunPadMath.ToPad(90f, 45f).x, 0f);
        }

        [Test]
        public void CenterKeepsPreviousDirection_AndEdgeClampsElevation()
        {
            var (d, e) = SunPadMath.FromPad(Vector2.zero, 42f);
            Assert.AreEqual(42f, d);
            Assert.AreEqual(90f, e);

            var (_, low) = SunPadMath.FromPad(new Vector2(3f, 0f), 0f);
            Assert.AreEqual(SunPadMath.MinElevation, low);
        }
    }
}
