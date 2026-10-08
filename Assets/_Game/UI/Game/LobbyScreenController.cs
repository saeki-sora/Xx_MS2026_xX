using MS2026.Fortress.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ロビー画面（4人の接続待ち）の動き。プレイヤー番号を選んで「ホストになる」か「参加する」を押すと接続し、
    /// つながったら次の画面（既定: HUD）へ切り替える。ボタンや入力欄は差し替え自由（ここで参照を入れ替えるだけ）。
    /// 状態の文は値「lobby.status」、選んだ番号は「lobby.selectedPlayer」にも書くので、見た目は値のつなぎ部品で自由に作れる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI Studio/ロビー画面の動き (LobbyScreenController)")]
    public sealed class LobbyScreenController : MonoBehaviour
    {
        [Tooltip("P1〜P4 を選ぶボタン（左から P1, P2, P3, P4）。")]
        public Button[] playerButtons = new Button[4];

        [Tooltip("ホストになる（自分のPCで部屋を作る）ボタン。")]
        public Button hostButton;

        [Tooltip("参加する（ホストのPCにつなぐ）ボタン。")]
        public Button joinButton;

        [Tooltip("やめる（切断して選び直す）ボタン。")]
        public Button leaveButton;

        [Tooltip("ホストのIPアドレスの入力欄（標準の InputField か TextMeshPro の入力欄）。")]
        public Selectable addressField;

        [Tooltip("つながったら切り替える画面の名前。")]
        public string nextScreenId = "Hud";

        [Tooltip("画面切り替えの幕（空なら暗転）。")]
        public UiScreenTransition transition;

        [Tooltip("ONなら、今の仮の接続画面（画面右上の OnGUI）を隠す。")]
        public bool hideTemporaryConnectUi = true;

        [Tooltip("最初に選んでおくプレイヤー（0〜3）。")]
        [Range(0, 3)]
        public int defaultPlayer;

        private int _selected;
        private bool _connecting;
        private bool _switched;

        private void OnEnable()
        {
            _selected = Mathf.Clamp(defaultPlayer, 0, 3);
            for (var i = 0; i < playerButtons.Length; i++)
            {
                var index = i;
                if (playerButtons[i] != null)
                {
                    playerButtons[i].onClick.AddListener(() => Select(index));
                }
            }

            AddListener(hostButton, Host);
            AddListener(joinButton, Join);
            AddListener(leaveButton, Leave);

            if (hideTemporaryConnectUi)
            {
                foreach (var ui in FindObjectsByType<FortressConnectUI>(FindObjectsSortMode.None))
                {
                    ui.enabled = false;
                }
            }

            Select(_selected);
            SetStatus("プレイヤー番号を選んで、ホストになるか参加してください。");

            _connecting = false;
            _switched = false;
        }

        private void OnDisable()
        {
            foreach (var button in playerButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }
            }

            RemoveListener(hostButton, Host);
            RemoveListener(joinButton, Join);
            RemoveListener(leaveButton, Leave);
        }

        private void Update()
        {
            if (_switched)
            {
                return;
            }

            // ボタンでつないだときも、起動引数（テスト用の bat）で後からつながったときも、つながったら次の画面へ。
            if (IsConnected())
            {
                _switched = true;
                _connecting = false;
                SetStatus("つながりました！");
                if (UiRoot.Active != null && !string.IsNullOrEmpty(nextScreenId))
                {
                    var self = GetComponentInParent<UiScreen>();
                    UiRoot.Active.SwitchTo(nextScreenId, transition, closeScreenId: self != null ? self.screenId : null);
                }

                return;
            }

            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (_connecting && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress)))
            {
                _connecting = false;
                var bootstrap = FortressNetworkBootstrap.Instance;
                var reason = bootstrap != null ? bootstrap.LastDisconnectReason : null;
                SetStatus(string.IsNullOrEmpty(reason) ? "つながりませんでした。IPアドレスとホストの準備を確かめてください。" : $"つながりませんでした: {reason}");
            }
        }

        public void Select(int player)
        {
            _selected = Mathf.Clamp(player, 0, 3);
            for (var i = 0; i < playerButtons.Length; i++)
            {
                if (playerButtons[i] != null)
                {
                    playerButtons[i].transform.localScale = i == _selected ? Vector3.one * 1.08f : Vector3.one;
                }
            }

            UiValues.Set(FortressUiKeys.LobbySelectedPlayer, _selected);
        }

        public void Host()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null)
            {
                SetStatus("通信の部品（FortressNetworkBootstrap）がシーンにありません。");
                return;
            }

            SetStatus($"P{_selected + 1} としてホストを始めています…");
            _connecting = bootstrap.HostGame(_selected);
            if (!_connecting)
            {
                SetStatus("ホストを始められませんでした（直前に切断した直後は少し待ってください）。");
            }
        }

        public void Join()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null)
            {
                SetStatus("通信の部品（FortressNetworkBootstrap）がシーンにありません。");
                return;
            }

            var address = ReadAddress();
            SetStatus($"P{_selected + 1} として {address} に参加しています…");
            _connecting = bootstrap.JoinGame(address, _selected);
            if (!_connecting)
            {
                SetStatus("参加を始められませんでした。");
            }
        }

        public void Leave()
        {
            _connecting = false;
            if (FortressNetworkBootstrap.Instance != null)
            {
                FortressNetworkBootstrap.Instance.StopGame();
            }

            SetStatus("切断しました。");
        }

        private string ReadAddress()
        {
            var text = addressField switch
            {
                InputField legacy => legacy.text,
                TMP_InputField tmp => tmp.text,
                _ => null
            };

            return string.IsNullOrWhiteSpace(text) ? "127.0.0.1" : text.Trim();
        }

        private static bool IsConnected()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            return manager != null && (manager.IsConnectedClient || (manager.IsHost && manager.IsListening));
        }

        // 未設定の欄は編集中「偽のnull」になるので ?. ではなく == null で調べる。
        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static void SetStatus(string message) => UiValues.Set(FortressUiKeys.LobbyStatus, message);
    }
}
