using System;
using System.Collections.Generic;
using MS2026.GripInputBridge.Provider;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripInputProviderTests
    {
        private sealed class FakeGripTransport : IGripTransport
        {
            public readonly float[] Values = new float[GripInputProvider.PlayerCount];
            public readonly bool[] Connected = { true, true, true, true };

            public event Action<int> OnConnected;
            public event Action<int> OnDisconnected;

            public bool IsConnected(int playerIndex) => Connected[playerIndex];

            public float GetRawValue(int playerIndex) => Values[playerIndex];
        }

        [Test]
        public void GetGripValue_ReturnsTransportRawValue_InPhase1()
        {
            var transport = new FakeGripTransport();
            transport.Values[0] = 0.73f;
            var provider = new GripInputProvider(transport);

            Assert.AreEqual(0.73f, provider.GetGripValue(0), 1e-5f);
        }

        [Test]
        public void GetGripValue_InvalidPlayerIndex_ReturnsZero_WithoutThrowing()
        {
            var provider = new GripInputProvider(new FakeGripTransport());

            Assert.AreEqual(0f, provider.GetGripValue(-1));
            Assert.AreEqual(0f, provider.GetGripValue(99));
        }

        [Test]
        public void IsGripping_BelowDeadZone_IsFalse()
        {
            var transport = new FakeGripTransport();
            transport.Values[0] = 0.01f;
            var provider = new GripInputProvider(transport);

            Assert.IsFalse(provider.IsGripping(0));
        }

        [Test]
        public void IsGripping_AboveDeadZone_IsTrue()
        {
            var transport = new FakeGripTransport();
            transport.Values[0] = 0.5f;
            var provider = new GripInputProvider(transport);

            Assert.IsTrue(provider.IsGripping(0));
        }

        [Test]
        public void GetStatus_ReflectsTransportConnection()
        {
            var transport = new FakeGripTransport();
            transport.Connected[1] = false;
            var provider = new GripInputProvider(transport);

            Assert.AreEqual(GripDeviceStatus.Connected, provider.GetStatus(0));
            Assert.AreEqual(GripDeviceStatus.Disconnected, provider.GetStatus(1));
        }

        [Test]
        public void Update_FiresOnGripStarted_WhenCrossingThresholdUpward()
        {
            var transport = new FakeGripTransport();
            var provider = new GripInputProvider(transport);
            var startedPlayers = new List<int>();
            provider.OnGripStarted += startedPlayers.Add;

            provider.Update(); // 初期状態(0)を確定させる

            transport.Values[2] = 0.5f;
            provider.Update();

            CollectionAssert.AreEqual(new[] { 2 }, startedPlayers);
        }

        [Test]
        public void Update_FiresOnGripReleased_WhenCrossingThresholdDownward()
        {
            var transport = new FakeGripTransport();
            transport.Values[3] = 0.5f;
            var provider = new GripInputProvider(transport);
            var releasedPlayers = new List<int>();
            provider.OnGripReleased += releasedPlayers.Add;

            provider.Update(); // 握っている状態を確定させる

            transport.Values[3] = 0f;
            provider.Update();

            CollectionAssert.AreEqual(new[] { 3 }, releasedPlayers);
        }

        [Test]
        public void Update_DoesNotRefireEvents_WhileValueStaysAboveThreshold()
        {
            var transport = new FakeGripTransport();
            var provider = new GripInputProvider(transport);
            var startedCount = 0;
            provider.OnGripStarted += _ => startedCount++;

            provider.Update();
            transport.Values[0] = 0.5f;
            provider.Update();
            transport.Values[0] = 0.9f;
            provider.Update();

            Assert.AreEqual(1, startedCount);
        }

        [Test]
        public void SetTransport_Null_ThrowsArgumentNullException()
        {
            var provider = new GripInputProvider(new FakeGripTransport());

            Assert.Throws<ArgumentNullException>(() => provider.SetTransport(null));
        }
    }
}
