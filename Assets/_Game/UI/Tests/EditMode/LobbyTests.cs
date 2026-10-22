using System.Collections.Generic;
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
        public void Roster_StageIndexIsCopiedAndCleared()
        {
            var roster = new LobbyRoster();
            Assert.AreEqual(-1, roster.stageIndex, "最初はステージ未定");
            var version = roster.Version;
            roster.SetStage(1);
            roster.SetPhase(LobbyPhase.StageRoulette);
            Assert.AreNotEqual(version, roster.Version, "ステージが変わったら配り直す");

            var copy = new LobbyRoster();
            copy.CopyFrom(roster);
            Assert.AreEqual(1, copy.stageIndex);
            Assert.AreEqual(LobbyPhase.StageRoulette, copy.phase);

            version = roster.Version;
            roster.SetStage(1);
            Assert.AreEqual(version, roster.Version, "同じステージなら変化なし");

            roster.Clear();
            Assert.AreEqual(-1, roster.stageIndex);
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
                build = "0.1.0",
                roomId = 123456789
            };

            Assert.IsTrue(LanDiscovery.TryParseReply(LanDiscovery.FormatReply(reply), out var parsed));
            Assert.AreEqual("たろう/の部屋", parsed.hostName, "区切りの | は / に置き換える");
            Assert.AreEqual(7777, parsed.gamePort);
            Assert.AreEqual(0b0101, parsed.takenMask);
            Assert.AreEqual(LobbyPhase.InGame, parsed.phase);
            Assert.AreEqual(2, parsed.players);
            Assert.AreEqual("0.1.0", parsed.build);
            Assert.AreEqual(123456789, parsed.roomId);
        }

        [Test]
        public void Discovery_ReadsOldReplyWithoutRoomId()
        {
            Assert.IsTrue(LanDiscovery.TryParseReply("MS2026LOBBY|1|!|a|7777|1|0|1|x", out var parsed), "部屋の番号が無い古い返事も読める");
            Assert.AreEqual(0, parsed.roomId);
            Assert.IsFalse(LanDiscovery.TryParseReply("MS2026LOBBY|1|!|a|7777|1|0|1|x|abc", out _), "番号が数字でなければ無視する");
        }

        // ───────── 展示用の自動接続 ─────────

        private static LanDiscovery.FoundHost Room(int roomId, int takenMask, LobbyPhase phase = LobbyPhase.CharacterSelect, bool sameBuild = true)
        {
            var players = 0;
            for (var i = 0; i < 4; i++)
            {
                players += (takenMask >> i) & 1;
            }

            return new LanDiscovery.FoundHost
            {
                address = $"192.168.0.{roomId % 250}",
                sameBuild = sameBuild,
                reply = new LanDiscovery.Reply { roomId = roomId, takenMask = takenMask, players = players, phase = phase }
            };
        }

        [Test]
        public void Auto_FirstFreeSeatIsArrivalOrder()
        {
            Assert.AreEqual(0, AutoMatchRules.FirstFreeSeat(0));
            Assert.AreEqual(1, AutoMatchRules.FirstFreeSeat(0b0001));
            Assert.AreEqual(2, AutoMatchRules.FirstFreeSeat(0b1011), "抜けた人の席から埋める");
            Assert.AreEqual(-1, AutoMatchRules.FirstFreeSeat(0b1111));
        }

        [Test]
        public void Auto_ChoosesTheBiggestJoinableRoom()
        {
            var hosts = new List<LanDiscovery.FoundHost> { Room(50, 0b0001), Room(70, 0b0011), Room(10, 0b0001) };
            Assert.AreEqual(AutoMatchChoice.Join, AutoMatchRules.Choose(hosts, 0, out var index));
            Assert.AreEqual(1, index, "人が多い部屋");

            hosts.RemoveAt(1);
            AutoMatchRules.Choose(hosts, 0, out index);
            Assert.AreEqual(1, index, "同じ人数なら部屋の番号が小さい方");

            AutoMatchRules.Choose(hosts, 10, out index);
            Assert.AreEqual(0, index, "自分が前に作った部屋は選ばない");
        }

        [Test]
        public void Auto_JoinsRoomsInAnyPlayablePhase()
        {
            foreach (var phase in new[] { LobbyPhase.Waiting, LobbyPhase.CharacterSelect, LobbyPhase.InGame, LobbyPhase.Countdown })
            {
                Assert.AreEqual(AutoMatchChoice.Join, AutoMatchRules.Choose(new[] { Room(5, 0b0001, phase) }, 0, out _), phase.ToString());
            }
        }

        [Test]
        public void Auto_WaitsInsteadOfHostingWhenARoomIsBusy()
        {
            Assert.AreEqual(AutoMatchChoice.Wait, AutoMatchRules.Choose(new[] { Room(5, 0b0001, LobbyPhase.Loading) }, 0, out _), "読み込み中は待つ");
            Assert.AreEqual(AutoMatchChoice.Wait, AutoMatchRules.Choose(new[] { Room(5, 0b0001, LobbyPhase.Returning) }, 0, out _));
            Assert.AreEqual(AutoMatchChoice.Wait, AutoMatchRules.Choose(new[] { Room(5, 0b1111) }, 0, out _), "満員なら待つ");
            Assert.AreEqual(AutoMatchChoice.None, AutoMatchRules.Choose(new[] { Room(5, 0b0001, sameBuild: false) }, 0, out _), "違うビルドの部屋は無いものとする");
            Assert.AreEqual(AutoMatchChoice.None, AutoMatchRules.Choose(new LanDiscovery.FoundHost[0], 0, out _));
        }

        [Test]
        public void Auto_OnlyOneOfTwoHostsYields()
        {
            var a = Room(100, 0b0001);
            var b = Room(200, 0b0001);
            Assert.IsFalse(AutoMatchRules.ShouldYield(1, 100, b), "番号が小さい部屋は残る");
            Assert.IsTrue(AutoMatchRules.ShouldYield(1, 200, a), "番号が大きい部屋が譲る");

            var bigger = Room(300, 0b0011);
            Assert.IsTrue(AutoMatchRules.ShouldYield(1, 100, bigger), "人が少ない部屋が譲る");
            Assert.IsFalse(AutoMatchRules.ShouldYield(2, 300, a));

            Assert.IsFalse(AutoMatchRules.ShouldYield(1, 100, Room(100, 0b0001)), "自分の部屋には譲らない");
            Assert.IsFalse(AutoMatchRules.ShouldYield(1, 200, Room(100, 0b1111)), "満員の部屋には譲らない");
            Assert.IsFalse(AutoMatchRules.ShouldYield(1, 200, Room(100, 0b0001, sameBuild: false)), "違うビルドには譲らない");
            Assert.IsFalse(AutoMatchRules.ShouldYield(1, 200, Room(0, 0b0001)), "番号の無い古いビルドには譲らない");
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
