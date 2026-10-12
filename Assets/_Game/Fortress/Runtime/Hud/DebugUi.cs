using System;
using DDrive.Runtime.Net;
using UnityEngine;

namespace MS2026.Fortress.Hud
{
    /// <summary>
    /// デバッグ表示（画面に直接描く計測・通信状態・接続画面など）を出すかどうか（2026-10-13、ユーザー決定）。
    /// ・Unity エディタの中（普段の作業）: 今まで通り出す
    /// ・ビルドしたゲーム: 出さない（Development Build でも出さない。会場で見せないため）
    /// ・ビルドでも、起動オプション <see cref="ShowArgument"/>（-debug-ui）を付ければ出す（Tools/NetTest の bat は付けている）
    /// デバッグ表示を作るときは、描く前に <see cref="Visible"/> を見ること。
    /// D-Drive の表示（通信状態 NetDebugOverlay・手動接続 NetManualConnectOverlay）と Unity の開発用コンソール（エラーの赤い窓）も、
    /// 出さないときはここで隠す（D-Drive のパッケージは変えない）。
    /// </summary>
    public static class DebugUi
    {
        /// <summary>ビルドしたゲームでもデバッグ表示を出す起動オプション。</summary>
        public const string ShowArgument = "-debug-ui";

        private static bool? _visible;

        /// <summary>デバッグ表示を出してよいか（エディタの中、または起動オプション -debug-ui）。</summary>
        public static bool Visible => _visible ??= Application.isEditor || HasShowArgument();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _visible = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHider()
        {
            if (Visible)
            {
                return;
            }

            // Development Build のときに出る、エラーの開発用コンソール（画面左下の赤い窓）も出さない。
            Debug.developerConsoleEnabled = false;
            Debug.developerConsoleVisible = false;

            var go = new GameObject("[Fortress] DebugUiHider");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DebugUiHider>();
        }

        private static bool HasShowArgument()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, ShowArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// ビルドで D-Drive のデバッグ表示を隠し続ける（通信を始めたときなどに後から作られるので、ときどき確かめる）。
    /// 見えなくするだけでなく部品ごと止める（OnGUI が毎フレーム呼ばれる負荷も無くす）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class DebugUiHider : MonoBehaviour
    {
        private const float CheckInterval = 0.5f;

        private float _nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + CheckInterval;
            foreach (var overlay in FindObjectsByType<NetDebugOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                overlay.Visible = false;
                overlay.enabled = false;
            }

            foreach (var overlay in FindObjectsByType<NetManualConnectOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                overlay.Visible = false;
                overlay.enabled = false;
            }
        }
    }
}
