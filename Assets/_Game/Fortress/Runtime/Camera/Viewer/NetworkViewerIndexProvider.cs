using MS2026.Fortress.Net;

namespace MS2026.Fortress.Cameras
{
    /// <summary>Host/Clientとして開始済みなら、その時に選んだ自分のプレイヤー番号を視点にする。</summary>
    public sealed class NetworkViewerIndexProvider : IViewerIndexProvider
    {
        public string Label => "通信(自分のプレイヤー番号)";

        public bool TryGetViewerIndex(out int viewer)
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            viewer = bootstrap != null ? bootstrap.LocalPlayerIndex : ViewerIndex.Overview;
            return ViewerIndex.IsPlayer(viewer);
        }
    }
}
