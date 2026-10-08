using System.Net;
using MS2026.UI.Game;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class LobbyTests
    {
        [Test]
        public void Roster_CanStartOnlyWhenEveryoneIsReady()
        {
            var roster = new LobbyRoster();
            roster.Join(0, 0, "たろう", true);
            roster.Join(2, 5, "はなこ", false);

            Assert.IsFalse(roster.CanStart(1, false), "まだ誰も準備OKでない");
            roster.SetReady(0, true);
            Assert.IsFalse(roster.CanStart(1, false), "1人だけ準備OK");
            Assert.IsTrue(roster.CanStart(1, true), "待たずに開始なら始められる");
            roster.SetReady(2, true);
            Assert.IsTrue(roster.CanStart(1, false));
            Assert.IsFalse(roster.CanStart(3, false), "人数が足りない");

            roster.SetPhase(LobbyPhase.Countdown, 3f);
            Assert.IsFalse(roster.CanStart(1, false), "カウントダウン中は始め直せない");
        }

        [Test]
        public void Roster_TracksSeatsByClient()
        {
            var roster = new LobbyRoster();
            roster.Join(1, 42, "  とても長い名前ですよーーー  ", false);

            Assert.AreEqual(1, roster.SeatOf(42));
            Assert.AreEqual(-1, roster.SeatOf(7));
            Assert.AreEqual(LobbyRoster.MaxNameLength, roster.seats[1].name.Length, "長い名前は切る");

            roster.SetName(1, "");
            Assert.AreEqual("P2", roster.seats[1].name, "空の名前は P2 にする");

            roster.Leave(1);
            Assert.AreEqual(0, roster.PresentCount);
            Assert.AreEqual(-1, roster.SeatOf(42));
        }

        [Test]
        public void Roster_VersionChangesOnlyWhenSomethingChanges()
        {
            var roster = new LobbyRoster();
            roster.Join(0, 0, "a", true);
            var version = roster.Version;

            roster.SetReady(0, false);
            roster.SetName(0, "a");
            Assert.AreEqual(version, roster.Version, "同じ値では変わらない");

            Assert.IsFalse(roster.SetPing(0, 5), "10ms 未満の揺れは配り直さない");
            Assert.IsTrue(roster.SetPing(0, 40));
            Assert.AreNotEqual(version, roster.Version);
        }

        [Test]
        public void Roster_ClearReadyAndCopy()
        {
            var roster = new LobbyRoster();
            roster.Join(0, 0, "a", true);
            roster.Join(3, 9, "d", false);
            roster.SetReady(0, true);
            roster.SetReady(3, true);
            roster.SetPhase(LobbyPhase.InGame);

            var copy = new LobbyRoster();
            copy.CopyFrom(roster);
            Assert.AreEqual(2, copy.ReadyCount);
            Assert.AreEqual(LobbyPhase.InGame, copy.phase);

            roster.ClearReady();
            Assert.AreEqual(0, roster.ReadyCount);
            Assert.AreEqual(2, copy.ReadyCount, "写しは元に引きずられない");
        }

        [Test]
        public void Discovery_ReplyRoundTrips()
        {
            var reply = new LanDiscovery.Reply
            {
                hostName = "たろう|の部屋",
                gamePort = 7777,
                takenMask = 0b0101,
                phase = LobbyPhase.InGame,
                players = 2,
                build = "0.1.0"
            };

            Assert.IsTrue(LanDiscovery.TryParseReply(LanDiscovery.FormatReply(reply), out var parsed));
            Assert.AreEqual("たろう/の部屋", parsed.hostName, "区切りの | は / に置き換える");
            Assert.AreEqual(7777, parsed.gamePort);
            Assert.AreEqual(0b0101, parsed.takenMask);
            Assert.AreEqual(LobbyPhase.InGame, parsed.phase);
            Assert.AreEqual(2, parsed.players);
            Assert.AreEqual("0.1.0", parsed.build);
        }

        [Test]
        public void Discovery_RejectsOtherTraffic()
        {
            Assert.IsTrue(LanDiscovery.IsRequest(LanDiscovery.FormatRequest()));
            Assert.IsFalse(LanDiscovery.IsRequest("hello"));
            Assert.IsFalse(LanDiscovery.TryParseReply("hello", out _));
            Assert.IsFalse(LanDiscovery.TryParseReply("MS2026LOBBY|99|!|a|7777|0|0|0|x", out _), "違う版の呼びかけは無視する");
            Assert.IsFalse(LanDiscovery.TryParseReply(LanDiscovery.FormatRequest(), out _), "呼びかけは返事ではない");
        }

        [Test]
        public void Discovery_FoundHostKnowsTakenSeats()
        {
            var host = new LanDiscovery.FoundHost { reply = new LanDiscovery.Reply { takenMask = 0b1001 } };
            Assert.IsTrue(host.IsTaken(0));
            Assert.IsFalse(host.IsTaken(1));
            Assert.IsTrue(host.IsTaken(3));
        }

        [Test]
        public void Discovery_BroadcastAddress()
        {
            var broadcast = LanDiscovery.Broadcast(IPAddress.Parse("192.168.1.12"), IPAddress.Parse("255.255.255.0"));
            Assert.AreEqual("192.168.1.255", broadcast.ToString());
            Assert.AreEqual("10.64.255.255", LanDiscovery.Broadcast(IPAddress.Parse("10.64.58.147"), IPAddress.Parse("255.255.0.0")).ToString());
            Assert.IsNull(LanDiscovery.Broadcast(IPAddress.Parse("192.168.1.12"), null));
        }
    }
}
