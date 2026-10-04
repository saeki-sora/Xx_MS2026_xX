using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Phase 0検証用の最小限の接続画面(OnGUI)。本番UIはPhase 1以降で作り直す。
    /// </summary>
    public sealed class FortressConnectUI : MonoBehaviour
    {
        public FortressNetworkBootstrap bootstrap;

        [Range(0, 3)]
        public int localPlayerIndex;

        public string joinAddress = "127.0.0.1";

        [Header("画面上の位置・幅(NetDebugOverlay等と被らないよう調整可能。高さは内容に合わせて自動)")]
        public Vector2 areaPosition = new(-340, 20);
        public float areaWidth = 320;

        [Tooltip("ONならareaPosition.xを画面右端からのオフセットとして扱う(負の値=右端から左へ)。OFFなら画面左端からの絶対座標。")]
        public bool anchorFromRightEdge = true;

        [Tooltip("この画面高さを基準に、高解像度の画面ではUIを拡大する(基準以下では等倍)。")]
        public float referenceScreenHeight = 1080;

        private readonly StringBuilder _statusBuilder = new();

        private void Start()
        {
            // D-Driveの手動接続画面(Development Buildで左下に出る)は、この画面と役割が重なるうえ、
            // そこから接続するとプレイヤー番号を載せずに接続して承認で弾かれるので隠す(D-Driveの公開フィールドVisible)。
            foreach (var overlay in FindObjectsByType<DDrive.Runtime.Net.NetManualConnectOverlay>(FindObjectsSortMode.None))
            {
                overlay.Visible = false;
            }

            if (bootstrap == null)
            {
                bootstrap = FortressNetworkBootstrap.Instance;
            }

            if (bootstrap == null)
            {
                return;
            }

            var launchArgs = bootstrap.LaunchArgs;
            if (launchArgs.PlayerIndex.HasValue)
            {
                localPlayerIndex = launchArgs.PlayerIndex.Value;
            }

            if (!string.IsNullOrEmpty(launchArgs.Address))
            {
                joinAddress = launchArgs.Address;
            }
        }

        private void OnGUI()
        {
            var scale = Mathf.Max(1f, Screen.height / referenceScreenHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var screenWidth = Screen.width / scale;
            var screenHeight = Screen.height / scale;
            var x = anchorFromRightEdge ? screenWidth + areaPosition.x : areaPosition.x;

            // 高さを固定すると内容(特にClient欄)が枠外にはみ出して描画されないため、画面下端までを上限にして中身の分だけ伸ばす。
            GUILayout.BeginArea(new Rect(x, areaPosition.y, areaWidth, screenHeight - areaPosition.y));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("D-Drive/NGO 接続 (Phase 0 検証用)");

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || bootstrap == null)
            {
                GUILayout.Label("NetworkManager または FortressNetworkBootstrap が見つかりません。");
            }
            else if (networkManager.IsListening)
            {
                DrawSessionStatus(networkManager);
            }
            else
            {
                DrawConnectForm();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawConnectForm()
        {
            if (!string.IsNullOrEmpty(bootstrap.LastDisconnectReason))
            {
                GUILayout.Label($"前回の切断理由: {bootstrap.LastDisconnectReason}");
            }

            GUILayout.Label("自分のプレイヤー番号");
            localPlayerIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(localPlayerIndex, 0, 3));
            GUILayout.Label($"プレイヤー{localPlayerIndex + 1}");

            if (GUILayout.Button("Hostとして開始"))
            {
                bootstrap.HostGame(localPlayerIndex);
            }

            GUILayout.Space(8);
            GUILayout.Label("接続先IP");
            joinAddress = GUILayout.TextField(joinAddress);

            if (GUILayout.Button("Clientとして接続"))
            {
                bootstrap.JoinGame(joinAddress, localPlayerIndex);
            }
        }

        private void DrawSessionStatus(NetworkManager networkManager)
        {
            var self = $"プレイヤー{bootstrap.LocalPlayerIndex + 1}";

            if (networkManager.IsServer)
            {
                _statusBuilder.Clear();
                foreach (var playerIndex in bootstrap.ClientIdToPlayerIndex.Values)
                {
                    _statusBuilder.Append(" P").Append(playerIndex + 1);
                }

                GUILayout.Label($"Host として稼働中({self})");
                GUILayout.Label($"参加中:{_statusBuilder}");
            }
            else
            {
                GUILayout.Label(networkManager.IsConnectedClient ? $"Client として接続済み({self})" : $"Client として接続待ち...({self})");
            }

            if (GUILayout.Button("切断"))
            {
                bootstrap.StopGame();
            }
        }
    }
}
