using System.Collections.Generic;
using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripPortReportTests
    {
        private const string Bluetooth = @"\Device\BthModem0";
        private const string ArduinoUno = @"\Device\USBSER000";

        [Test]
        public void TellsDeviceKindsApart()
        {
            StringAssert.StartsWith("Bluetooth", GripPortKind.Describe(Bluetooth));
            StringAssert.Contains("Arduino 純正", GripPortKind.Describe(ArduinoUno));
            StringAssert.Contains("CH340", GripPortKind.Describe(@"\Device\CH341SER0"));
            StringAssert.Contains("FTDI", GripPortKind.Describe(@"\Device\VCP0"));
            StringAssert.StartsWith("PC 本体", GripPortKind.Describe(@"\Device\Serial0"));
            Assert.AreEqual("Something7", GripPortKind.Describe(@"\Device\Something7"));
            Assert.IsNull(GripPortKind.Describe(null));

            Assert.IsFalse(GripPortKind.IsUsb(Bluetooth));
            Assert.IsFalse(GripPortKind.IsUsb(@"\Device\Serial0"));
            Assert.IsTrue(GripPortKind.IsUsb(ArduinoUno));
            Assert.IsTrue(GripPortKind.IsUsb(@"\Device\Something7"));
            Assert.IsFalse(GripPortKind.IsUsb(null));
        }

        [Test]
        public void OnlyBluetoothPorts_MeansArduinoIsNotRecognized()
        {
            // 今回の実例: COM5（設定の初期値で、このPCに無い）・COM3/COM4（Bluetooth）。
            var reports = new List<GripPortReport>
            {
                new GripPortReport("COM5", GripPortState.NotPresent),
                new GripPortReport("COM3", GripPortState.Silent, Bluetooth),
                new GripPortReport("COM4", GripPortState.Silent, Bluetooth)
            };

            StringAssert.StartsWith("PC が Arduino を認識していません", GripPortReport.Hint(reports, 9600));
            StringAssert.Contains("COM5  このPCに無い", GripPortReport.Summarize(reports));
            StringAssert.Contains("COM3  Bluetooth（Arduino ではない）  開けたが何も届かない", GripPortReport.Summarize(reports));
        }

        [Test]
        public void NoPortsAtAll_MeansArduinoIsNotRecognized()
        {
            var reports = new List<GripPortReport> { new GripPortReport("COM5", GripPortState.NotPresent) };
            StringAssert.StartsWith("PC が Arduino を認識していません", GripPortReport.Hint(reports, 9600));
            Assert.AreEqual("（COMポートがありません）", GripPortReport.Summarize(new List<GripPortReport>()));
        }

        [Test]
        public void UnknownKinds_DoNotClaimNotRecognized()
        {
            // 機器の名前が取れない（レジストリを読めない）ときは、「認識していない」とは言わない。
            var reports = new List<GripPortReport> { new GripPortReport("COM3", GripPortState.Silent) };
            StringAssert.StartsWith("開けたのに何も届きません", GripPortReport.Hint(reports, 9600));
        }

        [Test]
        public void BusyPort_SaysCloseTheSerialMonitor()
        {
            var reports = new List<GripPortReport>
            {
                new GripPortReport("COM3", GripPortState.Silent, Bluetooth),
                new GripPortReport("COM7", GripPortState.OpenFailed, ArduinoUno, "Access denied", true)
            };

            StringAssert.Contains("シリアルモニタ", GripPortReport.Hint(reports, 9600));
            StringAssert.Contains("ほかのソフトが使用中", reports[1].ToText());
        }

        [Test]
        public void UnreadableLines_ShowTheLineAndTheBaudRate()
        {
            var report = new GripPortReport("COM7", GripPortState.Unreadable, ArduinoUno, linesReceived: 12, lastRejectedLine: "512,300,100,50,\r");
            StringAssert.Contains("届くが読めない（12行）: 512,300,100,50,\\r", report.ToText());
            StringAssert.Contains("Serial.begin(9600)", GripPortReport.Hint(new List<GripPortReport> { report }, 9600));
        }

        [Test]
        public void ReadablePort_HasNoHint()
        {
            var reports = new List<GripPortReport> { new GripPortReport("COM7", GripPortState.Readable, ArduinoUno) };
            Assert.IsNull(GripPortReport.Hint(reports, 9600));
            Assert.IsNull(GripPortReport.Hint(null, 9600));
        }
    }
}
