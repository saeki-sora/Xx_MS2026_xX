using Unity.Netcode;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 「このPCがゲームの結果を決めてよいか」の共通判定。ダメージ適用など結果に影響する処理はこれで囲む。
    /// オフライン(通信していない)ときは常に自分が決める。
    /// </summary>
    public static class FortressNet
    {
        /// <summary>Host/Clientとして通信中(接続待ちを含む)。</summary>
        public static bool IsNetworked
        {
            get
            {
                var networkManager = NetworkManager.Singleton;
                return networkManager != null && networkManager.IsListening;
            }
        }

        /// <summary>オフライン、またはHost。falseならClientなので、結果はHostから受け取るだけにする。</summary>
        public static bool HasSimulationAuthority
        {
            get
            {
                var networkManager = NetworkManager.Singleton;
                return networkManager == null || !networkManager.IsListening || networkManager.IsServer;
            }
        }
    }
}
