using System.Collections.Generic;
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

        [Test]
        public void TryParseLine_LineWithTrailingCarriageReturn_Parses()
        {
            var ok = GripSerialProtocol.TryParseLine("G,1,4095,10\r", out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, sample.PlayerIndex);
            Assert.AreEqual(1f, sample.NormalizedRawValue, 1e-5f);
        }

        [TestCase("0", 0f)]
        [TestCase("512\r", 512f / 1023f)]
        [TestCase("1023", 1f)]
        public void TryParseLine_SimpleIntegerLine_ParsesAsPlayerZeroTenBit(string line, float expected)
        {
            var ok = GripSerialProtocol.TryParseLine(line, out var sample);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, sample.PlayerIndex);
            Assert.AreEqual(expected, sample.NormalizedRawValue, 1e-5f);
        }

        [Test]
        public void TryParseSamples_CommaSeparatedSimpleValues_ParsesInPlayerOrder()
        {
            var samples = new List<GripSerialProtocol.ParsedSample>();

            var ok = GripSerialProtocol.TryParseSamples("512,1023\r", samples);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, samples.Count);
            Assert.AreEqual(0, samples[0].PlayerIndex);
            Assert.AreEqual(512f / 1023f, samples[0].NormalizedRawValue, 1e-5f);
            Assert.AreEqual(1, samples[1].PlayerIndex);
            Assert.AreEqual(1f, samples[1].NormalizedRawValue, 1e-5f);
        }

        [Test]
        public void TryParseSamples_ProtocolLine_ReturnsSingleSample()
        {
            var samples = new List<GripSerialProtocol.ParsedSample>();

            var ok = GripSerialProtocol.TryParseSamples("G,2,4095,0", samples);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, samples.Count);
            Assert.AreEqual(2, samples[0].PlayerIndex);
        }

        [TestCase("")]
        [TestCase("512,")] // 空の値
        [TestCase("512,1024")] // 範囲外
        [TestCase("1,2,3,4,5")] // プレイヤー数(4)を超える
        [TestCase("G,4,2048,100")] // 不正な正式フォーマットを簡易フォーマットとして誤読しない
        public void TryParseSamples_InvalidInput_ReturnsFalse_AndLeavesNoSamples(string line)
        {
            var samples = new List<GripSerialProtocol.ParsedSample>();

            var ok = GripSerialProtocol.TryParseSamples(line, samples);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, samples.Count);
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
        [TestCase("1024")] // 簡易フォーマットの範囲外(0-1023のみ有効)
        [TestCase("-1")]
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
