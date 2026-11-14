using System;

namespace MS2026.Cpu
{
    /// <summary>起動オプションで決めた CPU の席。</summary>
    public enum CpuSeatMode
    {
        /// <summary>指定なし（設定のとおり）。</summary>
        Default = 0,

        /// <summary>CPU を使わない（-cpu none）。</summary>
        None = 1,

        /// <summary>全部の席（自分の席も。-cpu all / -cpu-all。自動プレイ用）。</summary>
        All = 2,

        /// <summary>指定した席だけ（-cpu 2,3,4）。</summary>
        List = 3
    }

    /// <summary>
    /// CPU の起動オプション（.exe・bat 用。エディタでは使わない）。
    ///   -cpu 2,3,4        P2・P3・P4 を CPU にする（番号は P1〜P4。人が入っている席は人が優先）
    ///   -cpu all          全部の席を CPU にする（自分の席も。-cpu-all でも同じ）
    ///   -cpu none         CPU を使わない
    ///   -cpu-level hard   強さ（easy / normal / hard、1〜3）
    ///   -cpu-seed 123     乱数の種（同じ種なら同じ動き。0 = ばらばら）
    ///   -autoplay 10      自動プレイ: 全部の席を CPU にして 10 試合続けて回す（席は -cpu で変えられる）
    ///   -timescale 2      早送り（自動プレイのときだけ。1〜4）
    ///   -cpu-csv          自動プレイでなくても、CPU がいる試合の結果を表（CSV）に書く
    /// </summary>
    public struct CpuLaunchOptions
    {
        public const string SeatsFlag = "-cpu";
        public const string AllFlag = "-cpu-all";
        public const string LevelFlag = "-cpu-level";
        public const string SeedFlag = "-cpu-seed";
        public const string AutoplayFlag = "-autoplay";
        public const string TimeScaleFlag = "-timescale";
        public const string CsvFlag = "-cpu-csv";

        public CpuSeatMode SeatMode;

        /// <summary><see cref="CpuSeatMode.List"/> のときの席（bit p = P(p+1)）。</summary>
        public int SeatMask;

        public CpuLevel? Level;
        public int? Seed;

        /// <summary>自動プレイの試合数（指定なしは null）。</summary>
        public int? AutoplayMatches;

        /// <summary>早送りの倍率（指定なしは null）。</summary>
        public float? TimeScale;

        /// <summary>結果の表を書く（-cpu-csv）。</summary>
        public bool Csv;

        public bool HasAny => SeatMode != CpuSeatMode.Default || Level.HasValue || Seed.HasValue || AutoplayMatches.HasValue ||
                              TimeScale.HasValue || Csv;

        /// <summary>この起動のオプション（1回だけ読む）。</summary>
        public static CpuLaunchOptions Current => Cache.Value;

        // 構造体の中に自分自身の型の静的な欄を持つと読み込めない実行環境があるので、別のクラスに置く。
        private static class Cache
        {
            public static readonly CpuLaunchOptions Value = Parse(ReadArgs());

            private static string[] ReadArgs()
            {
                try
                {
                    return Environment.GetCommandLineArgs();
                }
                catch (Exception)
                {
                    return Array.Empty<string>();
                }
            }
        }

        public static CpuLaunchOptions Parse(string[] args)
        {
            var result = new CpuLaunchOptions();
            if (args == null)
            {
                return result;
            }

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, AllFlag, StringComparison.OrdinalIgnoreCase))
                {
                    result.SeatMode = CpuSeatMode.All;
                }
                else if (string.Equals(arg, SeatsFlag, StringComparison.OrdinalIgnoreCase))
                {
                    var value = NextValue(args, ref i);
                    ParseSeats(value, ref result);
                }
                else if (string.Equals(arg, LevelFlag, StringComparison.OrdinalIgnoreCase))
                {
                    var value = NextValue(args, ref i);
                    if (CpuLevels.TryParse(value, out var level))
                    {
                        result.Level = level;
                    }
                }
                else if (string.Equals(arg, SeedFlag, StringComparison.OrdinalIgnoreCase))
                {
                    var value = NextValue(args, ref i);
                    if (int.TryParse(value, out var seed))
                    {
                        result.Seed = seed;
                    }
                }
                else if (string.Equals(arg, AutoplayFlag, StringComparison.OrdinalIgnoreCase))
                {
                    // 数が無ければ 1 試合。
                    var value = NextValue(args, ref i);
                    result.AutoplayMatches = int.TryParse(value, out var matches) ? Math.Max(1, matches) : 1;
                }
                else if (string.Equals(arg, TimeScaleFlag, StringComparison.OrdinalIgnoreCase))
                {
                    var value = NextValue(args, ref i);
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                            out var scale) && scale > 0f)
                    {
                        result.TimeScale = scale;
                    }
                }
                else if (string.Equals(arg, CsvFlag, StringComparison.OrdinalIgnoreCase))
                {
                    result.Csv = true;
                }
            }

            return result;
        }

        // 「2,3,4」「all」「none」。読めなければ指定なしのまま。
        private static void ParseSeats(string value, ref CpuLaunchOptions result)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var lower = value.Trim().ToLowerInvariant();
            if (lower == "all")
            {
                result.SeatMode = CpuSeatMode.All;
                return;
            }

            if (lower == "none" || lower == "off" || lower == "0")
            {
                result.SeatMode = CpuSeatMode.None;
                result.SeatMask = 0;
                return;
            }

            var mask = 0;
            foreach (var part in lower.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var text = part.StartsWith("p") ? part.Substring(1) : part;
                if (int.TryParse(text, out var number) && number >= 1 && number <= CpuSeatRules.PlayerCount)
                {
                    mask |= 1 << (number - 1);
                }
            }

            if (mask != 0)
            {
                result.SeatMode = CpuSeatMode.List;
                result.SeatMask = mask;
            }
        }

        private static string NextValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            i++;
            return args[i];
        }
    }
}
