using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「指定した視点でPlayを始める」仕組み。Play開始直後に <see cref="DebugViewerOverride"/> を設定し、
    /// カメラリグを補間なしでその視点へ合わせる。ボタンからの1回限りの指定と、通常のPlayでも効く「固定」の2通り。
    /// 状態はSessionStateに持つので、ドメインリロードをまたいでも消えず、Unityを閉じると元に戻る。
    /// </summary>
    [InitializeOnLoad]
    public static class CameraPlayAsViewer
    {
        private const string Prefix = "MS2026.Fortress.CameraTool.PlayAs.";
        private const string PendingKey = Prefix + "Pending";
        private const string StickyKey = Prefix + "Sticky";
        private const string StickyViewerKey = Prefix + "StickyViewer";
        private const int None = int.MinValue;

        static CameraPlayAsViewer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>ONの間は、通常のPlay(▶ボタン・Ctrl+P)でも <see cref="StickyViewer"/> の視点で始める。</summary>
        public static bool Sticky
        {
            get => SessionState.GetBool(StickyKey, false);
            set => SessionState.SetBool(StickyKey, value);
        }

        /// <summary>固定するときの視点。最後に「▶ Pnでプレイ」を押した視点が入る。</summary>
        public static int StickyViewer
        {
            get => SessionState.GetInt(StickyViewerKey, 0);
            set => SessionState.SetInt(StickyViewerKey, ViewerIndex.IsValid(value) ? value : 0);
        }

        /// <summary>この視点でPlayを始める。固定の視点もこの視点に合わせる。</summary>
        public static void PlayAs(int viewer)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !ViewerIndex.IsValid(viewer))
            {
                return;
            }

            StickyViewer = viewer;
            SessionState.SetInt(PendingKey, viewer);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (change != PlayModeStateChange.EnteredPlayMode)
            {
                // Playに入れなかった(コンパイルエラー等)ときに、次の通常Playへ持ち越さない。
                SessionState.EraseInt(PendingKey);
                return;
            }

            var viewer = SessionState.GetInt(PendingKey, None);
            SessionState.EraseInt(PendingKey);
            if (viewer == None && Sticky)
            {
                viewer = StickyViewer;
            }

            if (!ViewerIndex.IsValid(viewer))
            {
                return;
            }

            DebugViewerOverride.Set(viewer);
            var rig = FortressCameraRig.Active;
            if (rig != null)
            {
                // 最初のフレームが全体視点で描かれていても、補間せずに指定の視点へ合わせる。
                rig.SnapToTarget();
            }

            Debug.Log($"[カメラ] {ViewerIndex.LongLabel(viewer)}の視点でPlayを始めました（要塞デザイナー › カメラ「この視点でプレイ」）。");
        }
    }
}
