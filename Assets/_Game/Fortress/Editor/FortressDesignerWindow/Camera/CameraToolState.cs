using MS2026.Fortress.Cameras;
using UnityEditor;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// カメラタブの表示状態(編集中の視点・表示切替など)。エディタを閉じるまで保持する(SessionState)。
    /// パネル同士はこのクラス経由で状態を共有し、互いを直接参照しない。
    /// </summary>
    public static class CameraToolState
    {
        private const string Prefix = "MS2026.Fortress.CameraTool.";

        /// <summary>カメラタブが画面に出ているか。Sceneの枠やハンドルはタブ表示中だけ出す(他タブのScene操作と干渉させない)。</summary>
        public static bool TabVisible { get; set; }

        /// <summary>コピーした視点。「貼り付け」で別の視点に移せる。</summary>
        public static CameraViewSettings? Clipboard { get; set; }

        public static int SelectedViewer
        {
            get => SessionState.GetInt(Prefix + "SelectedViewer", 0);
            set => SessionState.SetInt(Prefix + "SelectedViewer", ViewerIndex.IsValid(value) ? value : ViewerIndex.Overview);
        }

        public static bool ShowSceneFrames
        {
            get => SessionState.GetBool(Prefix + "ShowSceneFrames", true);
            set => SessionState.SetBool(Prefix + "ShowSceneFrames", value);
        }

        public static bool ShowOnlySelectedFrame
        {
            get => SessionState.GetBool(Prefix + "ShowOnlySelectedFrame", false);
            set => SessionState.SetBool(Prefix + "ShowOnlySelectedFrame", value);
        }

        public static bool ShowSceneHandles
        {
            get => SessionState.GetBool(Prefix + "ShowSceneHandles", true);
            set => SessionState.SetBool(Prefix + "ShowSceneHandles", value);
        }

        public static bool ShowSafeAreaInScene
        {
            get => SessionState.GetBool(Prefix + "ShowSafeAreaInScene", true);
            set => SessionState.SetBool(Prefix + "ShowSafeAreaInScene", value);
        }

        /// <summary>Play Mode外で、編集中の視点をGameビュー(本物のカメラ)にも反映する。</summary>
        public static bool SyncGameView
        {
            get => SessionState.GetBool(Prefix + "SyncGameView", true);
            set => SessionState.SetBool(Prefix + "SyncGameView", value);
        }

        public static bool AutoRefreshPreview
        {
            get => SessionState.GetBool(Prefix + "AutoRefreshPreview", true);
            set => SessionState.SetBool(Prefix + "AutoRefreshPreview", value);
        }

        public static bool ShowPreviewGuides
        {
            get => SessionState.GetBool(Prefix + "ShowPreviewGuides", true);
            set => SessionState.SetBool(Prefix + "ShowPreviewGuides", value);
        }

        public static bool SnapRollTo90
        {
            get => SessionState.GetBool(Prefix + "SnapRollTo90", true);
            set => SessionState.SetBool(Prefix + "SnapRollTo90", value);
        }

        public static bool GetFoldout(string key, bool defaultValue = true) => SessionState.GetBool(Prefix + "Foldout." + key, defaultValue);

        public static void SetFoldout(string key, bool value) => SessionState.SetBool(Prefix + "Foldout." + key, value);
    }
}
