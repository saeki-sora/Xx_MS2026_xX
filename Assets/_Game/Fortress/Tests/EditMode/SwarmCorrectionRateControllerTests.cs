using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmCorrectionRateControllerTests
    {
        private static SwarmCorrectionRateController Create()
        {
            return new SwarmCorrectionRateController { MinCycle = 0.15f, MaxCycle = 0.5f, HighError = 1f, LowError = 0.3f, RelaxSecondsPerSecond = 0.1f };
        }

        [Test]
        public void NoReport_StaysAtNormalCycle()
        {
            var controller = Create();

            Assert.AreEqual(0.5f, controller.Update(0.1f, -1f), 1e-5f);
        }

        [Test]
        public void LargeError_ShrinksImmediatelyToMinimum()
        {
            var controller = Create();

            Assert.AreEqual(0.15f, controller.Update(0.016f, 10f), 1e-5f);
        }

        [Test]
        public void MediumError_InterpolatesBetweenMinAndMax()
        {
            var controller = Create();

            // 0.65 は 0.3〜1.0 のちょうど中間 → 0.5 と 0.15 の中間。
            Assert.AreEqual(0.325f, controller.Update(0.016f, 0.65f), 1e-4f);
        }

        [Test]
        public void SmallError_RelaxesSlowlyBackToNormal()
        {
            var controller = Create();
            controller.Update(0.016f, 10f);

            var afterOneSecond = controller.Update(1f, 0.1f);
            Assert.AreEqual(0.25f, afterOneSecond, 1e-4f);

            for (var i = 0; i < 10; i++)
            {
                controller.Update(1f, 0.1f);
            }

            Assert.AreEqual(0.5f, controller.CurrentCycle, 1e-5f);
        }

        [Test]
        public void MinLargerThanMax_IsClampedToMax()
        {
            var controller = Create();
            controller.MinCycle = 0.8f;

            Assert.AreEqual(0.5f, controller.Update(0.1f, 10f), 1e-5f);
        }
    }
}
