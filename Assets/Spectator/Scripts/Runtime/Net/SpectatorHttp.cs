using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MS2026.Spectator
{
    /// <summary>届いた1つの頼み（HTTP のリクエスト）。</summary>
    public sealed class SpectatorRequest
    {
        public string Method = "GET";

        /// <summary>「?」より前（例: /api/state）。</summary>
        public string Path = "/";

        /// <summary>「?」より後をばらしたもの。</summary>
        public Dictionary<string, string> Query = new Dictionary<string, string>();

        /// <summary>見出し（名前は小文字）。</summary>
        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        public string Body = "";

        /// <summary>このあともつないだままにしてよいか（HTTP/1.1 の既定は true）。</summary>
        public bool KeepAlive = true;
    }

    /// <summary>返す中身（HTTP のレスポンス）。</summary>
    public struct SpectatorResponse
    {
        public int Status;
        public string ContentType;
        public byte[] Body;

        /// <summary>true ならスマホに覚えさせない（状態など、毎回変わるもの）。</summary>
        public bool NoStore;

        public static SpectatorResponse Json(string json) =>
            new SpectatorResponse { Status = 200, ContentType = "application/json; charset=utf-8", Body = Encoding.UTF8.GetBytes(json), NoStore = true };

        public static SpectatorResponse Html(byte[] body) =>
            new SpectatorResponse { Status = 200, ContentType = "text/html; charset=utf-8", Body = body, NoStore = true };

        public static SpectatorResponse Empty(int status) =>
            new SpectatorResponse { Status = status, ContentType = "text/plain; charset=utf-8", Body = Array.Empty<byte>(), NoStore = true };
    }

    /// <summary>HTTP の読み書きの決まり（Unity に頼らない。テストできる）。</summary>
    public static class SpectatorHttp
    {
        public const int MaxHeadBytes = 8 * 1024;
        public const int MaxBodyBytes = 4 * 1024;

        /// <summary>見出しの部分（最初の行から空行の手前まで）を読み解く。おかしければ false。</summary>
        public static bool TryParseHead(string head, out SpectatorRequest request, out int contentLength)
        {
            request = null;
            contentLength = 0;
            if (string.IsNullOrEmpty(head))
            {
                return false;
            }

            var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length != 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            {
                return false;
            }

            var req = new SpectatorRequest { Method = first[0].ToUpperInvariant() };
            var target = first[1];
            var q = target.IndexOf('?');
            req.Path = q >= 0 ? target.Substring(0, q) : target;
            if (q >= 0)
            {
                req.Query = ParseForm(target.Substring(q + 1));
            }

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var name = line.Substring(0, colon).Trim().ToLowerInvariant();
                req.Headers[name] = line.Substring(colon + 1).Trim();
            }

            if (req.Headers.TryGetValue("content-length", out var lengthText))
            {
                if (!int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength) || contentLength < 0 || contentLength > MaxBodyBytes)
                {
                    return false;
                }
            }

            var http10 = first[2] == "HTTP/1.0";
            req.Headers.TryGetValue("connection", out var connection);
            if (string.Equals(connection, "close", StringComparison.OrdinalIgnoreCase))
            {
                req.KeepAlive = false;
            }
            else if (http10 && !string.Equals(connection, "keep-alive", StringComparison.OrdinalIgnoreCase))
            {
                req.KeepAlive = false;
            }

            request = req;
            return true;
        }

        /// <summary>「a=1&amp;b=2」をばらす（%xx と + を戻す）。同じ名前は後の方が勝つ。</summary>
        public static Dictionary<string, string> ParseForm(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (var pair in text.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                var eq = pair.IndexOf('=');
                var key = Decode(eq >= 0 ? pair.Substring(0, eq) : pair);
                var value = eq >= 0 ? Decode(pair.Substring(eq + 1)) : "";
                if (key.Length > 0)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        private static string Decode(string s)
        {
            try
            {
                return Uri.UnescapeDataString(s.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                return s;
            }
        }

        /// <summary>返す中身を、送るバイト列にする。</summary>
        public static byte[] BuildResponse(SpectatorResponse response, bool keepAlive)
        {
            var body = response.Body ?? Array.Empty<byte>();
            var sb = new StringBuilder(256);
            sb.Append("HTTP/1.1 ").Append(response.Status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Reason(response.Status)).Append("\r\n");
            sb.Append("Content-Type: ").Append(response.ContentType ?? "text/plain").Append("\r\n");
            sb.Append("Content-Length: ").Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            if (response.NoStore)
            {
                sb.Append("Cache-Control: no-store\r\n");
            }

            sb.Append("X-Content-Type-Options: nosniff\r\n");
            sb.Append(keepAlive ? "Connection: keep-alive\r\nKeep-Alive: timeout=10\r\n" : "Connection: close\r\n");
            sb.Append("\r\n");
            var head = Encoding.ASCII.GetBytes(sb.ToString());
            var bytes = new byte[head.Length + body.Length];
            Buffer.BlockCopy(head, 0, bytes, 0, head.Length);
            Buffer.BlockCopy(body, 0, bytes, head.Length, body.Length);
            return bytes;
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 204: return "No Content";
                case 400: return "Bad Request";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 413: return "Payload Too Large";
                case 503: return "Service Unavailable";
                default: return "Status";
            }
        }
    }

    /// <summary>
    /// つないだままの1本の線から、頼みを1つずつ読み出す（見出しの後ろに次の頼みが続いていても取りこぼさない）。
    /// </summary>
    public sealed class SpectatorRequestReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[SpectatorHttp.MaxHeadBytes + SpectatorHttp.MaxBodyBytes];
        private int _count;

        public SpectatorRequestReader(Stream stream)
        {
            _stream = stream;
        }

        /// <summary>次の頼みを読む。相手が閉じた・おかしな頼み・大きすぎる、のときは false（線を閉じる）。</summary>
        public bool TryRead(out SpectatorRequest request)
        {
            request = null;
            var headEnd = -1;
            while (true)
            {
                headEnd = IndexOfHeadEnd();
                if (headEnd >= 0)
                {
                    break;
                }

                if (_count >= SpectatorHttp.MaxHeadBytes || !Fill())
                {
                    return false;
                }
            }

            var head = Encoding.ASCII.GetString(_buffer, 0, headEnd);
            var bodyStart = headEnd + 4;
            if (!SpectatorHttp.TryParseHead(head, out request, out var length))
            {
                return false;
            }

            while (_count < bodyStart + length)
            {
                if (!Fill())
                {
                    return false;
                }
            }

            request.Body = length > 0 ? Encoding.UTF8.GetString(_buffer, bodyStart, length) : "";
            var used = bodyStart + length;
            Buffer.BlockCopy(_buffer, used, _buffer, 0, _count - used);
            _count -= used;
            return true;
        }

        private int IndexOfHeadEnd()
        {
            for (var i = 0; i + 3 < _count; i++)
            {
                if (_buffer[i] == '\r' && _buffer[i + 1] == '\n' && _buffer[i + 2] == '\r' && _buffer[i + 3] == '\n')
                {
                    return i;
                }
            }

            return -1;
        }

        private bool Fill()
        {
            if (_count >= _buffer.Length)
            {
                return false;
            }

            int read;
            try
            {
                read = _stream.Read(_buffer, _count, _buffer.Length - _count);
            }
            catch (IOException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (read <= 0)
            {
                return false;
            }

            _count += read;
            return true;
        }
    }
}
