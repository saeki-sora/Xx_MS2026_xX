using System;
using System.Text;
using MS2026.GripInputBridge.Relay;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripRelayCodecTests
    {
        private static GripRelayPacket MakePacket()
        {
            var packet = new GripRelayPacket
            {
                SenderId = 0xA1B2C3D4,
                Sequence = 123456789,
                ForcedTestSender = true,
                SenderName = "EXHIBIT-PC1"
            };
            packet.Sensors[0] = new GripRelaySensorSample { Raw = 0f, Received = 0.5f, Connected = true };
            packet.Sensors[1] = new GripRelaySensorSample { Raw = 1f, Received = 1f, Connected = true };
            packet.Sensors[2] = new GripRelaySensorSample { Raw = 0.25f, Received = 0.75f, Connected = false };
            packet.Sensors[3] = new GripRelaySensorSample { Raw = 0.333f, Received = 0.001f, Connected = true };
            return packet;
        }

        [Test]
        public void WriteThenRead_RoundTrips()
        {
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var length = GripRelayCodec.Write(MakePacket(), buffer);

            var read = new GripRelayPacket();
            Assert.AreEqual(GripRelayReadResult.Ok, GripRelayCodec.TryRead(buffer, length, read));
            Assert.AreEqual(0xA1B2C3D4u, read.SenderId);
            Assert.AreEqual(123456789u, read.Sequence);
            Assert.IsTrue(read.ForcedTestSender);
            Assert.AreEqual("EXHIBIT-PC1", read.SenderName);

            var expected = MakePacket();
            for (var i = 0; i < GripRelayPacket.SensorCount; i++)
            {
                // 0-65535 にまとめるので、誤差は 1/65535 以内。
                Assert.AreEqual(expected.Sensors[i].Raw, read.Sensors[i].Raw, 1f / 65535f, $"raw {i}");
                Assert.AreEqual(expected.Sensors[i].Received, read.Sensors[i].Received, 1f / 65535f, $"received {i}");
                Assert.AreEqual(expected.Sensors[i].Connected, read.Sensors[i].Connected, $"connected {i}");
            }
        }

        [Test]
        public void Write_ClampsOutOfRangeAndNaN()
        {
            var packet = MakePacket();
            packet.Sensors[0].Raw = -0.5f;
            packet.Sensors[1].Raw = 1.7f;
            packet.Sensors[2].Raw = float.NaN;
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var read = new GripRelayPacket();

            Assert.AreEqual(GripRelayReadResult.Ok, GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read));
            Assert.AreEqual(0f, read.Sensors[0].Raw);
            Assert.AreEqual(1f, read.Sensors[1].Raw);
            Assert.AreEqual(0f, read.Sensors[2].Raw);
        }

        [Test]
        public void LongJapaneseName_IsTruncatedWithinLimit()
        {
            var packet = MakePacket();
            packet.SenderName = new string('展', 30) + "😀";
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var length = GripRelayCodec.Write(packet, buffer);
            var read = new GripRelayPacket();

            Assert.AreEqual(GripRelayReadResult.Ok, GripRelayCodec.TryRead(buffer, length, read));
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(read.SenderName), GripRelayCodec.MaxNameBytes);
            Assert.IsTrue(packet.SenderName.StartsWith(read.SenderName, StringComparison.Ordinal));
            Assert.AreEqual(13, read.SenderName.Length); // 1文字3バイト × 13 = 39 バイト。
        }

        [Test]
        public void TruncateName_DoesNotSplitSurrogatePair()
        {
            var name = new string('a', 38) + "😀"; // 38 + 4 = 42 バイト。
            Assert.AreEqual(new string('a', 38), GripRelayCodec.TruncateName(name));
            Assert.AreEqual(string.Empty, GripRelayCodec.TruncateName(null));
        }

        [Test]
        public void OtherData_IsNotRelayPacket()
        {
            var read = new GripRelayPacket();
            var lobby = Encoding.UTF8.GetBytes("MS2026LOBBY|1|?");
            Assert.AreEqual(GripRelayReadResult.NotRelayPacket, GripRelayCodec.TryRead(lobby, lobby.Length, read));
            Assert.AreEqual(GripRelayReadResult.NotRelayPacket, GripRelayCodec.TryRead(new byte[3], 3, read));
            Assert.AreEqual(GripRelayReadResult.NotRelayPacket, GripRelayCodec.TryRead(null, 0, read));
        }

        [Test]
        public void DifferentVersion_IsReported()
        {
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var length = GripRelayCodec.Write(MakePacket(), buffer);
            buffer[4] = GripRelayCodec.Version + 1;

            Assert.AreEqual(GripRelayReadResult.WrongVersion, GripRelayCodec.TryRead(buffer, length, new GripRelayPacket()));
        }

        [Test]
        public void TruncatedOrPaddedPacket_IsMalformed_AndLeavesTargetUntouched()
        {
            var buffer = new byte[GripRelayCodec.MaxPacketBytes + 8];
            var length = GripRelayCodec.Write(MakePacket(), buffer);
            var read = new GripRelayPacket { SenderId = 77 };

            Assert.AreEqual(GripRelayReadResult.Malformed, GripRelayCodec.TryRead(buffer, length - 1, read));
            Assert.AreEqual(GripRelayReadResult.Malformed, GripRelayCodec.TryRead(buffer, length + 1, read));
            Assert.AreEqual(GripRelayReadResult.Malformed, GripRelayCodec.TryRead(buffer, 10, read));
            Assert.AreEqual(77u, read.SenderId);
        }

        [Test]
        public void ReadingIntoSamePacket_ReusesNameUntilItChanges()
        {
            var buffer = new byte[GripRelayCodec.MaxPacketBytes];
            var read = new GripRelayPacket();
            var packet = MakePacket();

            GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read);
            var first = read.SenderName;
            packet.Sequence++;
            GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read);
            Assert.AreSame(first, read.SenderName); // 同じ名前なら文字列を作り直さない。

            packet.SenderName = "SPARE-PC";
            GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read);
            Assert.AreEqual("SPARE-PC", read.SenderName);

            packet.SenderName = "EXHIBIT-PC9"; // 長さが同じで中身が違う。
            GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read);
            Assert.AreEqual("EXHIBIT-PC9", read.SenderName);

            packet.SenderName = string.Empty;
            GripRelayCodec.TryRead(buffer, GripRelayCodec.Write(packet, buffer), read);
            Assert.AreEqual(string.Empty, read.SenderName);
        }

        [Test]
        public void Write_RejectsSmallBuffer()
        {
            Assert.Throws<ArgumentException>(() => GripRelayCodec.Write(MakePacket(), new byte[10]));
        }
    }
}
