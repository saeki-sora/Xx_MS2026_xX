using System.IO;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;

namespace MS2026.Spectator.Tests
{
    public class SpectatorTests
    {
        // ---- 起動オプション ----

        [Test]
        public void Config_DefaultsAndFlags()
        {
            var none = SpectatorConfig.Parse(new string[0]);
            Assert.IsFalse(none.Disabled);
            Assert.AreEqual(SpectatorConfig.DefaultPort, none.Port);

            var set = SpectatorConfig.Parse(new[] { "-grip-sensor", "2", "-spectator-port", "8081", "-no-spectator" });
            Assert.IsTrue(set.Disabled);
            Assert.AreEqual(8081, set.Port);

            Assert.AreEqual(SpectatorConfig.DefaultPort, SpectatorConfig.Parse(new[] { "-spectator-port", "abc" }).Port);
            Assert.AreEqual(SpectatorConfig.DefaultPort, SpectatorConfig.Parse(new[] { "-spectator-port", "70000" }).Port);
        }

        // ---- HTTP の読み書き ----

        [Test]
        public void Http_ParsesHeadQueryAndKeepAlive()
        {
            Assert.IsTrue(SpectatorHttp.TryParseHead("GET /api/state?id=abc12345&color=2 HTTP/1.1\r\nHost: x\r\nConnection: keep-alive", out var req, out var length));
            Assert.AreEqual("GET", req.Method);
            Assert.AreEqual("/api/state", req.Path);
            Assert.AreEqual("abc12345", req.Query["id"]);
            Assert.AreEqual("2", req.Query["color"]);
            Assert.IsTrue(req.KeepAlive);
            Assert.AreEqual(0, length);

            Assert.IsTrue(SpectatorHttp.TryParseHead("GET / HTTP/1.1\r\nConnection: close", out var close, out _));
            Assert.IsFalse(close.KeepAlive);
            Assert.IsTrue(SpectatorHttp.TryParseHead("GET / HTTP/1.0", out var old, out _));
            Assert.IsFalse(old.KeepAlive);
        }

        [Test]
        public void Http_RejectsBrokenOrHugeRequests()
        {
            Assert.IsFalse(SpectatorHttp.TryParseHead("hello", out _, out _));
            Assert.IsFalse(SpectatorHttp.TryParseHead("GET / SPDY/3", out _, out _));
            Assert.IsFalse(SpectatorHttp.TryParseHead("POST /api/send HTTP/1.1\r\nContent-Length: 999999", out _, out _));
            Assert.IsFalse(SpectatorHttp.TryParseHead("POST /api/send HTTP/1.1\r\nContent-Length: -1", out _, out _));
        }

        [Test]
        public void Http_FormDecodesPercentAndPlus()
        {
            var form = SpectatorHttp.ParseForm("a=1&b=%E8%8F%8C+%2323&c&=x");
            Assert.AreEqual("1", form["a"]);
            Assert.AreEqual("菌 #23", form["b"]);
            Assert.AreEqual("", form["c"]);
            Assert.IsFalse(form.ContainsKey(""));
        }

        [Test]
        public void Reader_ReadsTwoRequestsOnOneConnection()
        {
            var raw = "POST /api/send HTTP/1.1\r\nContent-Length: 11\r\n\r\nid=abcdefghGET /api/state?id=x HTTP/1.1\r\n\r\n";
            var reader = new SpectatorRequestReader(new MemoryStream(Encoding.ASCII.GetBytes(raw)));
            Assert.IsTrue(reader.TryRead(out var first));
            Assert.AreEqual("/api/send", first.Path);
            Assert.AreEqual("id=abcdefgh", first.Body);
            Assert.IsTrue(reader.TryRead(out var second));
            Assert.AreEqual("/api/state", second.Path);
            Assert.IsFalse(reader.TryRead(out _));
        }

        [Test]
        public void Response_HasLengthAndConnection()
        {
            var text = Encoding.UTF8.GetString(SpectatorHttp.BuildResponse(SpectatorResponse.Json("{\"a\":1}"), true));
            StringAssert.StartsWith("HTTP/1.1 200 OK\r\n", text);
            StringAssert.Contains("Content-Length: 7\r\n", text);
            StringAssert.Contains("Connection: keep-alive", text);
            StringAssert.Contains("Cache-Control: no-store", text);
            StringAssert.EndsWith("\r\n\r\n{\"a\":1}", text);
        }

        // ---- 名簿 ----

        [Test]
        public void Roster_GivesNumbersAndNames_AndRejectsBadIds()
        {
            var roster = new SpectatorRoster(10, 8);
            Assert.IsTrue(roster.TryGetOrAdd("aaaaaaaa", 1, 0, out var a));
            Assert.IsTrue(roster.TryGetOrAdd("bbbbbbbb", 9, 0, out var b));
            Assert.IsTrue(roster.TryGetOrAdd("aaaaaaaa", 3, 0, out var again));
            Assert.AreEqual(1, a.Number);
            Assert.AreEqual(2, b.Number);
            Assert.AreEqual(a.Name, again.Name);
            Assert.AreEqual(1, again.ColorIndex, "2回目の色は名簿の色のまま（色を変えるのは SetColor）");
            Assert.AreEqual(0, b.ColorIndex, "範囲外の色は 0 に");
            Assert.AreEqual(SpectatorNames.Make(1), a.Name);

            Assert.IsFalse(roster.TryGetOrAdd("short", 0, 0, out _));
            Assert.IsFalse(roster.TryGetOrAdd("has space here", 0, 0, out _));
            Assert.IsFalse(roster.TryGetOrAdd("<script>alert1", 0, 0, out _));
        }

        [Test]
        public void Roster_IsFullAtLimit()
        {
            var roster = new SpectatorRoster(2, 8);
            Assert.IsTrue(roster.TryGetOrAdd("aaaaaaaa", 0, 0, out _));
            Assert.IsTrue(roster.TryGetOrAdd("bbbbbbbb", 0, 0, out _));
            Assert.IsFalse(roster.TryGetOrAdd("cccccccc", 0, 0, out _));
            Assert.IsTrue(roster.TryGetOrAdd("aaaaaaaa", 0, 0, out _), "覚えている人はいっぱいでも入れる");
        }

        [Test]
        public void Roster_CooldownAndRefund()
        {
            var roster = new SpectatorRoster(10, 8);
            Assert.AreEqual(SpectatorSendResult.Unknown, roster.TrySend("aaaaaaaa", 0, out _));
            roster.TryGetOrAdd("aaaaaaaa", 0, 0, out _);
            Assert.AreEqual(SpectatorSendResult.Ok, roster.TrySend("aaaaaaaa", 10, out var sent));
            Assert.AreEqual(8, sent.CooldownRemaining, 1e-6);
            Assert.AreEqual(1, sent.Sent);
            Assert.AreEqual(SpectatorSendResult.CoolingDown, roster.TrySend("aaaaaaaa", 17.9, out var waiting));
            Assert.AreEqual(0.1, waiting.CooldownRemaining, 1e-6);
            Assert.AreEqual(SpectatorSendResult.Ok, roster.TrySend("aaaaaaaa", 18, out _));

            roster.Refund("aaaaaaaa");
            Assert.AreEqual(SpectatorSendResult.Ok, roster.TrySend("aaaaaaaa", 18.5, out var after));
            Assert.AreEqual(2, after.Sent, "取り消した分は数えない");
        }

        [Test]
        public void Names_AreStableAndDifferForNeighbours()
        {
            Assert.AreEqual(SpectatorNames.Make(23), SpectatorNames.Make(23));
            Assert.AreNotEqual(SpectatorNames.Make(1), SpectatorNames.Make(2));
            StringAssert.EndsWith("菌 #23", SpectatorNames.Make(23));
        }

        // ---- 受け答え ----

        private static SpectatorApi MakeApi(double[] clock, string phase = SpectatorPhase.Play)
        {
            var api = new SpectatorApi(new SpectatorRoster(10, 8), () => clock[0]);
            api.Board = new SpectatorBoard { Phase = phase, StageKey = "Stage1:3:1", ExitCount = 3, Allowance = 1000, SharedJson = "{\"phase\":\"" + phase + "\"}" };
            return api;
        }

        private static SpectatorRequest Post(string path, string body) => new SpectatorRequest { Method = "POST", Path = path, Body = body };

        private static string Text(SpectatorResponse r) => Encoding.UTF8.GetString(r.Body);

        [Test]
        public void Api_StateRegistersAndSendQueuesOrder()
        {
            var clock = new[] { 0.0 };
            var api = MakeApi(clock);
            var state = Text(api.Handle(new SpectatorRequest { Path = "/api/state", Query = SpectatorHttp.ParseForm("id=aaaaaaaa&color=2") }));
            StringAssert.Contains("\"me\":{\"name\":", state);
            StringAssert.Contains("\"color\":2", state);

            var ok = Text(api.Handle(Post("/api/send", "id=aaaaaaaa&exit=1&stage=Stage1%3A3%3A1&color=1")));
            StringAssert.Contains("\"ok\":true", ok);
            Assert.IsTrue(api.TryDequeue(out var order));
            Assert.AreEqual(1, order.ExitIndex);
            Assert.AreEqual(1, order.ColorIndex);
            Assert.AreEqual("Stage1:3:1", order.StageKey);
            Assert.IsFalse(api.TryDequeue(out _));

            var again = Text(api.Handle(Post("/api/send", "id=aaaaaaaa&exit=1&stage=Stage1%3A3%3A1")));
            StringAssert.Contains("\"reason\":\"cooldown\"", again);
            Assert.IsFalse(api.TryDequeue(out _));
        }

        [Test]
        public void Api_RejectsWhenNotPlayingOrStaleMapOrBadExit()
        {
            var clock = new[] { 0.0 };
            var waiting = MakeApi(clock, SpectatorPhase.Wait);
            waiting.Handle(new SpectatorRequest { Path = "/api/state", Query = SpectatorHttp.ParseForm("id=aaaaaaaa") });
            StringAssert.Contains("not_playing", Text(waiting.Handle(Post("/api/send", "id=aaaaaaaa&exit=0&stage=Stage1%3A3%3A1"))));

            var api = MakeApi(clock);
            api.Handle(new SpectatorRequest { Path = "/api/state", Query = SpectatorHttp.ParseForm("id=aaaaaaaa") });
            StringAssert.Contains("\"stage\"", Text(api.Handle(Post("/api/send", "id=aaaaaaaa&exit=0&stage=old"))));
            StringAssert.Contains("\"exit\"", Text(api.Handle(Post("/api/send", "id=aaaaaaaa&exit=3&stage=Stage1%3A3%3A1"))));
            StringAssert.Contains("\"unknown\"", Text(api.Handle(Post("/api/send", "id=zzzzzzzz&exit=0&stage=Stage1%3A3%3A1"))));
            Assert.IsFalse(api.TryDequeue(out _));
        }

        [Test]
        public void Api_ServesPageAndRefusesWrongMethods()
        {
            var api = MakeApi(new[] { 0.0 });
            api.SetPage(Encoding.UTF8.GetBytes("<p>hi</p>"));
            var page = api.Handle(new SpectatorRequest { Path = "/" });
            Assert.AreEqual(200, page.Status);
            StringAssert.StartsWith("text/html", page.ContentType);
            Assert.AreEqual(405, api.Handle(Post("/", "")).Status);
            Assert.AreEqual(405, api.Handle(new SpectatorRequest { Path = "/api/send" }).Status);
            Assert.AreEqual(404, api.Handle(new SpectatorRequest { Path = "/secret" }).Status);
        }

        [Test]
        public void Json_EscapesTextSoNamesCannotBreakThePage()
        {
            var text = new StringBuilder().Str("a\"b<c>\\").ToString();
            Assert.AreEqual("\"a\\\"b\\u003cc\\u003e\\\\\"", text);
            Assert.AreEqual("1.5", new StringBuilder().Num(1.5).ToString());
            Assert.AreEqual("0", new StringBuilder().Num(double.NaN).ToString());
        }

        [Test]
        public void Api_RefusesWhenMatchLimitIsUsedUp_CountingQueuedOrders()
        {
            var api = MakeApi(new[] { 0.0 });
            var board = api.Board;
            api.Board = new SpectatorBoard { Phase = board.Phase, StageKey = board.StageKey, ExitCount = 3, Allowance = SpectatorConfig.GermsPerSend + 5, SharedJson = "{}" };
            api.Handle(new SpectatorRequest { Path = "/api/state", Query = SpectatorHttp.ParseForm("id=aaaaaaaa") });
            api.Handle(new SpectatorRequest { Path = "/api/state", Query = SpectatorHttp.ParseForm("id=bbbbbbbb") });
            StringAssert.Contains("\"ok\":true", Text(api.Handle(Post("/api/send", "id=aaaaaaaa&exit=0&stage=Stage1%3A3%3A1"))));
            StringAssert.Contains("\"busy\"", Text(api.Handle(Post("/api/send", "id=bbbbbbbb&exit=0&stage=Stage1%3A3%3A1"))), "積まれている分も数える");
            Assert.IsTrue(api.TryDequeue(out _));
            StringAssert.Contains("\"ok\":true", Text(api.Handle(Post("/api/send", "id=bbbbbbbb&exit=0&stage=Stage1%3A3%3A1"))), "取り出した後は、また上限の中");
        }

        // ---- 上限とボス ----

        [Test]
        public void Budget_GrowsWithWaveAndShrinksWithSpectators()
        {
            var budget = new SpectatorBudget { Ratio = 0.15f, StartAllowance = 60 };
            Assert.AreEqual(60, budget.Allowance);
            budget.SetWaveSpawned(1000);
            Assert.AreEqual(210, budget.Allowance);
            budget.AddSpectatorSpawned(200);
            Assert.AreEqual(10, budget.Allowance);
            budget.AddSpectatorSpawned(100);
            Assert.AreEqual(0, budget.Allowance, "マイナスにはならない");
            budget.Reset();
            Assert.AreEqual(60, budget.Allowance);
        }

        [Test]
        public void Budget_BossEveryPoolMax_UpToMaxBosses()
        {
            var budget = new SpectatorBudget { PoolMax = 3, MaxBosses = 2 };
            Assert.IsFalse(budget.AddToPool());
            Assert.IsFalse(budget.AddToPool());
            Assert.IsTrue(budget.AddToPool());
            Assert.AreEqual(0, budget.Pool);
            Assert.AreEqual(1, budget.BossesLeft);

            budget.AddToPool();
            budget.AddToPool();
            Assert.IsTrue(budget.AddToPool());
            Assert.AreEqual(0, budget.BossesLeft);
            Assert.IsFalse(budget.AddToPool(), "出し切ったらたまらない");
            Assert.AreEqual(0, budget.Pool);

            budget.RefundBoss();
            Assert.AreEqual(1, budget.BossesLeft);
            Assert.IsTrue(budget.AddToPool(), "出せなかったボスは次の 1 回で出し直す");
        }

        [Test]
        public void Budget_DefaultBossFitsInOneMatch()
        {
            // 1 試合でウェーブが出す菌はおよそ 2000〜3000 体（Stage1・Stage2 の計画）。上限の中で送れる回数でボスが出ること。
            var budget = new SpectatorBudget();
            budget.SetWaveSpawned(2000);
            var sends = budget.Allowance / SpectatorConfig.GermsPerSend;
            Assert.GreaterOrEqual(sends, budget.PoolMax, "上限の中でボスまで届く");
        }

        // ---- 群れの帳簿 ----

        [Test]
        public void Book_TracksCenterSpreadAndAlive()
        {
            var book = new SpectatorGroupBook();
            var tag = book.NewTag();
            book.AddGroup(tag, 7, 2, false, 0);
            book.OnAssigned(tag, 100);
            book.OnAssigned(tag, 101);
            book.OnAssigned(999, 102); // 知らない印は無視

            book.BeginScan();
            book.Accumulate(100, 0f, 0f);
            book.Accumulate(101, 4f, 0f);
            book.Accumulate(55, 50f, 50f); // 観客の菌でない
            book.EndScan(1);

            var list = new System.Collections.Generic.List<SpectatorGroupState>();
            book.Snapshot(list, 10);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(7, list[0].Number);
            Assert.AreEqual(2, list[0].ColorIndex);
            Assert.AreEqual(2, list[0].Alive);
            Assert.AreEqual(2f, list[0].X, 1e-5f);
            Assert.AreEqual(3f, list[0].Radius, 1e-4f, "中心から 2 離れた 2 体 → 2 × 1.5");

            book.OnRemoved(100, SpectatorRemoval.Killed);
            book.OnRemoved(101, SpectatorRemoval.ReachedCore);
            book.EndScan(2);
            book.Snapshot(list, 10);
            Assert.AreEqual(0, list.Count, "全員いなくなった群れは消える");
            Assert.AreEqual(0, book.GroupCount);
            Assert.AreEqual(1, book.Score(7).Killed);
            Assert.AreEqual(1, book.Score(7).Bites);
        }

        [Test]
        public void Book_DropsGroupsThatNeverAppeared_AndPutsBossFirst()
        {
            var book = new SpectatorGroupBook();
            var never = book.NewTag();
            book.AddGroup(never, 1, 0, false, 0);
            book.Prune(1);
            Assert.AreEqual(1, book.GroupCount, "まだ待つ");
            book.Prune(SpectatorGroupBook.UnassignedTimeout + 0.1);
            Assert.AreEqual(0, book.GroupCount, "番号が付かないまま時間切れ");

            var a = book.NewTag();
            var boss = book.NewTag();
            book.AddGroup(a, 2, 0, false, 10);
            book.AddGroup(boss, 0, 0, true, 10);
            book.OnAssigned(a, 1);
            book.OnAssigned(boss, 2);
            book.BeginScan();
            book.Accumulate(1, 1f, 1f);
            book.Accumulate(2, 2f, 2f);
            book.EndScan(10);
            var list = new System.Collections.Generic.List<SpectatorGroupState>();
            book.Snapshot(list, 10);
            Assert.IsTrue(list[0].Boss);
            Assert.AreEqual(2, list[1].Number);

            book.OnRemoved(2, SpectatorRemoval.Killed);
            Assert.AreEqual(0, book.Score(0).Killed, "ボスは成績に入れない");
        }

        [Test]
        public void Book_ReusedNetIdMovesToNewGroup()
        {
            var book = new SpectatorGroupBook();
            var first = book.NewTag();
            var second = book.NewTag();
            book.AddGroup(first, 1, 0, false, 0);
            book.AddGroup(second, 2, 0, false, 0);
            book.OnAssigned(first, 5);
            book.OnAssigned(second, 5); // 消えた知らせを取りこぼして番号が使い回された
            book.BeginScan();
            book.Accumulate(5, 1f, 1f);
            book.EndScan(1);
            var list = new System.Collections.Generic.List<SpectatorGroupState>();
            book.Snapshot(list, 10);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(2, list[0].Number);
        }

        // ---- 地図 ----

        [Test]
        public void Map_CentersOnCoreAndScalesFarthestToOne()
        {
            var outX = new float[3];
            var outY = new float[3];
            SpectatorMapProjection.Project(new[] { 10f, 0f, 5f }, new[] { 0f, 4f, 0f }, 0f, 0f, outX, outY);
            Assert.AreEqual(1f, outX[0], 1e-5f);
            Assert.AreEqual(0.4f, outY[1], 1e-5f);
            Assert.AreEqual(0.5f, outX[2], 1e-5f);

            SpectatorMapProjection.Project(new[] { 2f }, new[] { 3f }, 2f, 3f, outX, outY);
            Assert.AreEqual(0f, outX[0], 1e-5f);
        }

        // ---- 本物の通信（このPCの中だけ） ----

        [Test]
        public void Server_AnswersOverRealSocketWithKeepAlive()
        {
            var api = MakeApi(new[] { 0.0 });
            using (var server = new SpectatorHttpServer(api.Handle, 8))
            {
                Assert.IsTrue(server.Start(0, out var error), error);
                using (var client = new TcpClient("127.0.0.1", server.Port))
                {
                    client.ReceiveTimeout = 3000;
                    var stream = client.GetStream();
                    var ask = Encoding.ASCII.GetBytes("GET /api/state?id=aaaaaaaa HTTP/1.1\r\nHost: t\r\n\r\n");
                    stream.Write(ask, 0, ask.Length);
                    StringAssert.Contains("\"me\":", ReadOne(stream));
                    stream.Write(ask, 0, ask.Length);
                    StringAssert.Contains("\"me\":", ReadOne(stream), "同じ線で2回目も答える");
                }
            }
        }

        private static string ReadOne(NetworkStream stream)
        {
            var buffer = new byte[8192];
            var total = 0;
            while (true)
            {
                var n = stream.Read(buffer, total, buffer.Length - total);
                Assert.Greater(n, 0, "返事の途中で切れた");
                total += n;
                var text = Encoding.UTF8.GetString(buffer, 0, total);
                var headEnd = text.IndexOf("\r\n\r\n", System.StringComparison.Ordinal);
                if (headEnd < 0)
                {
                    continue;
                }

                var lengthAt = text.IndexOf("Content-Length: ", System.StringComparison.Ordinal) + 16;
                var length = int.Parse(text.Substring(lengthAt, text.IndexOf('\r', lengthAt) - lengthAt));
                if (Encoding.UTF8.GetByteCount(text) >= headEnd + 4 + length)
                {
                    return text;
                }
            }
        }
    }
}
