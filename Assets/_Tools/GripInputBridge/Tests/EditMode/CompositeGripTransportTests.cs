using System;
using MS2026.GripInputBridge.Transports;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class CompositeGripTransportTests
    {
        private sealed class FakeGripTransport : IGripTransport
        {
            public readonly float[] Values = new float[GripInputBridgeConstants.PlayerCount];
            public readonly bool[] Connected = new bool[GripInputBridgeConstants.PlayerCount];
            public int ReadCount;

            public event Action<int> OnConnected;
            public event Action<int> OnDisconnected;

            public bool IsConnected(int playerIndex) => Connected[playerIndex];

            public float GetRawValue(int playerIndex)
            {
                ReadCount++;
                return Values[playerIndex];
            }

            public void RaiseConnected(int playerIndex) => OnConnected?.Invoke(playerIndex);

            public void RaiseDisconnected(int playerIndex) => OnDisconnected?.Invoke(playerIndex);
        }

        private FakeGripTransport _device;
        private FakeGripTransport _keyboard;
        private CompositeGripTransport _composite;

        [SetUp]
        public void SetUp()
        {
            _device = new FakeGripTransport();
            _keyboard = new FakeGripTransport();
            for (var i = 0; i < GripInputBridgeConstants.PlayerCount; i++)
            {
                _keyboard.Connected[i] = true;
            }

            _composite = new CompositeGripTransport(_device, _keyboard);
        }

        [Test]
        public void DeviceDisconnected_UsesKeyboard()
        {
            _device.Values[0] = 0.9f;
            _keyboard.Values[0] = 0.3f;

            Assert.AreEqual(0.3f, _composite.GetRawValue(0), 1e-5f);
            Assert.IsTrue(_composite.IsConnected(0));
            Assert.IsFalse(_composite.IsDeviceConnected(0));
        }

        [Test]
        public void DeviceConnected_UsesLargerValue()
        {
            _device.Connected[0] = true;
            _device.Values[0] = 0.6f;
            _keyboard.Values[0] = 0.2f;
            Assert.AreEqual(0.6f, _composite.GetRawValue(0), 1e-5f);

            // 実機を挿したままでも、キーボードを強く押せばそちらが使われる(デバッグ用)。
            _keyboard.Values[0] = 0.8f;
            Assert.AreEqual(0.8f, _composite.GetRawValue(0), 1e-5f);
        }

        [Test]
        public void KeyboardIsReadEveryTime_EvenWhenDeviceConnected()
        {
            // キーボード側は読まれた時に押下中の値を進めるため、実機が繋がっていても毎回読む必要がある。
            _device.Connected[1] = true;

            _composite.GetRawValue(1);
            _composite.GetRawValue(1);

            Assert.AreEqual(2, _keyboard.ReadCount);
        }

        [Test]
        public void BothDisconnected_ReturnsZero()
        {
            _keyboard.Connected[2] = false;
            _keyboard.Values[2] = 1f;

            Assert.AreEqual(0f, _composite.GetRawValue(2));
            Assert.IsFalse(_composite.IsConnected(2));
        }

        [Test]
        public void DeviceEvents_AreForwarded()
        {
            var connected = -1;
            var disconnected = -1;
            _composite.OnConnected += p => connected = p;
            _composite.OnDisconnected += p => disconnected = p;

            _device.RaiseConnected(3);
            _device.RaiseDisconnected(2);

            Assert.AreEqual(3, connected);
            Assert.AreEqual(2, disconnected);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new CompositeGripTransport(null, _keyboard));
            Assert.Throws<ArgumentNullException>(() => new CompositeGripTransport(_device, null));
        }
    }
}
