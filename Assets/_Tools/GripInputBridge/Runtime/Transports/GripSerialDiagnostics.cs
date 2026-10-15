using System.Text;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機（自作コントローラー）から届いている「生の」受信の様子。デバッグ表示用（<c>SerialGripTransport.GetDiagnostics</c> が作る）。
    /// 形式が合わずに捨てた行も見えるので、「値が来ない」のか「来ているが読めない」のかを見分けられる。
    /// </summary>
    public readonly struct GripSerialDiagnostics
    {
        /// <summary>最後に届いた1行（読めたかどうかに関係なく、届いたまま）。まだ何も届いていなければ null。</summary>
        public readonly string LastLine;

        /// <summary>最後に読めなかった（形式が合わず捨てた）1行。まだ無ければ null。</summary>
        public readonly string LastRejectedLine;

        /// <summary>届いた行の数（読めなかった行も含む）。</summary>
        public readonly long LinesReceived;

        /// <summary>読めなかった行の数。</summary>
        public readonly long LinesRejected;

        /// <summary>最後に行が届いてからの秒数（一度も届いていなければ +∞）。</summary>
        public readonly double SecondsSinceLastLine;

        public GripSerialDiagnostics(string lastLine, string lastRejectedLine, long linesReceived, long linesRejected, double secondsSinceLastLine)
        {
            LastLine = lastLine;
            LastRejectedLine = lastRejectedLine;
            LinesReceived = linesReceived;
            LinesRejected = linesRejected;
            SecondsSinceLastLine = secondsSinceLastLine;
        }

        /// <summary>
        /// 受信した1行を、画面に出せる形にする。改行・タブなどの見えない文字は <c>\r</c> <c>\n</c> <c>\t</c> <c>\x00</c> の形で見せ
        /// （Arduino の println は行末に \r が付く）、長すぎる行は <paramref name="maxLength"/> 文字で切って「…」を付ける。null は「(まだ届いていません)」。
        /// </summary>
        public static string ToVisibleText(string line, int maxLength = 80)
        {
            if (line == null)
            {
                return "(まだ届いていません)";
            }

            var builder = new StringBuilder(line.Length + 8);
            foreach (var c in line)
            {
                if (builder.Length >= maxLength)
                {
                    builder.Append('…');
                    break;
                }

                switch (c)
                {
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ' || c == '\u007f')
                        {
                            builder.Append("\\x").Append(((int)c).ToString("X2"));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
