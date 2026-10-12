using System;
using System.Collections.Generic;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機のCOMポートを自動でさがすときに試すポートの順番を決める（I/Oに依存しないのでテストできる）。
    /// 設定したポートを先頭に、PCにあるほかのポートを後ろに並べる（大文字・小文字の違いと重複は除く）。
    /// </summary>
    public static class GripPortCandidates
    {
        public static List<string> Build(string configuredPort, IEnumerable<string> availablePorts, bool includeOthers)
        {
            var result = new List<string>();
            Add(result, configuredPort);
            if (includeOthers && availablePorts != null)
            {
                foreach (var port in availablePorts)
                {
                    Add(result, port);
                }
            }

            return result;
        }

        private static void Add(List<string> list, string port)
        {
            port = port?.Trim();
            if (string.IsNullOrEmpty(port))
            {
                return;
            }

            foreach (var existing in list)
            {
                if (string.Equals(existing, port, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            list.Add(port);
        }
    }
}
