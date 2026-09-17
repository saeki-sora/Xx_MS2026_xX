using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripWaveformPresetMathTests
    {
        [Test]
        public void Evaluate_None_ReturnsZero()
        {
            Assert.AreEqual(0f, GripWaveformPresetMath.Evaluate(GripWaveformPreset.None, 1.23f));
        }

        [TestCase(GripWaveformPreset.Soft, 0.2f, 0.3f)]
        [TestCase(GripWaveformPreset.Firm, 0.5f, 0.6f)]
        [TestCase(GripWaveformPreset.Full, 0.9f, 1.0f)]
        public void Evaluate_ConstantBandPresets_StayWithinExpectedRange(GripWaveformPreset preset, float min, float max)
        {
            for (var t = 0f; t < 5f; t += 0.1f)
            {
                var value = GripWaveformPresetMath.Evaluate(preset, t);
                Assert.GreaterOrEqual(value, min - 1e-4f, $"t={t}");
                Assert.LessOrEqual(value, max + 1e-4f, $"t={t}");
            }
        }

        [Test]
        public void Evaluate_RhythmTestPulse_AlternatesBetweenHighAndLow()
        {
            var atStart = GripWaveformPresetMath.Evaluate(GripWaveformPreset.RhythmTestPulse, 0f);
            var afterOneBeat = GripWaveformPresetMath.Evaluate(GripWaveformPreset.RhythmTestPulse, 0.5f); // 1拍目(0.4s)を過ぎた直後
            var afterTwoBeats = GripWaveformPresetMath.Evaluate(GripWaveformPreset.RhythmTestPulse, 0.9f); // 2拍目(0.8s)を過ぎた直後

            Assert.Greater(atStart, 0.5f);
            Assert.AreEqual(0f, afterOneBeat);
            Assert.Greater(afterTwoBeats, 0.5f);
        }
    }
}
