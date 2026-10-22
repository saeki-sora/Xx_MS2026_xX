using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>
    /// 握力の放送の宛先を作る（Unity を使わない。部屋さがし <c>LanDiscovery</c> と同じ考え方）。
    /// 宛先 = センサー1〜4の番号それぞれに「LAN 全体(255.255.255.255)」「このPCの各ネットワークのブロードキャスト」「このPC自身(127.0.0.1、同じPCで試すとき)」。
    /// 同じ物が重なって届くことがあるが、受け取る側が連番で捨てる。
    /// </summary>
    public static class GripRelayNetwork
    {
        /// <summary>放送の宛先の一覧（センサー4つ分）。ネットワークの一覧が取れなくても、LAN 全体とこのPC自身は必ず入る。</summary>
        public static List<IPEndPoint> BuildTargets(int basePort)
        {
            var addresses = new List<IPAddress> { IPAddress.Broadcast };
            foreach (var broadcast in DirectedBroadcastAddresses())
            {
                if (!addresses.Contains(broadcast))
                {
                    addresses.Add(broadcast);
                }
            }

            addresses.Add(IPAddress.Loopback);

            var targets = new List<IPEndPoint>();
            for (var sensor = GripRelayOptions.MinSensorNumber; sensor <= GripRelayOptions.MaxSensorNumber; sensor++)
            {
                var port = GripRelayOptions.PortForSensor(basePort, sensor);
                foreach (var address in addresses)
                {
                    targets.Add(new IPEndPoint(address, port));
                }
            }

            return targets;
        }

        /// <summary>このPCの、つながっている各ネットワーク（IPv4）のブロードキャストアドレス。</summary>
        public static List<IPAddress> DirectedBroadcastAddresses()
        {
            var result = new List<IPAddress>();
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
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                        {
                            continue;
                        }

                        IPAddress mask;
                        try
                        {
                            mask = unicast.IPv4Mask;
                        }
                        catch (NotImplementedException)
                        {
                            continue; // 取れない環境では LAN 全体(255.255.255.255)だけで送る。
                        }

                        var broadcast = Broadcast(unicast.Address, mask);
                        if (broadcast != null && !result.Contains(broadcast))
                        {
                            result.Add(broadcast);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 一覧が取れない（権限など）。LAN 全体への放送だけで続ける。
            }

            return result;
        }

        /// <summary>アドレスとネットマスクから、そのネットワークのブロードキャストアドレスを求める（マスクが無い・全部1なら null）。</summary>
        public static IPAddress Broadcast(IPAddress address, IPAddress mask)
        {
            if (address == null || mask == null)
            {
                return null;
            }

            var a = address.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4 || (m[0] == 255 && m[1] == 255 && m[2] == 255 && m[3] == 255))
            {
                return null;
            }

            var b = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                b[i] = (byte)(a[i] | ~m[i]);
            }

            return new IPAddress(b);
        }

        /// <summary>
        /// Windows では、相手がいない宛先へ送った後の受信で「既存の接続はリモート ホストに強制的に切断されました」の例外が出る。それを止める（SIO_UDP_CONNRESET）。
        /// Windows 以外・使えない環境では何もしない（受信側で読み飛ばす）。
        /// </summary>
        public static void DisableConnectionReset(Socket socket)
        {
            if (socket == null || Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            try
            {
                const int SioUdpConnReset = -1744830452;
                socket.IOControl(SioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception)
            {
                // 使えない環境では今まで通り。
            }
        }
    }
}
