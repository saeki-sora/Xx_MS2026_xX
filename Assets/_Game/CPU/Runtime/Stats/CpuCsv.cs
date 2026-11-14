using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MS2026.Cpu
{
    /// <summary>結果の表の1行（決まった列 + 名前つきの数）。</summary>
    public sealed class CpuResultRow
    {
        public readonly List<string> Fixed = new List<string>();
        public readonly CpuCounters Counters = new CpuCounters();
    }

    /// <summary>
    /// 結果の表（CSV）を作る（決まりだけ。テストできるようにファイルには触らない）。
    /// 列 = 決まった列 + すべての行に出てきた名前つきの数（出てきた順）。無い数は空欄。Excel で開けるよう、書く側が BOM 付き UTF-8 にする。
    /// </summary>
    public static class CpuCsv
    {
        public static string Build(IReadOnlyList<string> fixedHeader, IReadOnlyList<CpuResultRow> rows)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>();
            foreach (var row in rows)
            {
                foreach (var key in row.Counters.Keys)
                {
                    if (seen.Add(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            var text = new StringBuilder();
            var first = true;
            foreach (var head in fixedHeader)
            {
                Cell(text, head, ref first);
            }

            foreach (var key in keys)
            {
                Cell(text, key, ref first);
            }

            text.Append("\r\n");

            foreach (var row in rows)
            {
                first = true;
                for (var i = 0; i < fixedHeader.Count; i++)
                {
                    Cell(text, i < row.Fixed.Count ? row.Fixed[i] : string.Empty, ref first);
                }

                foreach (var key in keys)
                {
                    Cell(text, row.Counters.Has(key) ? Number(row.Counters.Get(key)) : string.Empty, ref first);
                }

                text.Append("\r\n");
            }

            return text.ToString();
        }

        /// <summary>数を表に書く形（小数 2 桁まで、ロケールに左右されない）。</summary>
        public static string Number(float value)
        {
            return System.Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>CSV の1マス（, " 改行 を含むなら " で囲む）。</summary>
        public static string Escape(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void Cell(StringBuilder text, string value, ref bool first)
        {
            if (!first)
            {
                text.Append(',');
            }

            first = false;
            text.Append(Escape(value));
        }

        /// <summary>
        /// ウェーブの記録を試合の合計にまとめる（名前に「最大」を含む数はいちばん大きい値、ほかは足し算）。
        /// </summary>
        public static void Accumulate(CpuCounters total, CpuCounters wave)
        {
            foreach (var key in wave.Keys)
            {
                if (key.Contains("最大"))
                {
                    total.Max(key, wave.Get(key));
                }
                else
                {
                    total.Add(key, wave.Get(key));
                }
            }
        }
    }
}
