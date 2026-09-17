using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripSimulationMathTests
    {
        [Test]
        public void StepValue_WhenHeld_IncreasesTowardOne()
        {
            var result = GripSimulationMath.StepValue(
                currentValue: 0f,
                isHeld: true,
                deltaTime: 0.1f,
                riseSpeedPerSecond: 2f,
                decaySpeedPerSecond: 1f);

            Assert.AreEqual(0.2f, result, 1e-5f);
        }

        [Test]
        public void StepValue_WhenReleased_DecreasesTowardZero()
        {
            var result = GripSimulationMath.StepValue(
                currentValue: 0.5f,
                isHeld: false,
                deltaTime: 0.1f,
                riseSpeedPerSecond: 2f,
                decaySpeedPerSecond: 1f);

            Assert.AreEqual(0.4f, result, 1e-5f);
        }

        [Test]
        public void StepValue_ClampsAtOne_WhenHeldPastMax()
        {
            var result = GripSimulationMath.StepValue(
                currentValue: 0.95f,
                isHeld: true,
                deltaTime: 1f,
                riseSpeedPerSecond: 5f,
                decaySpeedPerSecond: 1f);

            Assert.AreEqual(1f, result);
        }

        [Test]
        public void StepValue_ClampsAtZero_WhenReleasedPastMin()
        {
            var result = GripSimulationMath.StepValue(
                currentValue: 0.05f,
                isHeld: false,
                deltaTime: 1f,
                riseSpeedPerSecond: 2f,
                decaySpeedPerSecond: 5f);

            Assert.AreEqual(0f, result);
        }

        [Test]
        public void StepValue_ZeroDeltaTime_DoesNotChangeValue()
        {
            var result = GripSimulationMath.StepValue(
                currentValue: 0.42f,
                isHeld: true,
                deltaTime: 0f,
                riseSpeedPerSecond: 10f,
                decaySpeedPerSecond: 10f);

            Assert.AreEqual(0.42f, result, 1e-5f);
        }
    }
}
