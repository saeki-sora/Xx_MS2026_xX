using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripPortCandidatesTests
    {
        [Test]
        public void ConfiguredPortComesFirst()
        {
            var ports = GripPortCandidates.Build("COM5", new[] { "COM1", "COM3", "COM5" }, true);
            CollectionAssert.AreEqual(new[] { "COM5", "COM1", "COM3" }, ports);
        }

        [Test]
        public void RemovesDuplicatesAndBlanksIgnoringCase()
        {
            var ports = GripPortCandidates.Build(" com5 ", new[] { "COM5", "", null, "COM3", "com3" }, true);
            CollectionAssert.AreEqual(new[] { "com5", "COM3" }, ports);
        }

        [Test]
        public void OnlyConfiguredPortWhenNotSearchingOthers()
        {
            CollectionAssert.AreEqual(new[] { "COM5" }, GripPortCandidates.Build("COM5", new[] { "COM3" }, false));
        }

        [Test]
        public void WorksWithoutConfiguredPortOrPortList()
        {
            CollectionAssert.AreEqual(new[] { "COM3" }, GripPortCandidates.Build("", new[] { "COM3" }, true));
            Assert.IsEmpty(GripPortCandidates.Build(null, null, true));
        }
    }
}
