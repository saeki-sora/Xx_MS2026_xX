using System;
using System.Collections.Generic;
using DDrive.Runtime.Loop;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host/Client開始とPlayerIndexの割り当てをまとめる薄い層。
    /// 実際のNGO起動はDDriveRuntimeBootstrap.StartHost/StartClientに委譲する([14_networking.md] N-1)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FortressNetworkBootstrap : MonoBehaviour
    {
        [Tooltip("シーン上のDDriveRuntimeBootstrap。未設定ならシーンから自動検索する。")]
        public DDriveRuntimeBootstrap ddriveBootstrap;

        [Tooltip("Host/Client共通のポート番号。")]
        public ushort port = 7777;

        [Tooltip("1フレームに送受信できるパケット数の上限(通信層UnityTransportの設定)。既定の128だと、群衆を大量に同期している最中に" +
                 "フレームが重くなると受け取りきれずに捨てられ、再送待ちで遅れる。接続開始時にこの値を設定する。")]
        [Min(128)]
        public int packetQueueSize = 1024;

        public static FortressNetworkBootstrap Instance { get; private set; }

        private readonly Dictionary<ulong, int> _clientIdToPlayerIndex = new();
        private readonly HashSet<int> _takenPlayerIndices = new();

        // コールバックを登録したNetworkManager。NetworkManager.SingletonはNetworkManager自身のOnEnableで
        // セットされ、別GameObjectとのOnEnableの実行順は保証されないため、OnEnableでは登録しない
        // (Singletonがnullのまま登録漏れ → 全接続が承認待ちでタイムアウトする)。
        private NetworkManager _registeredNetworkManager;

        /// <summary>接続済みクライアントのPlayerIndex一覧。Phase 1のTurret所有権割り当てで使う。</summary>
        public IReadOnlyDictionary<ulong, int> ClientIdToPlayerIndex => _clientIdToPlayerIndex;

        /// <summary>最後にHost/Client開始したときの自分のプレイヤー番号(未開始なら-1)。</summary>
        public int LocalPlayerIndex { get; private set; } = -1;

        /// <summary>Client視点で最後にHostから切断された理由(承認拒否・ContentHash不一致等)。接続UIに表示する。</summary>
        public string LastDisconnectReason { get; private set; }

        /// <summary>起動時のコマンドライン引数(FortressLaunchArgs参照)。</summary>
        public FortressLaunchArgs LaunchArgs { get; private set; }

        private void Awake()
        {
            Instance = this;

            // フォーカスが外れたインスタンス(特にHost)が停止すると全員の通信が止まるため、Player Settingsに関わらず常に動かす。
            Application.runInBackground = true;

            LaunchArgs = FortressLaunchArgs.Parse(Environment.GetCommandLineArgs());

            if (ddriveBootstrap == null)
            {
                ddriveBootstrap = FindFirstObjectByType<DDriveRuntimeBootstrap>();
            }
        }

        private void Start()
        {
            EnsureCallbacksRegistered();

            if (LaunchArgs.Tile)
            {
                TileWindow(LaunchArgs.PlayerIndex ?? 0);
            }

            var launchPlayerIndex = LaunchArgs.PlayerIndex ?? 0;
            switch (LaunchArgs.Start)
            {
                case FortressLaunchArgs.StartMode.Host:
                    HostGame(launchPlayerIndex);
                    break;

                case FortressLaunchArgs.StartMode.Client:
                    JoinGame(string.IsNullOrEmpty(LaunchArgs.Address) ? "127.0.0.1" : LaunchArgs.Address, launchPlayerIndex);
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_registeredNetworkManager != null)
            {
                _registeredNetworkManager.ConnectionApprovalCallback -= HandleConnectionApproval;
                _registeredNetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                _registeredNetworkManager = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool HostGame(int localPlayerIndex)
        {
            if (!CanStart())
            {
                return false;
            }

            _takenPlayerIndices.Clear();
            _clientIdToPlayerIndex.Clear();
            LastDisconnectReason = null;
            LocalPlayerIndex = localPlayerIndex;
            NetworkManager.Singleton.NetworkConfig.ConnectionData = PlayerConnectionPayload.Encode(localPlayerIndex);
            return ddriveBootstrap.StartHost(port);
        }

        public bool JoinGame(string address, int localPlayerIndex)
        {
            if (!CanStart())
            {
                return false;
            }

            LastDisconnectReason = null;
            LocalPlayerIndex = localPlayerIndex;
            NetworkManager.Singleton.NetworkConfig.ConnectionData = PlayerConnectionPayload.Encode(localPlayerIndex);
            return ddriveBootstrap.StartClient(address, port);
        }

        public void StopGame()
        {
            if (ddriveBootstrap == null)
            {
                return;
            }

            ddriveBootstrap.StopNetworking();
            _takenPlayerIndices.Clear();
            _clientIdToPlayerIndex.Clear();
            LocalPlayerIndex = -1;
        }

        private bool CanStart()
        {
            if (NetworkManager.Singleton == null || ddriveBootstrap == null)
            {
                Debug.LogWarning("[Net] FortressNetworkBootstrap: NetworkManagerまたはDDriveRuntimeBootstrapが見つかりません。シーン構成を確認してください。");
                return false;
            }

            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[Net] FortressNetworkBootstrap: 既にHost/Clientとして開始済みです。");
                return false;
            }

            EnsureCallbacksRegistered();

            // シーンのNetworkManagerでチェックし忘れる(保存漏れ含む)と承認処理が丸ごとスキップされ、PlayerIndexの重複を弾けなくなる。
            // NGOはHost/Clientで設定値が違うと接続を拒否するため、両者とも必ずここを通して揃える。
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;

            if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                transport.MaxPacketQueueSize = packetQueueSize;
            }

            return true;
        }

        private void EnsureCallbacksRegistered()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager == _registeredNetworkManager)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback += HandleConnectionApproval;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _registeredNetworkManager = networkManager;
        }

        // 1台のPCで複数起動したとき、ウィンドウが重なって片方しか見えなくなるのを防ぐ(2x2の区画に並べる)。
        private static void TileWindow(int playerIndex)
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                return;
            }

            var display = Screen.mainWindowDisplayInfo;
            var area = display.workArea;
            var column = playerIndex % 2;
            var row = playerIndex / 2;
            var position = new Vector2Int(area.x + column * area.width / 2, area.y + row * area.height / 2);
            Screen.MoveMainWindowTo(display, position);
        }

        private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;

            if (!PlayerConnectionPayload.TryDecode(request.Payload, out var playerIndex))
            {
                response.Approved = false;
                response.Reason = "不正な接続データです。";
                return;
            }

            if (_takenPlayerIndices.Contains(playerIndex))
            {
                response.Approved = false;
                response.Reason = $"プレイヤー{playerIndex + 1}は既に接続中です。";
                return;
            }

            _takenPlayerIndices.Add(playerIndex);
            _clientIdToPlayerIndex[request.ClientNetworkId] = playerIndex;
            response.Approved = true;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && !networkManager.IsServer)
            {
                // Client視点: 自分がHostから切断された。NGOは承認拒否の理由をDisconnectReasonに入れる。
                LastDisconnectReason = string.IsNullOrEmpty(networkManager.DisconnectReason) ? "Hostから切断されました。" : networkManager.DisconnectReason;
                LocalPlayerIndex = -1;
                return;
            }

            if (_clientIdToPlayerIndex.TryGetValue(clientId, out var playerIndex))
            {
                _takenPlayerIndices.Remove(playerIndex);
                _clientIdToPlayerIndex.Remove(clientId);
            }
        }
    }
}
