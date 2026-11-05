using System;
using System.Globalization;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客ページ（スマホから菌を送る）の決まった値と起動オプション。Unity に頼らない（テストできる）。
    ///
    ///   -spectator-port N   観客ページの番号（ポート）。無ければ <see cref="DefaultPort"/>
    ///   -no-spectator       観客ページを動かさない
    ///
    /// ビルドでは何も付けなくても動く。エディタでは、メニュー「Tools/観客の菌/エディタの Play でも観客ページを動かす」を
    /// オンにしたときだけ動く（PlayerPrefs の <see cref="EditorEnabledKey"/>）。
    /// </summary>
    public static class SpectatorConfig
    {
        /// <summary>観客ページの番号。ほかの通信（47777 部屋さがし、47781〜47784 握力）と近いが重ならない。</summary>
        public const int DefaultPort = 47790;

        public const string PortFlag = "-spectator-port";
        public const string DisableFlag = "-no-spectator";

        /// <summary>エディタの Play でも動かすか（エディタのメニューが PlayerPrefs に 1 を入れる）。</summary>
        public const string EditorEnabledKey = "MS2026.Spectator.EditorEnabled";

        /// <summary>1回送ると出るふつうの菌の数。</summary>
        public const int GermsPerSend = 20;

        /// <summary>一人が次に送れるまでの秒数。</summary>
        public const float CooldownSeconds = 8f;

        /// <summary>覚えておく観客の数の上限（これを超えた人は「いっぱい」と出る）。</summary>
        public const int MaxSpectators = 400;

        /// <summary>同時につながっていられる数の上限（スマホ1台で1〜2本）。</summary>
        public const int MaxConnections = 256;

        public struct LaunchOptions
        {
            public bool Disabled;
            public int Port;
        }

        public static LaunchOptions Parse(string[] args)
        {
            var result = new LaunchOptions { Port = DefaultPort };
            if (args == null)
            {
                return result;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], DisableFlag, StringComparison.OrdinalIgnoreCase))
                {
                    result.Disabled = true;
                }
                else if (string.Equals(args[i], PortFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    i++;
                    if (int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port > 0 && port <= 65535)
                    {
                        result.Port = port;
                    }
                }
            }

            return result;
        }
    }
}
