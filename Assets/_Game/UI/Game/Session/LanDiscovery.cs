using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 同じLANの部屋（ホスト）を自動で見つける。IPアドレスを打たなくても、一覧から選ぶだけで参加できる。
    /// ・ホストは UDP の「呼びかけ用の番号（既定 47777）」で待ち、「部屋ありますか？」に部屋の様子を返す
    /// ・参加側は1秒ごとに LAN 全体（ブロードキャスト）と自分のPC（同じPCで試すとき）に呼びかけ、返事を一覧にする
    /// ゲームの通信（UDP 7777）とは別の番号なので、ゲームの同期には影響しない。Windows のファイアウォールで許可が要る（初回に確認が出る）。
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        public const string Magic = "MS2026LOBBY";
        public const int Protocol = 1;
        public const float SearchInterval = 1f;
        public const float ForgetAfter = 3.5f;

        /// <summary>ホストが返す部屋の様子。</summary>
        public struct Reply
        {
            public string hostName;
            public int gamePort;
            public int takenMask;
            public LobbyPhase phase;
            public int players;
            public string build;
        }

        /// <summary>見つかった部屋。</summary>
        public struct FoundHost
        {
            public string address;
            public Reply reply;
            public float lastSeen;
            public bool sameBuild;

            public bool IsTaken(int seat) => (reply.takenMask & (1 << seat)) != 0;
        }

        private readonly List<FoundHost> _hosts = new List<FoundHost>();
        private readonly int _port;
        private UdpClient _responder;
        private UdpClient _searcher;
        private Func<Reply> _describe;
        private float _nextSearch;
        private bool _warned;

        public LanDiscovery(int port)
        {
            _port = port;
        }

        public IReadOnlyList<FoundHost> Hosts => _hosts;

        public bool IsResponding => _responder != null;

        public bool IsSearching => _searcher != null;

        /// <summary>見つかった部屋の一覧が変わったとき。</summary>
        public event Action Changed;

        // ── ホスト側 ─────────────────
        public void StartResponder(Func<Reply> describe)
        {
            StopResponder();
            _describe = describe;
            try
            {
                var client = new UdpClient();
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                client.EnableBroadcast = true;
                _responder = client;
            }
            catch (Exception e)
            {
                WarnOnce($"部屋のお知らせ（UDP {_port}）を始められませんでした。一覧には出ませんが、アドレスを入力すれば参加できます。({e.Message})");
            }
        }

        public void StopResponder()
        {
            _responder?.Close();
            _responder = null;
        }

        // ── 参加側 ─────────────────
        public void StartSearch()
        {
            if (_searcher != null)
            {
                return;
            }

            try
            {
                var client = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
                _searcher = client;
                _nextSearch = 0f;
            }
            catch (Exception e)
            {
                WarnOnce($"部屋を探せませんでした。アドレスを入力して参加してください。({e.Message})");
            }
        }

        public void StopSearch()
        {
            _searcher?.Close();
            _searcher = null;
            if (_hosts.Count > 0)
            {
                _hosts.Clear();
                Changed?.Invoke();
            }
        }

        /// <summary>毎フレーム呼ぶ（届いた物を読み、必要なら呼びかけ・返事をする）。</summary>
        public void Tick(float now)
        {
            PollResponder();
            PollSearcher(now);
        }

        public void Dispose()
        {
            StopResponder();
            StopSearch();
        }

        private void PollResponder()
        {
            if (_responder == null)
            {
                return;
            }

            try
            {
                while (_responder.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = _responder.Receive(ref from);
                    if (!IsRequest(Encoding.UTF8.GetString(data)) || _describe == null)
                    {
                        continue;
                    }

                    var reply = Encoding.UTF8.GetBytes(FormatReply(_describe()));
                    _responder.Send(reply, reply.Length, from);
                }
            }
            catch (Exception e)
            {
                WarnOnce($"部屋のお知らせでエラーが起きました: {e.Message}");
            }
        }

        private void PollSearcher(float now)
        {
            if (_searcher == null)
            {
                return;
            }

            var changed = false;
            try
            {
                if (now >= _nextSearch)
                {
                    _nextSearch = now + SearchInterval;
                    var request = Encoding.UTF8.GetBytes(FormatRequest());
                    foreach (var target in BroadcastTargets(_port))
                    {
                        try
                        {
                            _searcher.Send(request, request.Length, target);
                        }
                        catch (SocketException)
                        {
                            // そのネットワークには送れない（無効なアダプタなど）。ほかの宛先は続ける。
                        }
                    }
                }

                while (_searcher.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = _searcher.Receive(ref from);
                    if (TryParseReply(Encoding.UTF8.GetString(data), out var reply))
                    {
                        changed |= Upsert(from.Address.ToString(), reply, now);
                    }
                }
            }
            catch (Exception e)
            {
                WarnOnce($"部屋を探している途中でエラーが起きました: {e.Message}");
            }

            for (var i = _hosts.Count - 1; i >= 0; i--)
            {
                if (now - _hosts[i].lastSeen > ForgetAfter)
                {
                    _hosts.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private bool Upsert(string address, Reply reply, float now)
        {
            var found = new FoundHost { address = address, reply = reply, lastSeen = now, sameBuild = reply.build == BuildId };
            for (var i = 0; i < _hosts.Count; i++)
            {
                if (_hosts[i].address == address && _hosts[i].reply.gamePort == reply.gamePort)
                {
                    var before = _hosts[i];
                    _hosts[i] = found;
                    return before.reply.takenMask != reply.takenMask || before.reply.phase != reply.phase || before.reply.hostName != reply.hostName ||
                           before.reply.players != reply.players;
                }
            }

            _hosts.Add(found);
            return true;
        }

        private void WarnOnce(string message)
        {
            if (_warned)
            {
                return;
            }

            _warned = true;
            Debug.LogWarning("[Lobby] " + message);
        }

        // ── 文字のやり取り（テストあり）─────────────────

        /// <summary>同じビルドかどうかの目印（違うビルド同士は同期が崩れるので、一覧で注意を出す）。</summary>
        public static string BuildId => Application.version;

        public static string FormatRequest() => $"{Magic}|{Protocol}|?";

        public static bool IsRequest(string text) => text == FormatRequest();

        public static string FormatReply(Reply reply)
        {
            return string.Join("|", Magic, Protocol.ToString(CultureInfo.InvariantCulture), "!", Sanitize(reply.hostName),
                reply.gamePort.ToString(CultureInfo.InvariantCulture), reply.takenMask.ToString(CultureInfo.InvariantCulture),
                ((int)reply.phase).ToString(CultureInfo.InvariantCulture), reply.players.ToString(CultureInfo.InvariantCulture), Sanitize(reply.build));
        }

        public static bool TryParseReply(string text, out Reply reply)
        {
            reply = default;
            var parts = (text ?? string.Empty).Split('|');
            if (parts.Length != 9 || parts[0] != Magic || parts[1] != Protocol.ToString(CultureInfo.InvariantCulture) || parts[2] != "!")
            {
                return false;
            }

            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
                !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask) ||
                !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var phase) ||
                !int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var players))
            {
                return false;
            }

            reply = new Reply
            {
                hostName = parts[3],
                gamePort = port,
                takenMask = mask & 0xF,
                phase = Enum.IsDefined(typeof(LobbyPhase), (byte)phase) ? (LobbyPhase)phase : LobbyPhase.Waiting,
                players = players,
                build = parts[8]
            };
            return true;
        }

        private static string Sanitize(string text) => (text ?? string.Empty).Replace("|", "/");

        /// <summary>呼びかけの宛先: LAN全体・各ネットワークのブロードキャスト・自分のPC。</summary>
        public static IEnumerable<IPEndPoint> BroadcastTargets(int port)
        {
            yield return new IPEndPoint(IPAddress.Broadcast, port);
            foreach (var (_, broadcast) in LocalIPv4())
            {
                if (broadcast != null)
                {
                    yield return new IPEndPoint(broadcast, port);
                }
            }

            yield return new IPEndPoint(IPAddress.Loopback, port);
        }

        /// <summary>このPCのLANのIPv4アドレス（と、そのネットワークのブロードキャストアドレス）。使えるネットワークを先に並べる。</summary>
        public static List<(IPAddress address, IPAddress broadcast)> LocalIPv4()
        {
            var result = new List<(IPAddress, IPAddress)>();
            try
            {
                var withGateway = new List<(IPAddress, IPAddress)>();
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    var props = nic.GetIPProperties();
                    var hasGateway = false;
                    foreach (var gateway in props.GatewayAddresses)
                    {
                        hasGateway |= gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any);
                    }

                    foreach (var unicast in props.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                        {
                            continue;
                        }

                        var entry = (unicast.Address, Broadcast(unicast.Address, unicast.IPv4Mask));
                        (hasGateway ? withGateway : result).Add(entry);
                    }
                }

                withGateway.AddRange(result);
                return withGateway;
            }
            catch (Exception)
            {
                return result; // 取れない環境（権限など）では空のまま。アドレスの表示が出ないだけ。
            }
        }

        /// <summary>アドレスとネットマスクから、そのネットワークのブロードキャストアドレスを求める。</summary>
        public static IPAddress Broadcast(IPAddress address, IPAddress mask)
        {
            if (address == null || mask == null)
            {
                return null;
            }

            var a = address.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4)
            {
                return null;
            }

            var b = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                b[i] = (byte)(a[i] | ~m[i]);
            }

            return new IPAddress(b);
        }

        /// <summary>このPCのアドレスを人に見せる文（例: 「192.168.1.12」、複数なら「 / 」でつなぐ）。</summary>
        public static string DescribeLocalAddresses(int max = 2)
        {
            var list = LocalIPv4();
            if (list.Count == 0)
            {
                return "（ネットワークにつながっていません）";
            }

            var parts = new List<string>();
            for (var i = 0; i < list.Count && i < max; i++)
            {
                parts.Add(list[i].address.ToString());
            }

            return string.Join(" / ", parts);
        }
    }
}
