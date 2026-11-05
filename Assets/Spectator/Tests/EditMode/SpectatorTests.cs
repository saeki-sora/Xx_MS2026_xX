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
            api.Board = new SpectatorBoard { Phase = phase, StageKey = "Stage1:3:1", ExitCount = 3, SharedJson = "{\"phase\":\"" + phase + "\"}" };
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
