using System;
using MS2026.GripInputBridge.Relay;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripRelaySampleBuilderTests
    {
        internal sealed class FakeArduino : IGripRelayDevice
        {
            public readonly float[] Values = new float[4];
            public readonly float[] Received = new float[4];
            public readonly bool[] Connected = new bool[4];

#pragma warning disable CS0067
            public event Action<int> OnConnected;
            public event Action<int> OnDisconnected;
#pragma warning restore CS0067

            public bool IsConnected(int playerIndex) => Connected[playerIndex];

            public float GetRawValue(int playerIndex) => Values[playerIndex];

            public float GetReceivedValue(int playerIndex) => Received[playerIndex];
        }

        [Test]
        public void NoArduino_NotForced_DoesNotBroadcast()
        {
            Assert.IsFalse(GripRelaySampleBuilder.Fill(new GripRelayPacket(), null, new float[4], false));
        }

        [Test]
        public void Arduino_SendsItsValues_AndMarksMissingSensors()
        {
            var arduino = new FakeArduino();
            arduino.Connected[0] = arduino.Connected[1] = arduino.Connected[2] = true;
            arduino.Values[0] = 0.1f;
            arduino.Values[1] = 0.5f;
            arduino.Values[2] = 0.9f;
            arduino.Received[2] = 0.95f;
            var packet = new GripRelayPacket();

            Assert.IsTrue(GripRelaySampleBuilder.Fill(packet, arduino, new float[4], false));
            Assert.IsFalse(packet.ForcedTestSender);
            Assert.AreEqual(0.5f, packet.Sensors[1].Raw);
            Assert.AreEqual(0.9f, packet.Sensors[2].Raw);
            Assert.AreEqual(0.95f, packet.Sensors[2].Received);
            Assert.IsTrue(packet.Sensors[2].Connected);

            // センサー4の値は Arduino から届いていない。
            Assert.IsFalse(packet.Sensors[3].Connected);
            Assert.AreEqual(0f, packet.Sensors[3].Raw);
        }

        [Test]
        public void Arduino_KeyboardCanOverride_OnlyForSensorsWithValues()
        {
            var arduino = new FakeArduino();
            arduino.Connected[0] = true;
            arduino.Values[0] = 0.2f;
            var packet = new GripRelayPacket();

            GripRelaySampleBuilder.Fill(packet, arduino, new[] { 0.7f, 0f, 0f, 0.8f }, false);
            Assert.AreEqual(0.7f, packet.Sensors[0].Raw);
            Assert.IsFalse(packet.Sensors[3].Connected); // P のキーだけでは、値の無いセンサーを「届いている」にしない。
        }

        [Test]
        public void ForcedWithoutArduino_SendsKeyboardForAllFour()
        {
            var packet = new GripRelayPacket();
            Assert.IsTrue(GripRelaySampleBuilder.Fill(packet, null, new[] { 0f, 0.6f, 0f, 1f }, true));
            Assert.IsTrue(packet.ForcedTestSender);
            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(packet.Sensors[i].Connected);
            }

            Assert.AreEqual(0.6f, packet.Sensors[1].Raw);
            Assert.AreEqual(1f, packet.Sensors[3].Raw);
        }

        [Test]
        public void ForcedWithArduino_UsesArduino()
        {
            var arduino = new FakeArduino();
            var packet = new GripRelayPacket();
            Assert.IsTrue(GripRelaySampleBuilder.Fill(packet, arduino, new float[4], true));
            Assert.IsFalse(packet.ForcedTestSender);
            Assert.IsFalse(packet.Sensors[0].Connected);
        }

        [Test]
        public void MissingKeyboardArray_IsTreatedAsZero()
        {
            var packet = new GripRelayPacket();
            Assert.IsTrue(GripRelaySampleBuilder.Fill(packet, null, null, true));
            Assert.AreEqual(0f, packet.Sensors[0].Raw);
        }
    }
}
