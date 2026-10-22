using MS2026.GripInputBridge.Relay;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripRelayReceiverStateTests
    {
        private const uint Own = 1000;
        private const double Timeout = 0.5;

        private double _now;
        private GripRelayReceiverState _state;

        [SetUp]
        public void SetUp()
        {
            _now = 10;
            _state = new GripRelayReceiverState(Own, Timeout, () => _now);
        }

        private static GripRelayPacket Packet(uint sender, uint sequence, float value, bool connected = true, string name = "PC")
        {
            var packet = new GripRelayPacket { SenderId = sender, Sequence = sequence, SenderName = name };
            for (var i = 0; i < GripRelayPacket.SensorCount; i++)
            {
                packet.Sensors[i] = new GripRelaySensorSample { Raw = value + i * 0.1f, Received = value, Connected = connected };
            }

            return packet;
        }

        [Test]
        public void Accepted_ValuesAreReadable_UntilTimeout()
        {
            Assert.IsFalse(_state.IsSensorConnected(0));
            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(5, 1, 0.4f)));

            Assert.IsTrue(_state.TryGetSensor(2, out var raw, out var received));
            Assert.AreEqual(0.6f, raw, 1e-5f);
            Assert.AreEqual(0.4f, received, 1e-5f);

            _now += Timeout; // ちょうど時間切れの秒数まではまだ届いている扱い。
            Assert.IsTrue(_state.IsSensorConnected(2));
            _now += 0.01;
            Assert.IsFalse(_state.TryGetSensor(2, out raw, out _));
            Assert.AreEqual(0f, raw);
        }

        [Test]
        public void OwnPacket_IsIgnored()
        {
            Assert.AreEqual(GripRelayAcceptResult.OwnPacket, _state.Accept(Packet(Own, 1, 0.9f)));
            Assert.IsFalse(_state.IsSensorConnected(0));
        }

        [Test]
        public void DuplicateAndOlderSequences_AreIgnored()
        {
            _state.Accept(Packet(5, 10, 0.5f));
            Assert.AreEqual(GripRelayAcceptResult.OldOrDuplicate, _state.Accept(Packet(5, 10, 0.9f)));
            Assert.AreEqual(GripRelayAcceptResult.OldOrDuplicate, _state.Accept(Packet(5, 9, 0.9f)));
            _state.TryGetSensor(0, out var raw, out _);
            Assert.AreEqual(0.5f, raw, 1e-5f);

            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(5, 11, 0.7f)));
        }

        [Test]
        public void SequenceWrapAround_IsAccepted()
        {
            _state.Accept(Packet(5, uint.MaxValue, 0.1f));
            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(5, 0, 0.2f)));
            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(5, 1, 0.3f)));
        }

        [Test]
        public void SensorWithoutValue_IsNotConnected()
        {
            var packet = Packet(5, 1, 0.5f);
            packet.Sensors[3].Connected = false;
            _state.Accept(packet);

            Assert.IsTrue(_state.IsSensorConnected(0));
            Assert.IsFalse(_state.IsSensorConnected(3));
            Assert.IsFalse(_state.IsSensorConnected(4));
            Assert.IsFalse(_state.IsSensorConnected(-1));
        }

        [Test]
        public void SecondSender_IsIgnoredAndReported_WhileFirstIsAlive()
        {
            _state.Accept(Packet(5, 1, 0.2f, name: "PC1"));
            Assert.AreEqual(GripRelayAcceptResult.OtherSender, _state.Accept(Packet(6, 1, 0.9f, name: "PC2")));

            _state.TryGetSensor(0, out var raw, out _);
            Assert.AreEqual(0.2f, raw, 1e-5f);
            var snapshot = _state.GetSnapshot();
            Assert.AreEqual("PC1", snapshot.SenderName);
            Assert.AreEqual("PC2", snapshot.OtherSenderName);
        }

        [Test]
        public void WarningInfo_ReportsSecondSenderWhileItIsAlive()
        {
            _state.GetWarningInfo(out var sender, out var other, out var wrong);
            Assert.IsNull(sender);
            Assert.IsNull(other);
            Assert.AreEqual(0, wrong);

            _state.Accept(Packet(5, 1, 0.2f, name: "PC1"));
            _state.Accept(Packet(6, 1, 0.9f, name: "PC2"));
            _state.NoteWrongVersion();
            _state.GetWarningInfo(out sender, out other, out wrong);
            Assert.AreEqual("PC1", sender);
            Assert.AreEqual("PC2", other);
            Assert.AreEqual(1, wrong);

            // PC2 の放送が止まれば、知らせは消える（PC1 は届き続けている）。
            _now += Timeout * 0.8;
            _state.Accept(Packet(5, 2, 0.2f, name: "PC1"));
            _now += Timeout * 0.8;
            _state.GetWarningInfo(out _, out other, out _);
            Assert.IsNull(other);
        }

        [Test]
        public void SwitchesToSecondSender_WhenFirstStops()
        {
            _state.Accept(Packet(5, 1, 0.2f, name: "PC1"));
            _now += Timeout + 0.1;

            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(6, 1, 0.9f, name: "SPARE")));
            Assert.AreEqual("SPARE", _state.GetSnapshot().SenderName);
            _state.TryGetSensor(0, out var raw, out _);
            Assert.AreEqual(0.9f, raw, 1e-5f);

            // 前の放送は止まっているので、「2か所から来ている」とは出さない。
            Assert.IsNull(_state.GetSnapshot().OtherSenderName);
        }

        [Test]
        public void SwitchesToSenderWithValues_WhenCurrentHasNone()
        {
            // Arduino が抜けた PC1（値なし）と、Arduino を挿し替えた予備（値あり）が同時に放送している。
            _state.Accept(Packet(5, 1, 0f, connected: false, name: "PC1"));
            Assert.AreEqual(GripRelayAcceptResult.Accepted, _state.Accept(Packet(6, 1, 0.5f, name: "SPARE")));
            Assert.AreEqual("SPARE", _state.GetSnapshot().SenderName);

            // 戻らない（値のある方を使い続ける）。
            Assert.AreEqual(GripRelayAcceptResult.OtherSender, _state.Accept(Packet(5, 2, 0f, connected: false, name: "PC1")));
            Assert.AreEqual("SPARE", _state.GetSnapshot().SenderName);
            Assert.AreEqual("PC1", _state.GetSnapshot().OtherSenderName);
        }

        [Test]
        public void Snapshot_DescribesState()
        {
            var empty = _state.GetSnapshot();
            Assert.IsFalse(empty.HasSender);
            Assert.IsFalse(empty.IsFresh);
            Assert.IsTrue(double.IsPositiveInfinity(empty.SecondsSinceLastPacket));

            _state.Accept(Packet(5, 1, 0.3f));
            _state.NoteWrongVersion();
            _now += 0.2;
            var snapshot = _state.GetSnapshot();
            Assert.IsTrue(snapshot.HasSender);
            Assert.IsTrue(snapshot.IsFresh);
            Assert.AreEqual(0.2, snapshot.SecondsSinceLastPacket, 1e-9);
            Assert.AreEqual(1, snapshot.PacketsAccepted);
            Assert.AreEqual(1, snapshot.WrongVersionPackets);
            Assert.AreEqual(GripRelayPacket.SensorCount, snapshot.Sensors.Length);

            // 取り出した値を書き換えても、中身は変わらない。
            snapshot.Sensors[0].Raw = 99f;
            _state.TryGetSensor(0, out var raw, out _);
            Assert.AreEqual(0.3f, raw, 1e-5f);
        }
    }
}
