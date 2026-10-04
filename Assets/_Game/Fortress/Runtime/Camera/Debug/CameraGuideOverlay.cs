using MS2026.Fortress.Hud;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// ゲーム画面の上に構図ガイド(安全域・中心十字・三分割線)と「今誰の視点か」を描く。
    /// エディタとDevelopment Buildのみ。リリースビルドでは何もしない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraGuideOverlay : MonoBehaviour
    {
        [Tooltip("ガイドを表示する。Play中は F7(既定) で切り替えられる。")]
        public bool showGuides;

        [Tooltip("視点の持ち主と情報源を画面左上に表示する(デバッグ上書き中は常に表示)。")]
        public bool showViewerLabel = true;

        [Tooltip("ガイドの設定元。未設定ならシーンで有効なリグを使う。")]
        public FortressCameraRig rig;

        private static readonly Color SafeAreaColor = new Color(1f, 0.85f, 0.2f, 0.8f);
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.35f);

        private GUIStyle _labelStyle;

        private FortressCameraRig Rig => rig != null ? rig : FortressCameraRig.Active;

        private void Awake()
        {
            // GUILayoutを使わないので、OnGUIの度のレイアウト準備(=毎フレームのGC)を止める。
            useGUILayout = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            var currentRig = Rig;
            if (currentRig == null)
            {
                return;
            }

            if (showGuides)
            {
                DrawGuides(currentRig.guides, new Rect(0f, 0f, Screen.width, Screen.height));
            }

            if (showViewerLabel && (showGuides || DebugViewerOverride.IsActive))
            {
                DrawViewerLabel(currentRig);
            }
        }
#endif

        private static void DrawGuides(CameraGuideSettings guides, Rect screen)
        {
            var safe = guides.GetSafeRect(screen);
            DrawFrame(safe, SafeAreaColor, 2f);

            if (guides.showCenterCross)
            {
                var center = screen.center;
                DrawLine(new Rect(center.x - 20f, center.y - 1f, 40f, 2f), LineColor);
                DrawLine(new Rect(center.x - 1f, center.y - 20f, 2f, 40f), LineColor);
            }

            if (guides.showThirds)
            {
                for (var i = 1; i <= 2; i++)
                {
                    DrawLine(new Rect(screen.x + screen.width * i / 3f, screen.y, 1f, screen.height), LineColor);
                    DrawLine(new Rect(screen.x, screen.y + screen.height * i / 3f, screen.width, 1f), LineColor);
                }
            }
        }

        private void DrawViewerLabel(FortressCameraRig currentRig)
        {
            _labelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleLeft };

            var viewer = currentRig.CurrentViewer;
            var text = $"視点: {ViewerIndex.LongLabel(viewer)}  ({currentRig.ViewerSourceLabel})  F1-F4:P1-P4 F5:全体 F6:自動 F7:ガイド";
            var previous = GUI.color;
            GUI.color = ViewerIndex.Color(viewer);
            var width = Mathf.Min(620f, Screen.width - DebugOverlayLayout.Margin * 2f);
            GUI.Box(DebugOverlayLayout.TopLeft(DebugOverlaySlot.CameraViewerLabel, width, 28f), text, _labelStyle);
            GUI.color = previous;
        }

        private static void DrawFrame(Rect rect, Color color, float thickness)
        {
            DrawLine(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawLine(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawLine(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawLine(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private static void DrawLine(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
