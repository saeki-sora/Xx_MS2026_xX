using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// Play中にキー1つで「他のプレイヤーの視点」へ切り替えて確認するためのデバッグ機能。
    /// エディタとDevelopment Buildのみ有効(リリースビルドでは自動で無効になる)。
    /// 既定: F1〜F4=P1〜P4 / F5=全体 / F6=自動(通信・起動引数に戻す) / F7=構図ガイドの表示切替。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDebugHotkeys : MonoBehaviour
    {
        [Tooltip("P1〜P4の視点に切り替えるキー。")]
        public Key[] playerKeys = { Key.F1, Key.F2, Key.F3, Key.F4 };

        public Key overviewKey = Key.F5;

        [Tooltip("デバッグ上書きを解除して、通信・起動引数で決まる本来の視点に戻す。")]
        public Key autoKey = Key.F6;

        public Key toggleGuidesKey = Key.F7;

        [Tooltip("ガイド表示の切り替え先。未設定なら同じGameObjectから探す。")]
        public CameraGuideOverlay guideOverlay;

        private void Awake()
        {
            if (!DebugViewerOverride.IsAvailable)
            {
                enabled = false;
                return;
            }

            if (guideOverlay == null)
            {
                guideOverlay = GetComponent<CameraGuideOverlay>();
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (var i = 0; i < playerKeys.Length && i < ViewerIndex.PlayerCount; i++)
            {
                if (WasPressed(keyboard, playerKeys[i]))
                {
                    DebugViewerOverride.Set(i);
                }
            }

            if (WasPressed(keyboard, overviewKey))
            {
                DebugViewerOverride.Set(ViewerIndex.Overview);
            }

            if (WasPressed(keyboard, autoKey))
            {
                DebugViewerOverride.Clear();
            }

            if (guideOverlay != null && WasPressed(keyboard, toggleGuidesKey))
            {
                guideOverlay.showGuides = !guideOverlay.showGuides;
            }
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
