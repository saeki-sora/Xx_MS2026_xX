using System;
using MS2026.GripInputBridge.Relay;
using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class RelayedSensorTransportTests
    {
        private sealed class FakeDevice : IGripTransport
        {
            public readonly float[] Values = new float[4];
            public readonly bool[] Connected = new bool[4];

#pragma warning disable CS0067
            public event Action<int> OnConnected;
            public event Action<int> OnDisconnected;
#pragma warning restore CS0067

            public bool IsConnected(int playerIndex) => Connected[playerIndex];

            public float GetRawValue(int playerIndex) => Values[playerIndex];
        }

        private double _now;
        private GripRelayReceiverState _relay;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _relay = new GripRelayReceiverState(1, 0.5, () => _now);
        }

        private void Broadcast(params float[] values)
        {
            var packet = new GripRelayPacket { SenderId = 9, Sequence = (uint)(_now * 1000) + 1 };
            for (var i = 0; i < values.Length; i++)
            {
                packet.Sensors[i] = new GripRelaySensorSample { Raw = values[i], Received = values[i], Connected = true };
            }

            _relay.Accept(packet);
        }

        [Test]
        public void ChosenSensor_BecomesPlayerOne()
        {
            var transport = new RelayedSensorTransport(2, _relay);
            Broadcast(0.1f, 0.2f, 0.7f, 0.4f);

            Assert.IsTrue(transport.IsConnected(0));
            Assert.AreEqual(0.7f, transport.GetRawValue(0), 1e-5f);
            Assert.AreEqual(GripRelayInputSource.Relay, transport.CurrentSource);

            // ほかのプレイヤーは常に「つながっていない」（ほかの人の握力で、このPCの画面を操作しない）。
            for (var p = 1; p < 4; p++)
            {
                Assert.IsFalse(transport.IsConnected(p));
                Assert.AreEqual(0f, transport.GetRawValue(p));
            }
        }

        [Test]
        public void LocalDevice_IsPreferred_ThenFallsBackToRelay()
        {
            var device = new FakeDevice();
            device.Connected[1] = true;
            device.Values[1] = 0.9f;
            var transport = new RelayedSensorTransport(1, _relay) { LocalDevice = device };
            Broadcast(0f, 0.3f, 0f, 0f);

            Assert.AreEqual(GripRelayInputSource.LocalDevice, transport.CurrentSource);
            Assert.AreEqual(0.9f, transport.GetRawValue(0), 1e-5f);

            device.Connected[1] = false; // このPCの Arduino が抜けた。
            Assert.AreEqual(GripRelayInputSource.Relay, transport.CurrentSource);
            Assert.AreEqual(0.3f, transport.GetRawValue(0), 1e-5f);
        }

        [Test]
        public void NothingArrives_IsDisconnected()
        {
            var transport = new RelayedSensorTransport(0, _relay);
            Assert.IsFalse(transport.IsConnected(0));
            Assert.AreEqual(GripRelayInputSource.None, transport.CurrentSource);

            Broadcast(0.5f);
            _now = 0.6; // 放送が止まった。
            Assert.IsFalse(transport.IsConnected(0));
            Assert.AreEqual(0f, transport.GetRawValue(0));

            Assert.AreEqual(0f, new RelayedSensorTransport(0, null).GetRawValue(0));
        }

        [Test]
        public void WithKeyboard_InComposite_UsesLargerValue()
        {
            var keyboard = new FakeDevice();
            for (var i = 0; i < 4; i++)
            {
                keyboard.Connected[i] = true;
            }

            var composite = new CompositeGripTransport(new RelayedSensorTransport(3, _relay), keyboard, 1);
            Broadcast(0f, 0f, 0f, 0.6f);
            keyboard.Values[0] = 0.2f;
            keyboard.Values[1] = 0.8f;

            Assert.AreEqual(0.6f, composite.GetRawValue(0), 1e-5f);
            Assert.IsTrue(composite.IsDeviceConnected(0));

            // P2 はキーボード（W）だけ。
            Assert.AreEqual(0.8f, composite.GetRawValue(1), 1e-5f);
        }

        [Test]
        public void InvalidSensor_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelayedSensorTransport(4, _relay));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelayedSensorTransport(-1, _relay));
        }
    }
}
