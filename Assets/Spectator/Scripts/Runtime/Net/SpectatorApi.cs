using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MS2026.Spectator
{
    /// <summary>観客の「送る」1回分。Web サーバーのスレッドが積み、ゲームの係（メインスレッド）が取り出して菌を出す。</summary>
    public sealed class SpectatorOrder
    {
        public string SpectatorId;
        public int Number;
        public string Name;
        public int ColorIndex;
        public int ExitIndex;
        public string StageKey;
    }

    /// <summary>試合の様子の写し。ゲームの係が 1 秒に数回作り直して丸ごと差し替える（中身は変えない）。</summary>
    public sealed class SpectatorBoard
    {
        /// <summary>off（ゲームの画面でない）・wait（試合の前・ウェーブの合間）・play（送れる）・end（試合が終わった）。</summary>
        public string Phase = SpectatorPhase.Off;

        /// <summary>このステージの見分け（シーン名と出口の数）。スマホが古い地図のまま送ってきたら断る。</summary>
        public string StageKey = "";

        public int ExitCount;

        /// <summary>全員に同じ部分の JSON（地図・残り時間など）。</summary>
        public string SharedJson = "{}";
    }

    public static class SpectatorPhase
    {
        public const string Off = "off";
        public const string Wait = "wait";
        public const string Play = "play";
        public const string End = "end";
    }

    /// <summary>
    /// スマホからの頼みに答える（Web サーバーのスレッドで動く。Unity に頼らない）。
    ///   GET  /             スマホのページ
    ///   GET  /api/state    id=…&amp;color=…  → 試合の様子と自分の様子（初めての人には番号と名前を付ける）
    ///   POST /api/send     id, exit, stage, color → 菌を送る（待ち時間・試合中か・地図が古くないかを確かめる）
    ///   POST /api/color    id, color → 色を変える
    /// </summary>
    public sealed class SpectatorApi
    {
        private readonly SpectatorRoster _roster;
        private readonly ConcurrentQueue<SpectatorOrder> _orders = new ConcurrentQueue<SpectatorOrder>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Func<double> _now;
        private volatile SpectatorBoard _board = new SpectatorBoard();
        private volatile byte[] _page = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><p>観客ページを準備しています</p>");

        public SpectatorApi(SpectatorRoster roster, Func<double> now = null)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _now = now ?? (() => _clock.Elapsed.TotalSeconds);
        }

        public SpectatorRoster Roster => _roster;

        public SpectatorBoard Board
        {
            get => _board;
            set => _board = value ?? new SpectatorBoard();
        }

        public void SetPage(byte[] html)
        {
            if (html != null && html.Length > 0)
            {
                _page = html;
            }
        }

        /// <summary>積まれた「送る」を1つ取り出す（ゲームの係が毎フレーム呼ぶ）。</summary>
        public bool TryDequeue(out SpectatorOrder order) => _orders.TryDequeue(out order);

        public SpectatorResponse Handle(SpectatorRequest request)
        {
            switch (request.Path)
            {
                case "/":
                case "/index.html":
                    return request.Method == "GET" ? SpectatorResponse.Html(_page) : SpectatorResponse.Empty(405);
                case "/api/state":
                    return request.Method == "GET" ? State(request.Query) : SpectatorResponse.Empty(405);
                case "/api/send":
                    return request.Method == "POST" ? Send(SpectatorHttp.ParseForm(request.Body)) : SpectatorResponse.Empty(405);
                case "/api/color":
                    return request.Method == "POST" ? Color(SpectatorHttp.ParseForm(request.Body)) : SpectatorResponse.Empty(405);
                case "/favicon.ico":
                    return SpectatorResponse.Empty(204);
                default:
                    return SpectatorResponse.Empty(404);
            }
        }

        private SpectatorResponse State(System.Collections.Generic.Dictionary<string, string> q)
        {
            q.TryGetValue("id", out var id);
            var color = ReadInt(q, "color", 0);
            var board = _board;
            var sb = new StringBuilder(512);
            sb.Append('{').Key("s").Append(board.SharedJson).Append(',');
            if (_roster.TryGetOrAdd(id, color, _now(), out var me))
            {
                sb.Key("me");
                AppendMe(sb, me);
            }
            else
            {
                sb.Key("full").Bool(SpectatorRoster.IsValidId(id));
            }

            sb.Append('}');
            return SpectatorResponse.Json(sb.ToString());
        }

        private SpectatorResponse Send(System.Collections.Generic.Dictionary<string, string> form)
        {
            form.TryGetValue("id", out var id);
            form.TryGetValue("stage", out var stage);
            var exit = ReadInt(form, "exit", -1);
            var color = ReadInt(form, "color", -1);
            var board = _board;

            if (board.Phase != SpectatorPhase.Play)
            {
                return Reply(false, "not_playing", null);
            }

            if (!string.Equals(stage, board.StageKey, StringComparison.Ordinal))
            {
                return Reply(false, "stage", null);
            }

            if (exit < 0 || exit >= board.ExitCount)
            {
                return Reply(false, "exit", null);
            }

            if (color >= 0)
            {
                _roster.SetColor(id, color);
            }

            var result = _roster.TrySend(id, _now(), out var me);
            switch (result)
            {
                case SpectatorSendResult.Unknown:
                    return Reply(false, "unknown", null);
                case SpectatorSendResult.CoolingDown:
                    return Reply(false, "cooldown", me);
            }

            _orders.Enqueue(new SpectatorOrder
            {
                SpectatorId = id,
                Number = me.Number,
                Name = me.Name,
                ColorIndex = me.ColorIndex,
                ExitIndex = exit,
                StageKey = board.StageKey
            });
            return Reply(true, null, me);
        }

        private SpectatorResponse Color(System.Collections.Generic.Dictionary<string, string> form)
        {
            form.TryGetValue("id", out var id);
            _roster.SetColor(id, ReadInt(form, "color", 0));
            return SpectatorResponse.Json("{\"ok\":true}");
        }

        private SpectatorResponse Reply(bool ok, string reason, SpectatorInfo? me)
        {
            var sb = new StringBuilder(160);
            sb.Append('{').Key("ok").Bool(ok);
            if (reason != null)
            {
                sb.Append(',').Key("reason").Str(reason);
            }

            if (me.HasValue)
            {
                sb.Append(',').Key("me");
                AppendMe(sb, me.Value);
            }

            sb.Append('}');
            return SpectatorResponse.Json(sb.ToString());
        }

        private void AppendMe(StringBuilder sb, SpectatorInfo me)
        {
            sb.Append('{')
                .Key("name").Str(me.Name).Append(',')
                .Key("number").Num(me.Number).Append(',')
                .Key("color").Num(me.ColorIndex).Append(',')
                .Key("cooldown").Num(me.CooldownRemaining).Append(',')
                .Key("cooldownTotal").Num(_roster.CooldownSeconds).Append(',')
                .Key("sent").Num(me.Sent)
                .Append('}');
        }

        private static int ReadInt(System.Collections.Generic.Dictionary<string, string> map, string key, int fallback)
        {
            return map.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }
    }
}
