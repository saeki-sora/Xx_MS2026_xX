using System;
using System.Collections.Generic;
using System.Text;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>ポートさがしで試した1つのポートの今の様子。</summary>
    public enum GripPortState
    {
        /// <summary>開いている途中。</summary>
        Opening,

        /// <summary>このPCに無い（設定の初期値として試しただけ）。</summary>
        NotPresent,

        /// <summary>開けなかった（ほかのソフトが使っている など）。</summary>
        OpenFailed,

        /// <summary>開けたが、1行も届かない。</summary>
        Silent,

        /// <summary>行は届くが、どれも読めない形。</summary>
        Unreadable,

        /// <summary>読める値が届いた。</summary>
        Readable
    }

    /// <summary>
    /// ポートさがし（<c>SerialGripPortSearch</c>）で試した1つのポートの様子。F8 の表示と、見つからなかったときのログに使う。
    /// 「Arduino を PC が認識していない」「開けない」「何も来ない」「来るが読めない」を見分けるため。
    /// </summary>
    public readonly struct GripPortReport
    {
        public readonly string Port;
        public readonly GripPortState State;

        /// <summary>Windows がそのポートに付けている機器の名前（<c>\Device\BthModem0</c> など）。取れなければ null。</summary>
        public readonly string DevicePath;

        /// <summary>開けなかった理由（<see cref="GripPortState.OpenFailed"/> のとき）。</summary>
        public readonly string OpenError;

        /// <summary>ほかのソフトが使っていて開けなかったか。</summary>
        public readonly bool OpenAccessDenied;

        /// <summary>届いた行の数。</summary>
        public readonly long LinesReceived;

        /// <summary>最後に読めなかった1行（<see cref="GripPortState.Unreadable"/> のとき）。</summary>
        public readonly string LastRejectedLine;

        public GripPortReport(string port, GripPortState state, string devicePath = null, string openError = null, bool openAccessDenied = false,
            long linesReceived = 0, string lastRejectedLine = null)
        {
            Port = port;
            State = state;
            DevicePath = devicePath;
            OpenError = openError;
            OpenAccessDenied = openAccessDenied;
            LinesReceived = linesReceived;
            LastRejectedLine = lastRejectedLine;
        }

        /// <summary>そのポートの機器の種類（日本語の短い説明）。分からなければ null。</summary>
        public string KindText => GripPortKind.Describe(DevicePath);

        /// <summary>1行の説明（例: 「COM3  Bluetooth（Arduino ではない）  開けたが何も届かない」）。</summary>
        public string ToText()
        {
            var builder = new StringBuilder(Port ?? "?");
            var kind = KindText;
            if (kind != null)
            {
                builder.Append("  ").Append(kind);
            }

            builder.Append("  ").Append(StateText());
            return builder.ToString();
        }

        private string StateText()
        {
            switch (State)
            {
                case GripPortState.Opening:
                    return "開いています…";
                case GripPortState.NotPresent:
                    return "このPCに無い";
                case GripPortState.OpenFailed:
                    return OpenAccessDenied
                        ? "開けない（ほかのソフトが使用中。Arduino IDE のシリアルモニタを閉じる）"
                        : "開けない" + (string.IsNullOrEmpty(OpenError) ? string.Empty : "（" + OpenError + "）");
                case GripPortState.Silent:
                    return "開けたが何も届かない";
                case GripPortState.Unreadable:
                    return $"届くが読めない（{LinesReceived}行）: {GripSerialDiagnostics.ToVisibleText(LastRejectedLine, 40)}";
                case GripPortState.Readable:
                    return "値が届いた";
                default:
                    return State.ToString();
            }
        }

        /// <summary>
        /// 全部のポートの様子から、いちばん大事な一言を返す（無ければ null）。
        /// 「Arduino らしい USB のポートが1つも無い」= PC が認識していない、のように、次に何を確かめればよいかを書く。
        /// </summary>
        public static string Hint(IReadOnlyList<GripPortReport> reports, int baudRate)
        {
            if (reports == null)
            {
                return null;
            }

            var present = 0;
            var knownKinds = 0;
            var usb = 0;
            var silent = 0;
            var unreadable = 0;
            var busy = 0;
            foreach (var report in reports)
            {
                if (report.State == GripPortState.NotPresent)
                {
                    continue;
                }

                present++;
                if (report.DevicePath != null)
                {
                    knownKinds++;
                    if (GripPortKind.IsUsb(report.DevicePath))
                    {
                        usb++;
                    }
                }

                if (report.State == GripPortState.Silent)
                {
                    silent++;
                }
                else if (report.State == GripPortState.Unreadable)
                {
                    unreadable++;
                }
                else if (report.State == GripPortState.OpenFailed && report.OpenAccessDenied)
                {
                    busy++;
                }
            }

            if (present == 0 || (knownKinds == present && usb == 0))
            {
                return "PC が Arduino を認識していません（USB のポートがありません）。充電専用でない USB ケーブルか、互換品なら CH340 のドライバを確かめてください。";
            }

            if (busy > 0)
            {
                return "ほかのソフトが使っているポートがあります。Arduino IDE のシリアルモニタ／プロッタを閉じてください。";
            }

            if (unreadable > 0)
            {
                return $"行は届いていますが読めません。Arduino の Serial.begin({baudRate}) と、1行が「512」か「512,300,100,50」の形か（最後にカンマを付けない・0〜1023）を確かめてください。";
            }

            if (silent > 0)
            {
                return "開けたのに何も届きません。Arduino にスケッチが書き込まれているか、Serial.println で送っているかを確かめてください。";
            }

            return null;
        }

        /// <summary>ログ用に、全部のポートを1行にまとめる（例: 「COM5（このPCに無い）, COM3（Bluetooth…・開けたが何も届かない）」）。</summary>
        public static string Summarize(IReadOnlyList<GripPortReport> reports)
        {
            if (reports == null || reports.Count == 0)
            {
                return "（COMポートがありません）";
            }

            var parts = new List<string>(reports.Count);
            foreach (var report in reports)
            {
                parts.Add(report.ToText());
            }

            return string.Join(" ／ ", parts);
        }
    }

    /// <summary>Windows がCOMポートに付けている機器の名前（<c>HKLM\HARDWARE\DEVICEMAP\SERIALCOMM</c> の値の名前）から、機器の種類を見分ける（純粋・テストあり）。</summary>
    public static class GripPortKind
    {
        /// <summary>機器の種類の短い説明。分からなければ <c>\Device\</c> を除いた名前、null なら null。</summary>
        public static string Describe(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                return null;
            }

            if (Contains(devicePath, "BthModem"))
            {
                return "Bluetooth（Arduino ではない）";
            }

            if (Contains(devicePath, "USBSER"))
            {
                return "USB（Arduino 純正・Leonardo など）";
            }

            if (Contains(devicePath, "CH34") || Contains(devicePath, "wch"))
            {
                return "USB（CH340・互換品の Arduino）";
            }

            if (Contains(devicePath, "VCP") || Contains(devicePath, "FTDI"))
            {
                return "USB（FTDI）";
            }

            if (Contains(devicePath, "Silab"))
            {
                return "USB（CP210x）";
            }

            if (Contains(devicePath, "Prolific") || Contains(devicePath, "PL2303"))
            {
                return "USB（PL2303）";
            }

            if (devicePath.StartsWith(@"\Device\Serial", StringComparison.OrdinalIgnoreCase))
            {
                return "PC 本体のシリアル（Arduino ではない）";
            }

            const string prefix = @"\Device\";
            return devicePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? devicePath.Substring(prefix.Length) : devicePath;
        }

        /// <summary>Arduino かもしれない機器か（Bluetooth・PC 本体のシリアル以外）。</summary>
        public static bool IsUsb(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                return false;
            }

            return !Contains(devicePath, "BthModem") && !devicePath.StartsWith(@"\Device\Serial", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
