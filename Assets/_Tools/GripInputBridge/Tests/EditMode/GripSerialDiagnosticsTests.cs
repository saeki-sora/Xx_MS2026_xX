using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripSerialDiagnosticsTests
    {
        [Test]
        public void ShowsInvisibleCharactersAsEscapes()
        {
            Assert.AreEqual("512,300\\r", GripSerialDiagnostics.ToVisibleText("512,300\r"));
            Assert.AreEqual("a\\tb\\x00\\n", GripSerialDiagnostics.ToVisibleText("a\tb\0\n"));
        }

        [Test]
        public void CutsLongLines()
        {
            Assert.AreEqual("12345…", GripSerialDiagnostics.ToVisibleText("1234567890", 5));
            Assert.AreEqual("12345", GripSerialDiagnostics.ToVisibleText("12345", 5));
        }

        [Test]
        public void NullMeansNothingReceivedYet()
        {
            Assert.AreEqual("(まだ届いていません)", GripSerialDiagnostics.ToVisibleText(null));
            Assert.AreEqual(string.Empty, GripSerialDiagnostics.ToVisibleText(string.Empty));
        }
    }
}
