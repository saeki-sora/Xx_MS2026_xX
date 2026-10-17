using System;
using DDrive.Runtime.Net;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// 次のデバッグ表示は、デバッグ表示を出すときでも最初は隠し、<see cref="OverlayToggleKey"/>（F9）でまとめて出し入れする
    /// （2026-10-17、ユーザー決定「一旦消して、邪魔にならないキーでオンオフ」）: D-Drive の通信状態 NetDebugOverlay（左上）・
    /// 群衆の計測（SwarmHud、左上。敵の数・FPS・処理時間）・右上の接続画面 FortressConnectUI。カメラの視点表示は今まで通り F7。
    /// </summary>
    public static class DebugUi
    {
        /// <summary>ビルドしたゲームでもデバッグ表示を出す起動オプション。</summary>
        public const string ShowArgument = "-debug-ui";

        /// <summary>デバッグ表示（左上の D-Drive の通信状態と群衆の計測・右上の接続画面）を出し入れするキー。F1〜F8 はほかで使っているので F9。</summary>
        public const Key OverlayToggleKey = Key.F9;

        private static bool? _visible;

        /// <summary>F9 で出し入れするデバッグ表示を出すか。最初は出さない（<see cref="Visible"/> が false なら常に出さない）。</summary>
        public static bool OverlaysShown { get; set; }

        /// <summary>デバッグ表示を出してよいか（エディタの中、または起動オプション -debug-ui）。</summary>
        public static bool Visible => _visible ??= Application.isEditor || HasShowArgument();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _visible = null;
            OverlaysShown = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHider()
        {
            if (!Visible)
            {
                // Development Build のときに出る、エラーの開発用コンソール（画面左下の赤い窓）も出さない。
                Debug.developerConsoleEnabled = false;
                Debug.developerConsoleVisible = false;
            }

            // エディタの中でも置く（D-Drive の通信状態を最初は隠し、F9 で出し入れするため）。

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
    /// D-Drive のデバッグ表示を隠し続ける（通信を始めたときなどに後から作られるので、ときどき確かめる）。
    /// ・通信状態 NetDebugOverlay: <see cref="DebugUi.OverlaysShown"/>（F9 で切り替え）が false の間は隠す（エディタの中でも）
    /// ・手動接続 NetManualConnectOverlay: デバッグ表示を出さないとき（ビルド）だけ隠す（エディタでは FortressConnectUI が隠している）
    /// 見えなくするだけでなく部品ごと止める（OnGUI が毎フレーム呼ばれる負荷も無くす）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class DebugUiHider : MonoBehaviour
    {
        private const float CheckInterval = 0.5f;

        private float _nextCheck;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (DebugUi.Visible && keyboard != null && keyboard[DebugUi.OverlayToggleKey].wasPressedThisFrame)
            {
                DebugUi.OverlaysShown = !DebugUi.OverlaysShown;
                _nextCheck = 0f; // すぐ反映する
            }

            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + CheckInterval;
            var showNet = DebugUi.Visible && DebugUi.OverlaysShown;
            foreach (var overlay in FindObjectsByType<NetDebugOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                overlay.Visible = showNet;
                overlay.enabled = showNet;
            }

            if (DebugUi.Visible)
            {
                return;
            }

            foreach (var overlay in FindObjectsByType<NetManualConnectOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                overlay.Visible = false;
                overlay.enabled = false;
            }
        }
    }
}
