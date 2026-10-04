using System;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// デバッグ用に「この画面を誰の視点で見るか」を一時的に上書きする。エディタのカメラタブや
    /// Development Buildのホットキー(F1〜F5)から操作する。リリースビルドでは常に無効。
    /// </summary>
    public static class DebugViewerOverride
    {
        // constにすると、リリースビルドで到達不能コードの警告が出るためstatic readonlyにする。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static readonly bool IsAvailable = true;
#else
        public static readonly bool IsAvailable = false;
#endif

        private static int? _viewer;

        public static event Action Changed;

        public static bool IsActive => IsAvailable && _viewer.HasValue;

        public static int Viewer => _viewer ?? ViewerIndex.Overview;

        public static void Set(int viewer)
        {
            if (!IsAvailable || !ViewerIndex.IsValid(viewer) || _viewer == viewer)
            {
                return;
            }

            _viewer = viewer;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (!_viewer.HasValue)
            {
                return;
            }

            _viewer = null;
            Changed?.Invoke();
        }

        /// <summary>ドメインリロード無効時にPlay Modeをまたいで上書きが残らないようにする。</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _viewer = null;
        }
    }

    /// <summary><see cref="DebugViewerOverride"/> を視点の情報源として扱うアダプタ。</summary>
    public sealed class DebugViewerIndexProvider : IViewerIndexProvider
    {
        public string Label => "デバッグ上書き";

        public bool TryGetViewerIndex(out int viewer)
        {
            viewer = DebugViewerOverride.Viewer;
            return DebugViewerOverride.IsActive;
        }
    }
}
