using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class RemoteGripBufferTests
    {
        private const double Timeout = 0.5;

        [Test]
        public void NothingReceived_ReturnsReleased()
        {
            var buffer = new RemoteGripBuffer(4);

            buffer.Get(1, 10.0, Timeout, out var isGripping, out var grip);

            Assert.IsFalse(isGripping);
            Assert.AreEqual(0f, grip);
        }

        [Test]
        public void LatestValue_IsReturned()
        {
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(1, true, 0.4f, 1, 10.0, Timeout);
            buffer.Submit(1, true, 0.7f, 2, 10.03, Timeout);

            buffer.Get(1, 10.05, Timeout, out var isGripping, out var grip);

            Assert.IsTrue(isGripping);
            Assert.AreEqual(0.7f, grip, 1e-5f);
        }

        [Test]
        public void OlderSequence_IsDiscarded()
        {
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(2, true, 0.9f, 5, 10.0, Timeout);

            var accepted = buffer.Submit(2, false, 0f, 4, 10.01, Timeout);
            buffer.Get(2, 10.02, Timeout, out var isGripping, out var grip);

            Assert.IsFalse(accepted);
            Assert.IsTrue(isGripping);
            Assert.AreEqual(0.9f, grip, 1e-5f);
        }

        [Test]
        public void SequenceWrapAround_IsTreatedAsNewer()
        {
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(0, true, 0.2f, uint.MaxValue, 10.0, Timeout);

            var accepted = buffer.Submit(0, true, 0.3f, 0, 10.01, Timeout);

            Assert.IsTrue(accepted);
        }

        [Test]
        public void Stale_IsTreatedAsReleased()
        {
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(3, true, 1f, 1, 10.0, Timeout);

            buffer.Get(3, 10.0 + Timeout + 0.01, Timeout, out var isGripping, out var grip);

            Assert.IsFalse(isGripping);
            Assert.AreEqual(0f, grip);
        }

        [Test]
        public void AfterStale_RestartedSequence_IsAccepted()
        {
            // 再接続でClientの連番が1から振り直されても、途切れた後なら受け入れる。
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(1, true, 0.5f, 1000, 10.0, Timeout);

            var accepted = buffer.Submit(1, true, 0.6f, 1, 11.0, Timeout);

            Assert.IsTrue(accepted);
        }

        [Test]
        public void GripValue_IsClamped()
        {
            var buffer = new RemoteGripBuffer(4);
            buffer.Submit(0, true, 3f, 1, 10.0, Timeout);

            buffer.Get(0, 10.0, Timeout, out _, out var grip);

            Assert.AreEqual(1f, grip);
        }

        [Test]
        public void OutOfRangePlayer_IsIgnored()
        {
            var buffer = new RemoteGripBuffer(4);

            Assert.IsFalse(buffer.Submit(4, true, 1f, 1, 10.0, Timeout));
            Assert.IsFalse(buffer.Submit(-1, true, 1f, 1, 10.0, Timeout));
            Assert.DoesNotThrow(() => buffer.Get(7, 10.0, Timeout, out _, out _));
        }
    }
}
