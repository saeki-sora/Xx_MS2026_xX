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

        /// <summary>Host(優先度つきの補正): 今の補正の通信量の枠(Client1人あたり、一番多い人の値、KB/秒)。</summary>
        public static float SwarmCorrectionBudgetKBs;

        /// <summary>往復時間(ミリ秒)。Host: 一番遅いClient / Client: Hostとの間。測れなければ負。</summary>
        public static float SwarmRttMs = -1f;

        /// <summary>Host(優先度つきの補正): 回線の混み具合で通信量を下げた回数(全Clientの累計)。</summary>
        public static int SwarmCorrectionBackoffs;

        /// <summary>写真方式: 送った/受け取った写真の枚数(累計)。</summary>
        public static long SwarmSnapshotsSent;

        public static long SwarmSnapshotsReceived;

        /// <summary>Host(写真方式): 直前の写真1枚の大きさ(バイト)。</summary>
        public static int SwarmSnapshotBytes;

        /// <summary>Host(写真方式): 直前の写真で、続いている敵1体の位置に使ったビット数(平均)。</summary>
        public static float SwarmSnapshotBitsPerAgent;

        /// <summary>Client(写真方式): 復元がHostと合わず、全員分の写真で立て直した回数(累計)。0であるべき。</summary>
        public static int SwarmSnapshotResyncs;

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
            SwarmCorrectionBudgetKBs = 0f;
            SwarmRttMs = -1f;
            SwarmCorrectionBackoffs = 0;
            SwarmSnapshotsSent = SwarmSnapshotsReceived = 0;
            SwarmSnapshotBytes = 0;
            SwarmSnapshotBitsPerAgent = 0f;
            SwarmSnapshotResyncs = 0;
        }
    }
}
