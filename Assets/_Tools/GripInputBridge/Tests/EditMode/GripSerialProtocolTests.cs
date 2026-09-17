using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripSerialProtocolTests
    {
        [Test]
        public void TryParseLine_ValidLine_ParsesAllFields()
        {
            var ok = GripSerialProtocol.TryParseLine("G,0,2048,183920", out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, sample.PlayerIndex);
            Assert.AreEqual(2048f / 4095f, sample.NormalizedRawValue, 1e-5f);
            Assert.AreEqual(183920L, sample.DeviceTimestampMs);
        }

        [Test]
        public void TryParseLine_MaxRawValue_NormalizesToOne()
        {
            var ok = GripSerialProtocol.TryParseLine("G,3,4095,0", out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(3, sample.PlayerIndex);
            Assert.AreEqual(1f, sample.NormalizedRawValue, 1e-5f);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("garbage")]
        [TestCase("G,0,2048")] // トークン数不足
        [TestCase("X,0,2048,100")] // 先頭文字が不正
        [TestCase("G,4,2048,100")] // プレイヤー番号が範囲外(0-3のみ有効)
        [TestCase("G,-1,2048,100")] // プレイヤー番号が負
        [TestCase("G,0,4096,100")] // 生値が範囲外(0-4095のみ有効)
        [TestCase("G,0,-1,100")]
        [TestCase("G,abc,2048,100")]
        public void TryParseLine_InvalidInput_ReturnsFalse_WithoutThrowing(string line)
        {
            var ok = GripSerialProtocol.TryParseLine(line, out _);

            Assert.IsFalse(ok);
        }

        [Test]
        public void BuildLine_And_TryParseLine_AreRoundTrippable()
        {
            var line = GripSerialProtocol.BuildLine(playerIndex: 2, rawAdcValue: 1024, deviceTimestampMs: 999);

            var ok = GripSerialProtocol.TryParseLine(line, out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, sample.PlayerIndex);
            Assert.AreEqual(1024f / 4095f, sample.NormalizedRawValue, 1e-5f);
            Assert.AreEqual(999L, sample.DeviceTimestampMs);
        }
    }
}
