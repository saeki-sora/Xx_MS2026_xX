// SerialGripTransport と同じく、実機通信を有効にしたとき（MS2026_GRIP_SERIAL_ENABLED）だけコンパイルする。
#if MS2026_GRIP_SERIAL_ENABLED

using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// Windows が COM ポートごとに付けている機器の名前（<c>COM3 → \Device\BthModem0</c> など）を読む。
    /// 「COM3 は Bluetooth で Arduino ではない」のように、F8 の表示で機器の種類を出すため（<see cref="GripPortKind"/>）。
    /// Windows 以外・読めないときは空を返す（ポートさがしそのものには使わない）。
    /// </summary>
    public static class SerialPortDevicePaths
    {
        public static Dictionary<string, string> Read()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return result;
            }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
                {
                    if (key == null)
                    {
                        return result;
                    }

                    foreach (var devicePath in key.GetValueNames())
                    {
                        if (key.GetValue(devicePath) is string port && !string.IsNullOrWhiteSpace(port))
                        {
                            result[port.Trim()] = devicePath;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 種類が分からないだけ。ポートさがしは続けられる。
            }

            return result;
        }
    }
}

#endif // MS2026_GRIP_SERIAL_ENABLED
