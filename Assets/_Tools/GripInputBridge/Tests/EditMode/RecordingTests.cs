using System;
using System.IO;
using MS2026.GripInputBridge.Recording;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class RecordingTests
    {
        private string _tempDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "GripInputBridgeTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void GripSessionRecorder_CreatesFile_InSpecifiedDirectory()
        {
            using (var recorder = new GripSessionRecorder(_tempDirectory))
            {
                recorder.RecordSample(0, 0.1f, 0.2f, 0.3f);
                recorder.Flush();

                Assert.IsTrue(File.Exists(recorder.FilePath));
                StringAssert.StartsWith(_tempDirectory, recorder.FilePath);
            }
        }

        [Test]
        public void GripSessionRecorder_WritesOneJsonLinePerSample()
        {
            string filePath;
            using (var recorder = new GripSessionRecorder(_tempDirectory))
            {
                filePath = recorder.FilePath;
                recorder.RecordSample(0, 0.1f, 0.1f, 0.1f);
                recorder.RecordSample(1, 0.2f, 0.2f, 0.2f);
            }

            var lines = File.ReadAllLines(filePath);
            Assert.AreEqual(2, lines.Length);
        }

        [Test]
        public void ReplayGripTransport_ReadsBackRecordedRawValue()
        {
            string filePath;
            using (var recorder = new GripSessionRecorder(_tempDirectory))
            {
                filePath = recorder.FilePath;
                recorder.RecordSample(0, raw: 0.42f, filtered: 0.4f, normalized: 0.5f);
            }

            // 記録直後(t=0付近)の1サンプル目なので、再生側は即座にこの値を返せる想定。
            var replay = new ReplayGripTransport(filePath);

            Assert.IsTrue(replay.IsConnected(0));
            Assert.AreEqual(0.42f, replay.GetRawValue(0), 1e-4f);
        }

        [Test]
        public void ReplayGripTransport_UnrecordedPlayer_IsNotConnected()
        {
            string filePath;
            using (var recorder = new GripSessionRecorder(_tempDirectory))
            {
                filePath = recorder.FilePath;
                recorder.RecordSample(0, 0.5f, 0.5f, 0.5f);
            }

            var replay = new ReplayGripTransport(filePath);

            Assert.IsFalse(replay.IsConnected(1));
            Assert.AreEqual(0f, replay.GetRawValue(1));
        }

        [Test]
        public void ReplayGripTransport_NullFilePath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ReplayGripTransport(null));
        }
    }
}
