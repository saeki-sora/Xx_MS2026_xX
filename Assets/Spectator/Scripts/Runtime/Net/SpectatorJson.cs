using System.Globalization;
using System.Text;

namespace MS2026.Spectator
{
    /// <summary>スマホに返す JSON を手で組み立てる小道具（数は必ず「.」区切り。文字はエスケープ）。</summary>
    public static class SpectatorJson
    {
        public static StringBuilder Str(this StringBuilder sb, string value)
        {
            sb.Append('"');
            if (value != null)
            {
                foreach (var c in value)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20 || c == '<' || c == '>' || c == '&')
                            {
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
            return sb;
        }

        public static StringBuilder Num(this StringBuilder sb, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return sb.Append('0');
            }

            return sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public static StringBuilder Num(this StringBuilder sb, int value) => sb.Append(value.ToString(CultureInfo.InvariantCulture));

        public static StringBuilder Bool(this StringBuilder sb, bool value) => sb.Append(value ? "true" : "false");

        public static StringBuilder Key(this StringBuilder sb, string key) => sb.Str(key).Append(':');
    }
}
