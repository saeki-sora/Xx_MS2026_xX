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


        /// <summary>Host: 全員分の写真(キーフレーム)を頼んできて、まだ送っていないClientの数。</summary>
        public static int SwarmFullStateTransfers;


        /// <summary>Client: 全員分の写真(キーフレーム)を受け取り終えたか。</summary>
        public static bool SwarmFullStateReceived;






        /// <summary>写真方式: 送った/受け取った写真の枚数(累計)。</summary>
        public static long SwarmSnapshotsSent;

        public static long SwarmSnapshotsReceived;

        /// <summary>Host(写真方式): 直前の写真1枚の大きさ(バイト)。</summary>
        public static int SwarmSnapshotBytes;

        /// <summary>Host(写真方式): 直前の写真で、続いている敵1体の位置に使ったビット数(平均)。</summary>
        public static float SwarmSnapshotBitsPerAgent;

        /// <summary>Client(写真方式): 復元がHostと合わず、全員分の写真で立て直した回数(累計)。0であるべき。</summary>
        public static int SwarmSnapshotResyncs;

        /// <summary>Host(写真方式): 今の1秒あたりの写真の枚数。</summary>
        public static float SwarmSnapshotRate;

        /// <summary>写真方式: 写真がいつもよりどれだけ遅れて届いたか(ミリ秒)。Host: 報告の一番大きい物(無ければ負) / Client: 直近の報告値。</summary>
        public static float SwarmSnapshotLagMs = -1f;

        /// <summary>Host(写真方式): 今の位置の細かさ(差を丸める単位 2^shift、1/500ワールド単位)。</summary>
        public static int SwarmSnapshotShift;

        /// <summary>Host(写真方式): 遅れのために位置を粗くした回数(累計)。</summary>
        public static int SwarmSnapshotQualityStepUps;

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
            SwarmEventsSent = SwarmEventsReceived = 0;
            SwarmFullStateTransfers = 0;
            SwarmFullStateReceived = false;
            SwarmSnapshotsSent = SwarmSnapshotsReceived = 0;
            SwarmSnapshotBytes = 0;
            SwarmSnapshotBitsPerAgent = 0f;
            SwarmSnapshotResyncs = 0;
            SwarmSnapshotRate = 0f;
            SwarmSnapshotLagMs = -1f;
            SwarmSnapshotShift = 0;
            SwarmSnapshotQualityStepUps = 0;
        }
    }
}
