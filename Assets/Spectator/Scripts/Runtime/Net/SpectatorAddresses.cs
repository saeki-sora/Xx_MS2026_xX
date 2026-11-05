using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MS2026.Spectator
{
    /// <summary>このPCの LAN の住所（スマホが開く http://住所:番号/ を記録に出すため）。</summary>
    public static class SpectatorAddresses
    {
        public static List<string> LanIPv4()
        {
            var result = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        var address = unicast.Address;
                        if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        {
                            var text = address.ToString();
                            if (!text.StartsWith("169.254.", StringComparison.Ordinal) && !result.Contains(text))
                            {
                                result.Add(text);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 住所が取れない環境でもゲームは続ける
            }

            return result;
        }

        public static string Urls(int port)
        {
            var list = LanIPv4();
            if (list.Count == 0)
            {
                return "http://localhost:" + port + "/";
            }

            for (var i = 0; i < list.Count; i++)
            {
                list[i] = "http://" + list[i] + ":" + port + "/";
            }

            return string.Join("  ", list);
        }
    }

    /// <summary>出口の位置を、コアを中心・いちばん遠い出口を 1 とした -1〜1 の地図に直す（y は上が +）。</summary>
    public static class SpectatorMapProjection
    {
        public static void Project(IReadOnlyList<float> xs, IReadOnlyList<float> ys, float centerX, float centerY, float[] outX, float[] outY)
        {
            var n = Math.Min(xs.Count, ys.Count);
            var far = 0f;
            for (var i = 0; i < n; i++)
            {
                var dx = xs[i] - centerX;
                var dy = ys[i] - centerY;
                far = Math.Max(far, (float)Math.Sqrt(dx * dx + dy * dy));
            }

            var scale = far > 1e-4f ? 1f / far : 1f;
            for (var i = 0; i < n; i++)
            {
                outX[i] = (xs[i] - centerX) * scale;
                outY[i] = (ys[i] - centerY) * scale;
            }
        }
    }
}
