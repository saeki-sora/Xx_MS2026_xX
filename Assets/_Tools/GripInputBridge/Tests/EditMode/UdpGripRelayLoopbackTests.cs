using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using MS2026.GripInputBridge.Relay;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    /// <summary>
    /// 本物の UDP で、放送係から4人分の受け取り係へ値が届くかを確かめる（このPCの中だけ: 127.0.0.1。ファイアウォールの確認は出ない）。
    /// </summary>
    [TestFixture]
    public class UdpGripRelayLoopbackTests
    {
        private sealed class FakeSource : IGripRelaySampleSource
        {
            public volatile bool Send = true;
            public volatile float Value;

            public bool TryFill(GripRelayPacket packet)
            {
                if (!Send)
                {
                    return false;
                }

                for (var i = 0; i < GripRelayPacket.SensorCount; i++)
                {
                    packet.Sensors[i] = new GripRelaySensorSample { Raw = Value + i * 0.1f, Received = Value, Connected = true };
                }

                return true;
            }
        }

        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private int _basePort;

        [SetUp]
        public void SetUp()
        {
            // ほかの物とぶつかりにくい番号を毎回選ぶ。
            _basePort = 52000 + new Random().Next(0, 3000) * 4; // 52000〜63999（4つ分の番号が 65535 を超えない）
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();
        }

        private static IReadOnlyList<IPEndPoint> LoopbackTargets(int basePort)
        {
            var targets = new List<IPEndPoint>();
            for (var sensor = 1; sensor <= GripRelayPacket.SensorCount; sensor++)
            {
                targets.Add(new IPEndPoint(IPAddress.Loopback, GripRelayOptions.PortForSensor(basePort, sensor)));
            }

            return targets;
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 3000)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(10);
            }

            return condition();
        }

        private static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        [Test]
        public void FourReceivers_EachGetTheBroadcast()
        {
            var states = new GripRelayReceiverState[GripRelayPacket.SensorCount];
            for (var i = 0; i < states.Length; i++)
            {
                states[i] = new GripRelayReceiverState(0, 0.5, Now);
                var receiver = Track(new UdpGripRelayReceiver(GripRelayOptions.PortForSensor(_basePort, i + 1), states[i], null, IPAddress.Loopback));
                Assert.IsTrue(receiver.IsListening, receiver.ListenError);
            }

            var source = new FakeSource { Value = 0.3f };
            var sender = Track(new UdpGripRelaySender(source, 42, "TEST-PC", _basePort, 60f, null, LoopbackTargets));

            for (var i = 0; i < states.Length; i++)
            {
                var index = i;
                Assert.IsTrue(WaitUntil(() => states[index].IsSensorConnected(index)), $"センサー{index + 1}の受け取り係に届かない");
                states[i].TryGetSensor(i, out var raw, out _);
                Assert.AreEqual(0.3f + i * 0.1f, raw, 1e-3f);
                Assert.AreEqual("TEST-PC", states[i].GetSnapshot().SenderName);
            }

            // 値が変わったら届く。
            source.Value = 0.6f;
            Assert.IsTrue(WaitUntil(() => states[2].TryGetSensor(2, out var raw, out _) && Math.Abs(raw - 0.8f) < 1e-3f));
            Assert.IsTrue(sender.IsSending);
        }

        // 展示と同じ組み立て: Arduino（4人分）→ 放送 → 4台の「PC」がそれぞれ自分の番号だけを P1 にする（キーボードと合成）。
        private sealed class ArduinoSource : IGripRelaySampleSource
        {
            public readonly GripRelaySampleBuilderTests.FakeArduino Arduino = new GripRelaySampleBuilderTests.FakeArduino();

            public bool TryFill(GripRelayPacket packet) => GripRelaySampleBuilder.Fill(packet, Arduino, new float[4], false);
        }

        private sealed class NoKeyboard : IGripTransport
        {
#pragma warning disable CS0067
            public event Action<int> OnConnected;
            public event Action<int> OnDisconnected;
#pragma warning restore CS0067

            public bool IsConnected(int playerIndex) => true;

            public float GetRawValue(int playerIndex) => 0f;
        }

        [Test]
        public void EndToEnd_EachPcGetsOnlyItsOwnSensorAsPlayerOne()
        {
            var source = new ArduinoSource();
            var pcs = new MS2026.GripInputBridge.Transports.CompositeGripTransport[GripRelayPacket.SensorCount];
            for (var sensor = 1; sensor <= pcs.Length; sensor++)
            {
                var state = new GripRelayReceiverState(0, 0.5, Now);
                var receiver = Track(new UdpGripRelayReceiver(GripRelayOptions.PortForSensor(_basePort, sensor), state, null, IPAddress.Loopback));
                Assert.IsTrue(receiver.IsListening, receiver.ListenError);
                pcs[sensor - 1] = new MS2026.GripInputBridge.Transports.CompositeGripTransport(new RelayedSensorTransport(sensor - 1, state), new NoKeyboard(), 1);
            }

            Track(new UdpGripRelaySender(source, 7, "ARDUINO-PC", _basePort, 60f, null, LoopbackTargets));

            // 3番の人だけが握る。
            for (var i = 0; i < 4; i++)
            {
                source.Arduino.Connected[i] = true;
            }

            source.Arduino.Values[2] = 0.8f;
            Assert.IsTrue(WaitUntil(() => Math.Abs(pcs[2].GetRawValue(0) - 0.8f) < 1e-3f), "センサー3の PC に届かない");
            for (var pc = 0; pc < pcs.Length; pc++)
            {
                Assert.IsTrue(pcs[pc].IsDeviceConnected(0), $"PC{pc + 1}");
                if (pc != 2)
                {
                    Assert.AreEqual(0f, pcs[pc].GetRawValue(0), 1e-3f, $"PC{pc + 1} が3番の人の握力で動いた");
                }

                for (var other = 1; other < 4; other++)
                {
                    Assert.AreEqual(0f, pcs[pc].GetRawValue(other), $"PC{pc + 1} の P{other + 1}");
                }
            }

            // 1番の人に替わる。
            source.Arduino.Values[2] = 0f;
            source.Arduino.Values[0] = 0.5f;
            Assert.IsTrue(WaitUntil(() => Math.Abs(pcs[0].GetRawValue(0) - 0.5f) < 1e-3f && pcs[2].GetRawValue(0) < 1e-3f));

            // Arduino の4番の線が外れた → 4番の PC だけ「届いていない」（キーボードだけで動く）。
            source.Arduino.Connected[3] = false;
            Assert.IsTrue(WaitUntil(() => !pcs[3].IsDeviceConnected(0)));
            Assert.IsTrue(pcs[0].IsDeviceConnected(0));
        }

        [Test]
        public void SendRate_IsAboutRequested()
        {
            var state = new GripRelayReceiverState(0, 0.5, Now);
            Track(new UdpGripRelayReceiver(_basePort, state, null, IPAddress.Loopback));
            var sender = Track(new UdpGripRelaySender(new FakeSource(), 42, "TEST-PC", _basePort, 60f, null, LoopbackTargets));

            Thread.Sleep(1000);
            var sent = sender.PacketsSent;
            var accepted = state.GetSnapshot().PacketsAccepted;

            // Windows の Sleep の粗さがあっても、1秒にだいたい 60 回（少なくとも 45 回）送れて、ほぼ全部届く。
            Assert.That(sent, Is.InRange(45, 75), $"sent={sent}");
            Assert.That(accepted, Is.GreaterThanOrEqualTo(sent - 3), $"accepted={accepted}");
        }

        [Test]
        public void StoppingTheBroadcast_TimesOut()
        {
            var state = new GripRelayReceiverState(0, 0.3, Now);
            Track(new UdpGripRelayReceiver(_basePort, state, null, IPAddress.Loopback));
            var source = new FakeSource();
            var sender = Track(new UdpGripRelaySender(source, 42, "TEST-PC", _basePort, 60f, null, LoopbackTargets));
            Assert.IsTrue(WaitUntil(() => state.IsSensorConnected(0)));

            source.Send = false; // Arduino が抜けた（放送をやめた）。
            Assert.IsTrue(WaitUntil(() => !state.IsSensorConnected(0), 2000));
            Assert.IsTrue(WaitUntil(() => !sender.IsSending, 1000));
        }

        [Test]
        public void OwnBroadcast_IsNotUsed()
        {
            var state = new GripRelayReceiverState(42, 0.5, Now);
            var receiver = Track(new UdpGripRelayReceiver(_basePort, state, null, IPAddress.Loopback));
            Assert.IsTrue(receiver.IsListening, receiver.ListenError);
            Track(new UdpGripRelaySender(new FakeSource(), 42, "SELF", _basePort, 60f, null, LoopbackTargets));

            Assert.IsTrue(WaitUntil(() => receiver.PacketsReceived > 5));
            Assert.IsFalse(state.GetSnapshot().HasSender);
        }

        [Test]
        public void SamePortTwice_SecondReportsError()
        {
            var first = Track(new UdpGripRelayReceiver(_basePort, new GripRelayReceiverState(0, 0.5, Now), null, IPAddress.Loopback));
            var second = Track(new UdpGripRelayReceiver(_basePort, new GripRelayReceiverState(0, 0.5, Now), null, IPAddress.Loopback));

            Assert.IsTrue(first.IsListening);
            Assert.IsFalse(second.IsListening);
            StringAssert.Contains(_basePort.ToString(), second.ListenError);
        }

        [Test]
        public void Dispose_StopsQuickly()
        {
            var receiver = new UdpGripRelayReceiver(_basePort, new GripRelayReceiverState(0, 0.5, Now), null, IPAddress.Loopback);
            var sender = new UdpGripRelaySender(new FakeSource(), 42, "TEST-PC", _basePort, 60f, null, LoopbackTargets);
            Thread.Sleep(100);

            var clock = Stopwatch.StartNew();
            sender.Dispose();
            receiver.Dispose();
            Assert.Less(clock.ElapsedMilliseconds, 1200);

            // 片付けた後は、同じ番号をすぐ使い直せる（ゲームを開き直したとき）。
            var again = Track(new UdpGripRelayReceiver(_basePort, new GripRelayReceiverState(0, 0.5, Now), null, IPAddress.Loopback));
            Assert.IsTrue(again.IsListening, again.ListenError);
        }
    }
}
