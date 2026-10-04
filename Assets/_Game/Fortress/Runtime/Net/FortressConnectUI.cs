using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Phase 0検証用の最小限の接続画面(OnGUI)。本番UIはPhase 1以降で作り直す。
    /// 毎フレームGCを出さないよう、GUILayout(呼ぶたびに内部でオブジェクトを作る)は使わず位置を自分で計算し、
    /// 表示する文字列は変化したときだけ作り直す(2026-10-04 段階3)。
    /// 入力欄(GUI.TextField)とスライダーも描くたびにメモリを確保する(計測で1フレームあたり約16回)ため、
    /// プレイヤー番号は4つのボタン、IPは普段は文字で表示し「変更」を押したときだけ入力欄を出す。
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

        private const float Padding = 6f;
        private const float LineHeight = 22f;
        private const float Spacing = 4f;
        private const string Title = "D-Drive/NGO 接続 (Phase 0 検証用)";

        private static readonly string[] PlayerLabels = { "プレイヤー1", "プレイヤー2", "プレイヤー3", "プレイヤー4" };
        private static readonly string[] PlayerButtons = { "P1", "P2", "P3", "P4" };
        private const float SmallButtonWidth = 60f;

        private readonly StringBuilder _builder = new();

        // IPの表示文字列(アドレスが変わったときだけ作り直す)と、入力欄を出しているか。
        private string _addressLabelSource;
        private string _addressLabel;
        private bool _editingAddress;
        private readonly GUIContent _reasonContent = new();
        private string _reasonSource;
        private float _reasonHeight;

        // 接続中の表示文字列(状態が変わったときだけ作り直す)。
        private int _statusKey = int.MinValue;
        private string _statusLine;
        private string _participantsLine;

        private void Awake()
        {
            // GUILayoutを使わないので、OnGUIの度のレイアウト準備(=毎フレームのGC)を止める。
            useGUILayout = false;
        }

        private void Start()
        {
            // D-Driveの手動接続画面(Development Buildで左下に出る)は、この画面と役割が重なるうえ、
            // そこから接続するとプレイヤー番号を載せずに接続して承認で弾かれるので隠す(D-Driveの公開フィールドVisible)。
            // 隠してもOnGUI自体は毎フレーム呼ばれてGCの元になるので、部品ごと止める。
            foreach (var overlay in FindObjectsByType<DDrive.Runtime.Net.NetManualConnectOverlay>(FindObjectsSortMode.None))
            {
                overlay.Visible = false;
                overlay.enabled = false;
            }

            // D-Driveの通信状態表示(Development Buildのみ)もGUILayoutを使わない(GUI.Boxだけ)ので、レイアウト準備を止める。
            foreach (var overlay in FindObjectsByType<DDrive.Runtime.Net.NetDebugOverlay>(FindObjectsSortMode.None))
            {
                overlay.useGUILayout = false;
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
            var x = anchorFromRightEdge ? screenWidth + areaPosition.x : areaPosition.x;
            var innerWidth = areaWidth - Padding * 2f;

            var networkManager = NetworkManager.Singleton;
            var state = networkManager == null || bootstrap == null ? 0 : networkManager.IsListening ? 1 : 2;

            // 先に高さを求めて枠を描き、その中に上から並べる(GUILayoutを使わない)。
            var height = Padding * 2f + LineHeight + Spacing + ContentHeight(state, innerWidth);
            GUI.Box(new Rect(x, areaPosition.y, areaWidth, height), GUIContent.none);

            var y = areaPosition.y + Padding;
            GUI.Label(Row(x, ref y, innerWidth, LineHeight), Title);
            y += Spacing;

            switch (state)
            {
                case 0:
                    GUI.Label(Row(x, ref y, innerWidth, LineHeight), "NetworkManager または FortressNetworkBootstrap が見つかりません。");
                    break;
                case 1:
                    DrawSessionStatus(networkManager, x, ref y, innerWidth);
                    break;
                default:
                    DrawConnectForm(x, ref y, innerWidth);
                    break;
            }
        }

        private float ContentHeight(int state, float width)
        {
            switch (state)
            {
                case 0:
                    return LineHeight;
                case 1:
                    // 状態 / (Hostなら)参加中 / 切断ボタン
                    var hostLines = NetworkManager.Singleton.IsServer ? 2 : 1;
                    return (LineHeight + Spacing) * (hostLines + 1);
                default:
                    // (切断理由) / 番号ラベル / P1〜P4 / Hostボタン / 余白 / IP表示+変更 / (入力欄) / Clientボタン
                    return ReasonHeight(width) + (LineHeight + Spacing) * (_editingAddress ? 6 : 5) + 8f;
            }
        }

        private float ReasonHeight(float width)
        {
            var reason = bootstrap.LastDisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                return 0f;
            }

            if (!ReferenceEquals(reason, _reasonSource))
            {
                _reasonSource = reason;
                _reasonContent.text = "前回の切断理由: " + reason;
                _reasonHeight = -1f;
            }

            if (_reasonHeight < 0f)
            {
                _reasonHeight = Mathf.Max(LineHeight, GUI.skin.label.CalcHeight(_reasonContent, width)) + Spacing;
            }

            return _reasonHeight;
        }

        private void DrawConnectForm(float x, ref float y, float width)
        {
            var reasonHeight = ReasonHeight(width);
            if (reasonHeight > 0f)
            {
                GUI.Label(Row(x, ref y, width, reasonHeight - Spacing), _reasonContent);
            }

            GUI.Label(Row(x, ref y, width, LineHeight), "自分のプレイヤー番号");
            var row = Row(x, ref y, width, LineHeight);
            var buttonWidth = (width - Spacing * 3f) / 4f;
            for (var i = 0; i < PlayerButtons.Length; i++)
            {
                var rect = new Rect(row.x + i * (buttonWidth + Spacing), row.y, buttonWidth, row.height);
                if (GUI.Toggle(rect, localPlayerIndex == i, PlayerButtons[i], GUI.skin.button))
                {
                    localPlayerIndex = i;
                }
            }

            if (GUI.Button(Row(x, ref y, width, LineHeight), "Hostとして開始"))
            {
                bootstrap.HostGame(localPlayerIndex);
            }

            y += 8f;
            row = Row(x, ref y, width, LineHeight);
            var labelRect = new Rect(row.x, row.y, row.width - SmallButtonWidth - Spacing, row.height);
            var buttonRect = new Rect(row.xMax - SmallButtonWidth, row.y, SmallButtonWidth, row.height);
            GUI.Label(labelRect, AddressLabel());
            if (GUI.Button(buttonRect, _editingAddress ? "決定" : "変更"))
            {
                _editingAddress = !_editingAddress;
            }

            if (_editingAddress)
            {
                joinAddress = GUI.TextField(Row(x, ref y, width, LineHeight), joinAddress);
            }

            if (GUI.Button(Row(x, ref y, width, LineHeight), "Clientとして接続"))
            {
                bootstrap.JoinGame(joinAddress, localPlayerIndex);
            }
        }

        private string AddressLabel()
        {
            if (!ReferenceEquals(joinAddress, _addressLabelSource))
            {
                _addressLabelSource = joinAddress;
                _addressLabel = "接続先IP: " + joinAddress;
            }

            return _addressLabel;
        }

        private void DrawSessionStatus(NetworkManager networkManager, float x, ref float y, float width)
        {
            RefreshStatusText(networkManager);
            GUI.Label(Row(x, ref y, width, LineHeight), _statusLine);
            if (networkManager.IsServer)
            {
                GUI.Label(Row(x, ref y, width, LineHeight), _participantsLine);
            }

            if (GUI.Button(Row(x, ref y, width, LineHeight), "切断"))
            {
                bootstrap.StopGame();
            }
        }

        // 役割・接続状態・自分の番号・参加者の組み合わせが変わったときだけ文字列を作る。
        private void RefreshStatusText(NetworkManager networkManager)
        {
            var participants = 0;
            if (networkManager.IsServer)
            {
                foreach (var playerIndex in bootstrap.ClientIdToPlayerIndex.Values)
                {
                    participants |= 1 << playerIndex;
                }
            }

            var key = (networkManager.IsServer ? 1 : 0) | (networkManager.IsConnectedClient ? 2 : 0) | ((bootstrap.LocalPlayerIndex + 1) << 2) | (participants << 8);
            if (key == _statusKey)
            {
                return;
            }

            _statusKey = key;
            var self = bootstrap.LocalPlayerIndex >= 0 && bootstrap.LocalPlayerIndex < PlayerLabels.Length ? PlayerLabels[bootstrap.LocalPlayerIndex] : "?";
            _statusLine = networkManager.IsServer
                ? $"Host として稼働中({self})"
                : networkManager.IsConnectedClient ? $"Client として接続済み({self})" : $"Client として接続待ち...({self})";

            _builder.Clear().Append("参加中:");
            for (var i = 0; i < 4; i++)
            {
                if ((participants & (1 << i)) != 0)
                {
                    _builder.Append(" P").Append(i + 1);
                }
            }

            _participantsLine = _builder.ToString();
        }

        private static Rect Row(float x, ref float y, float width, float height)
        {
            var rect = new Rect(x + Padding, y, width, height);
            y += height + Spacing;
            return rect;
        }
    }
}
