using MS2026.GripInputBridge.Relay;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripRelayOptionsTests
    {
        [Test]
        public void NoArguments_IsDisabled()
        {
            var options = GripRelayOptions.Parse(new[] { @"C:\Game\MS2026.exe" });
            Assert.IsFalse(options.Enabled);
            Assert.IsNull(options.Error);
            Assert.AreEqual(GripRelayOptions.DefaultBasePort, options.BasePort);

            Assert.IsFalse(GripRelayOptions.Parse(null).Enabled);
        }

        [TestCase("-grip-sensor", "3", 3)]
        [TestCase("-GRIP-SENSOR", "1", 1)]
        [TestCase("-grip-sensor", " 4 ", 4)]
        public void SensorNumber_SpaceSeparated(string name, string value, int expected)
        {
            var options = GripRelayOptions.Parse(new[] { "MS2026.exe", "-debug-ui", name, value, "-lobby" });
            Assert.IsTrue(options.Enabled);
            Assert.AreEqual(expected, options.SensorNumber);
            Assert.IsNull(options.Error);
            Assert.AreEqual(GripRelayOptions.DefaultBasePort + expected - 1, options.ListenPort);
        }

        [Test]
        public void SensorNumber_EqualsForm()
        {
            var options = GripRelayOptions.Parse(new[] { "-grip-sensor=2" });
            Assert.AreEqual(2, options.SensorNumber);
        }

        [TestCase("5")]
        [TestCase("-1")]
        [TestCase("three")]
        public void InvalidSensorNumber_IsDisabledWithError(string value)
        {
            var options = GripRelayOptions.Parse(new[] { "-grip-sensor", value });
            Assert.IsFalse(options.Enabled);
            Assert.IsNotNull(options.Error);
        }

        [Test]
        public void MissingSensorValue_IsDisabledWithError()
        {
            Assert.IsNotNull(GripRelayOptions.Parse(new[] { "-grip-sensor" }).Error);

            // 次が別の起動オプションなら、それを番号として読まない。
            var options = GripRelayOptions.Parse(new[] { "-grip-sensor", "-lobby" });
            Assert.IsFalse(options.Enabled);
            Assert.IsNotNull(options.Error);
        }

        [Test]
        public void ZeroMeansOff_WithoutError()
        {
            var options = GripRelayOptions.Parse(new[] { "-grip-sensor", "0" });
            Assert.IsFalse(options.Enabled);
            Assert.IsNull(options.Error);
        }

        [Test]
        public void ForceSend_NeedsSensor()
        {
            var ok = GripRelayOptions.Parse(new[] { "-grip-sensor", "1", "-grip-relay-send" });
            Assert.IsTrue(ok.ForceSend);
            Assert.IsNull(ok.Error);

            var alone = GripRelayOptions.Parse(new[] { "-grip-relay-send" });
            Assert.IsFalse(alone.Enabled);
            Assert.IsNotNull(alone.Error);
        }

        [Test]
        public void Port_CanBeChanged_AndIsValidated()
        {
            var options = GripRelayOptions.Parse(new[] { "-grip-relay-port", "50000", "-grip-sensor", "4" });
            Assert.AreEqual(50000, options.BasePort);
            Assert.AreEqual(50003, options.ListenPort);

            var tooHigh = GripRelayOptions.Parse(new[] { "-grip-sensor", "1", "-grip-relay-port=65534" });
            Assert.AreEqual(GripRelayOptions.DefaultBasePort, tooHigh.BasePort);
            Assert.IsNotNull(tooHigh.Error);
            Assert.IsTrue(tooHigh.Enabled);

            Assert.AreEqual(60000, GripRelayOptions.Parse(new string[0], 60000).BasePort);
        }

        [Test]
        public void Create_ForEditor()
        {
            var options = GripRelayOptions.Create(3, true, 48000);
            Assert.AreEqual(3, options.SensorNumber);
            Assert.IsTrue(options.ForceSend);
            Assert.AreEqual(48002, options.ListenPort);
            Assert.IsNull(options.Error);

            Assert.IsFalse(GripRelayOptions.Create(9, false, 48000).Enabled);
        }
    }
}
