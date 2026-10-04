using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 同期部品が送受信した量などの計測値(累計)。画面の計測表示(SwarmHud)と負荷計測のログ(NetBenchmarkRunner)が読む。
    /// 1秒あたりの量は、読む側が前回の値との差から求める(読む側ごとに間隔が違うため)。
    /// バイト数は同期部品が送ったデータ本体の大きさ(通信の見出し等は含まない)。
    /// </summary>
    public static class NetTrafficStats
    {
        public static long BytesSent;
        public static long BytesReceived;

        public static long SwarmEventsSent;
        public static long SwarmEventsReceived;
        public static long SwarmCorrectionsSent;
        public static long SwarmCorrectionsReceived;

        /// <summary>Host: まだ送っていない群衆の出現/消滅の数。</summary>
        public static int SwarmEventBacklog;

        /// <summary>Host: 途中参加者へ送っている最中の全体の状態の数。</summary>
        public static int SwarmFullStateTransfers;

        /// <summary>Client: 全体の状態を受け取り終わるまで貯めている出現/消滅の数。</summary>
        public static int SwarmBufferedEvents;

        /// <summary>Client: 全体の状態を受け取り終えたか。</summary>
        public static bool SwarmFullStateReceived;

        /// <summary>Host: 今の補正の間隔(全員を1回ずつ補正するのにかける秒数)。</summary>
        public static float SwarmCorrectionCycle;

        /// <summary>Host: Clientから報告されたズレ(一番大きい物)。報告が無ければ負。</summary>
        public static float SwarmReportedError = -1f;

        public static void Sent(long bytes)
        {
            BytesSent += bytes;
        }

        public static void Received(long bytes)
        {
            BytesReceived += bytes;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            BytesSent = BytesReceived = 0;
            SwarmEventsSent = SwarmEventsReceived = SwarmCorrectionsSent = SwarmCorrectionsReceived = 0;
            SwarmEventBacklog = SwarmFullStateTransfers = SwarmBufferedEvents = 0;
            SwarmFullStateReceived = false;
            SwarmCorrectionCycle = 0f;
            SwarmReportedError = -1f;
        }
    }
}
