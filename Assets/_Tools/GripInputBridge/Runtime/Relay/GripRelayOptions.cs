using System;
using System.Collections.Generic;
using System.Globalization;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>
    /// 「1台の Arduino に4人分のセンサーをつなぎ、LAN で配る」使い方の設定（起動オプションから読む。純粋・テストあり）。
    /// <list type="bullet">
    /// <item><c>-grip-sensor 3</c> … このPCの前の人のセンサーは3番（1〜4）。付けたときだけ LAN で配る・受け取る。付けなければ今まで通り（通信しない）。</item>
    /// <item><c>-grip-relay-send</c> … Arduino が無くても、このPCのキーボード（Q/W/O/P = センサー1〜4）を放送する（テスト用）。</item>
    /// <item><c>-grip-relay-port 47781</c> … 放送に使う UDP の番号の始め（センサー n は 始め + n - 1 で受け取る）。</item>
    /// </list>
    /// <c>-grip-sensor=3</c> の形でもよい。大文字・小文字は区別しない。
    /// </summary>
    public sealed class GripRelayOptions
    {
        public const string SensorArgument = "-grip-sensor";
        public const string ForceSendArgument = "-grip-relay-send";
        public const string PortArgument = "-grip-relay-port";

        /// <summary>センサー1を受け取る UDP の番号（2〜4は続きの番号）。部屋さがし(47777)・ゲーム(7777)とは別。</summary>
        public const int DefaultBasePort = 47781;

        public const int MinSensorNumber = 1;
        public const int MaxSensorNumber = GripRelayPacket.SensorCount;

        /// <summary>このPCの前の人のセンサー番号（1〜4）。0 = 使わない（今まで通り）。</summary>
        public int SensorNumber { get; private set; }

        /// <summary>Arduino が無くても、キーボードの値を放送する（テスト用）。</summary>
        public bool ForceSend { get; private set; }

        /// <summary>センサー1を受け取る UDP の番号。</summary>
        public int BasePort { get; private set; } = DefaultBasePort;

        /// <summary>起動オプションの書き方が間違っていたときの説明（正しければ null）。</summary>
        public string Error { get; private set; }

        /// <summary>LAN で配る・受け取る使い方か。</summary>
        public bool Enabled => SensorNumber >= MinSensorNumber;

        /// <summary>このPCが受け取りに使う UDP の番号。</summary>
        public int ListenPort => PortForSensor(BasePort, SensorNumber);

        /// <summary>センサー番号(1-4)を受け取る UDP の番号。</summary>
        public static int PortForSensor(int basePort, int sensorNumber) => basePort + sensorNumber - 1;

        /// <summary>直接作る（エディタで試すとき用）。範囲外は Error に入れて使わない扱いにする。</summary>
        public static GripRelayOptions Create(int sensorNumber, bool forceSend, int basePort)
        {
            var options = new GripRelayOptions();
            options.SetBasePort(basePort);
            options.SetSensor(sensorNumber);
            options.ForceSend = forceSend;
            options.CheckForceSend();
            return options;
        }

        /// <summary>起動オプションを読む（先頭の実行ファイル名は混ざっていてもよい）。同じ物が2回あれば後の方を使う。</summary>
        public static GripRelayOptions Parse(IReadOnlyList<string> args, int defaultBasePort = DefaultBasePort)
        {
            var options = new GripRelayOptions();
            options.SetBasePort(defaultBasePort);
            if (args == null)
            {
                return options;
            }

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i] ?? string.Empty;
                if (Matches(arg, ForceSendArgument))
                {
                    options.ForceSend = true;
                    continue;
                }

                if (TryReadValue(args, ref i, arg, SensorArgument, out var sensorText))
                {
                    if (sensorText == null || !int.TryParse(sensorText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sensor))
                    {
                        options.SensorNumber = 0;
                        options.Error = $"{SensorArgument} の後ろには 1〜4 の数字を書いてください（書いてあった値: {sensorText ?? "なし"}）。";
                        continue;
                    }

                    options.SetSensor(sensor);
                    continue;
                }

                if (TryReadValue(args, ref i, arg, PortArgument, out var portText))
                {
                    if (portText == null || !int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                    {
                        options.Error = $"{PortArgument} の後ろには数字を書いてください（書いてあった値: {portText ?? "なし"}）。";
                        continue;
                    }

                    options.SetBasePort(port);
                }
            }

            options.CheckForceSend();
            return options;
        }

        private void SetSensor(int sensor)
        {
            if (sensor == 0)
            {
                SensorNumber = 0;
                return;
            }

            if (sensor < MinSensorNumber || sensor > MaxSensorNumber)
            {
                SensorNumber = 0;
                Error = $"{SensorArgument} の番号は {MinSensorNumber}〜{MaxSensorNumber} にしてください（書いてあった値: {sensor}）。";
                return;
            }

            SensorNumber = sensor;
        }

        private void SetBasePort(int port)
        {
            // 4つ分の番号を使うので、最後の番号が 65535 を超えないようにする。1024 未満は Windows の決まった用途と重なりやすい。
            if (port < 1024 || port + MaxSensorNumber - 1 > 65535)
            {
                Error = $"{PortArgument} の番号は 1024〜{65535 - MaxSensorNumber + 1} にしてください（書いてあった値: {port}）。{DefaultBasePort} を使います。";
                BasePort = DefaultBasePort;
                return;
            }

            BasePort = port;
        }

        private void CheckForceSend()
        {
            if (ForceSend && !Enabled && Error == null)
            {
                Error = $"{ForceSendArgument} は {SensorArgument} と一緒に使ってください（放送はセンサー番号を決めたPCだけが行います）。";
            }
        }

        private static bool Matches(string arg, string name) => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase);

        // 「-name 値」と「-name=値」の両方を読む。名前が合わなければ false。値が無ければ value = null で true。
        private static bool TryReadValue(IReadOnlyList<string> args, ref int index, string arg, string name, out string value)
        {
            value = null;
            if (Matches(arg, name))
            {
                if (index + 1 < args.Count && !IsOptionName(args[index + 1]))
                {
                    index++;
                    value = args[index]?.Trim();
                }

                return true;
            }

            var prefix = name + "=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(prefix.Length).Trim();
                return true;
            }

            return false;
        }

        // 次の引数が別の起動オプション（-xxx）なら、値ではない。負の数（-1 など）は値として読む。
        private static bool IsOptionName(string arg)
        {
            return !string.IsNullOrEmpty(arg) && arg[0] == '-' && arg.Length > 1 && !char.IsDigit(arg[1]);
        }
    }
}
