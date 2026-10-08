using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    // 通信: ホストになる／参加する、部屋の一覧の管理（ホスト）と受け取り（参加側）、切断の後始末。
    public sealed partial class GameSession
    {
        private const float PingInterval = 1f;

        private NetworkManager _subscribed;
        private float _connectDeadline;
        private float _nextPing;
        private int _broadcastVersion = -1;
        private bool _expectingDisconnect;

        public bool IsOnline => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public bool IsHost => IsOnline && NetworkManager.Singleton.IsServer;

        /// <summary>このPCの席（部屋に入っていなければ選んでいる席）。</summary>
        public int LocalSeat => network != null && network.LocalPlayerIndex >= 0 ? network.LocalPlayerIndex : SelectedSeat;

        // ───────── ホストになる・参加する・出る ─────────

        /// <summary>部屋を作る（ホストになる）。</summary>
        public void Host()
        {
            if (!CanBeginConnection())
            {
                return;
            }

            if (!network.HostGame(SelectedSeat))
            {
                ShowMessage("ホストになれませんでした", "通信の準備ができていないか、前の通信がまだ終わっていません。少し待ってからもう一度押してください。");
                return;
            }

            var manager = NetworkManager.Singleton;
            // 参加した人は「すでに読み込んでいるロビーのシーン」をそのまま使い、ゲームのシーンだけを重ねて読み込む。
            manager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            SubscribeNetwork(manager);
            Roster.Clear();
            Roster.Join(SelectedSeat, manager.LocalClientId, DisplayName, true);
            RoomAddress = LanDiscovery.DescribeLocalAddresses(1);
            Discovery.StopSearch();
            Discovery.StartResponder(DescribeRoom);
            EnterRoom();
        }

        /// <summary>参加する部屋を選ぶ画面へ（同じLANの部屋を探し始める）。</summary>
        public void OpenJoin()
        {
            if (State != SessionState.Top)
            {
                return;
            }

            SetState(SessionState.Browsing);
            Discovery.StartSearch();
            OpenMenu(joinScreen);
        }

        /// <summary>最初の画面へ戻る（部屋探しをやめる）。</summary>
        public void BackToTop()
        {
            if (State != SessionState.Browsing && State != SessionState.Connecting)
            {
                return;
            }

            if (State == SessionState.Connecting)
            {
                CancelConnecting();
            }

            Discovery.StopSearch();
            SetState(SessionState.Top);
            OpenMenu(topScreen);
        }

        /// <summary>見つかった部屋に参加する（選んだ席が使われていれば空いている席に替える）。</summary>
        public void Join(LanDiscovery.FoundHost host)
        {
            if (host.IsTaken(SelectedSeat))
            {
                for (var seat = 0; seat < LobbyRoster.SeatCount; seat++)
                {
                    if (!host.IsTaken(seat))
                    {
                        Toast($"P{SelectedSeat + 1} は使われていたので P{seat + 1} で参加します");
                        SelectSeat(seat);
                        break;
                    }
                }
            }

            if (host.IsTaken(SelectedSeat))
            {
                ShowMessage("満員です", "この部屋は4人そろっています。");
                return;
            }

            Join(host.address);
        }

        /// <summary>アドレスを指定して参加する。</summary>
        public void Join(string address)
        {
            address = (address ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(address))
            {
                ShowMessage("アドレスがありません", "ホストのPCに出ている「このPCのアドレス」を入力してください。");
                return;
            }

            if (!CanBeginConnection())
            {
                return;
            }

            if (!network.JoinGame(address, SelectedSeat))
            {
                ShowMessage("参加できませんでした", "通信の準備ができていないか、前の通信がまだ終わっていません。少し待ってからもう一度押してください。");
                return;
            }

            LastAddress = address;
            PlayerPrefs.SetString(PrefAddress, address);
            RoomAddress = address;
            Roster.Clear();
            SubscribeNetwork(NetworkManager.Singleton);
            _connectDeadline = Time.unscaledTime + connectTimeoutSeconds;
            SetState(SessionState.Connecting);
            SetStatus($"{address} に接続しています…");
        }

        /// <summary>接続を待つのをやめる。</summary>
        public void CancelConnecting()
        {
            if (State != SessionState.Connecting)
            {
                return;
            }

            _expectingDisconnect = true;
            StopNetworking();
            SetState(SessionState.Browsing);
            SetStatus("接続をやめました。");
        }

        /// <summary>部屋を出る（ホストなら部屋を閉じる＝全員が切断される）。</summary>
        public void LeaveRoom()
        {
            if (State != SessionState.Room)
            {
                return;
            }

            _expectingDisconnect = true;
            StopNetworking();
            Roster.Clear();
            SetState(SessionState.Top);
            OpenMenu(topScreen, menuTransition);
        }

        /// <summary>ホストが、その席の人を部屋から外す。</summary>
        public void Kick(int seat)
        {
            if (!IsHost || !LobbyRoster.IsValid(seat) || !Roster.seats[seat].present || Roster.seats[seat].isHost)
            {
                return;
            }

            NetworkManager.Singleton.DisconnectClient(Roster.seats[seat].clientId, "ホストに部屋から外されました。");
        }

        private bool CanBeginConnection()
        {
            if (network == null || NetworkManager.Singleton == null)
            {
                ShowMessage("通信の準備がありません", "ロビーのシーンに NetworkManager と FortressNetworkBootstrap がありません。UIスタジオの「流れを組み立てる」でロビーを作り直してください。");
                return false;
            }

            if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress)
            {
                ShowMessage("少し待ってください", "前の通信を終わらせているところです。");
                return false;
            }

            return true;
        }

        private void StopNetworking()
        {
            Discovery.StopResponder();
            UnsubscribeNetwork();
            if (network != null)
            {
                network.StopGame();
            }
        }

        private void EnterRoom()
        {
            _broadcastVersion = -1;
            SetState(SessionState.Room);
            OpenMenu(roomScreen, menuTransition);
            SetStatus(IsHost ? "みんなが入ってくるのを待っています" : "部屋に入りました");
        }

        private LanDiscovery.Reply DescribeRoom()
        {
            var mask = 0;
            for (var i = 0; i < LobbyRoster.SeatCount; i++)
            {
                mask |= Roster.seats[i].present ? 1 << i : 0;
            }

            return new LanDiscovery.Reply
            {
                hostName = DisplayName,
                gamePort = network != null ? network.port : 7777,
                takenMask = mask,
                phase = Roster.phase,
                players = Roster.PresentCount,
                build = LanDiscovery.BuildId
            };
        }

        // ───────── NetworkManager の出来事 ─────────

        private void SubscribeNetwork(NetworkManager manager)
        {
            if (manager == null || manager == _subscribed)
            {
                return;
            }

            UnsubscribeNetwork();
            _subscribed = manager;
            _expectingDisconnect = false;
            manager.OnClientConnectedCallback += HandleClientConnected;
            manager.OnClientDisconnectCallback += HandleClientDisconnected;
            LobbyMessages.Register(manager, HandleHello, HandleReady, HandleState);
        }

        private void UnsubscribeNetwork()
        {
            if (_subscribed == null)
            {
                return;
            }

            _subscribed.OnClientConnectedCallback -= HandleClientConnected;
            _subscribed.OnClientDisconnectCallback -= HandleClientDisconnected;
            LobbyMessages.Unregister(_subscribed);
            _subscribed = null;
        }

        private void HandleClientConnected(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            if (manager.IsServer)
            {
                if (clientId == manager.LocalClientId)
                {
                    return;
                }

                var seat = SeatOfClient(clientId);
                if (seat >= 0)
                {
                    Roster.Join(seat, clientId, $"P{seat + 1}", false);
                    LobbyMessages.SendState(manager, clientId, Roster);
                }

                return;
            }

            if (clientId == manager.LocalClientId)
            {
                // 参加できた。名前を伝えて部屋の画面へ（試合中の部屋なら、この後ゲームのシーンが自動で読み込まれる）。
                LobbyMessages.SendHello(manager, DisplayName);
                _localReady = false;
                Discovery.StopSearch();
                EnterRoom();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer && clientId != manager.LocalClientId)
            {
                var seat = Roster.SeatOf(clientId);
                if (seat >= 0)
                {
                    Roster.Leave(seat);
                    if (Roster.phase == LobbyPhase.Countdown && Roster.PresentCount < minPlayers)
                    {
                        CancelCountdown("人数が足りなくなったので、開始をやめました");
                    }
                }

                return;
            }

            // 参加側から見て、自分が切断された（ホストが部屋を閉じた・外された・つながらなかった）。
            if (manager != null && !manager.IsServer)
            {
                OnLostConnection();
            }
        }

        private void OnLostConnection()
        {
            var wasConnecting = State == SessionState.Connecting;
            var reason = network != null && !string.IsNullOrEmpty(network.LastDisconnectReason) ? network.LastDisconnectReason : "";
            UnsubscribeNetwork();
            Discovery.StopResponder();
            if (_expectingDisconnect)
            {
                _expectingDisconnect = false;
                return;
            }

            var title = wasConnecting ? "つながりませんでした" : "部屋との接続が切れました";
            var body = wasConnecting
                ? $"{reason}\nホストのアドレス・ファイアウォールの許可・同じLANにいるかを確かめてください。".Trim()
                : string.IsNullOrEmpty(reason) ? "ホストが部屋を閉じたか、通信が途切れました。" : reason;
            ReturnAfterProblem(title, body, wasConnecting);
        }

        /// <summary>切断などのあと、最初の画面（つながらなかったときは部屋選び）へ戻ってお知らせを出す。</summary>
        private void ReturnAfterProblem(string title, string body, bool backToBrowsing)
        {
            Roster.Clear();
            _localReady = false;
            _forcedStart = false;
            if (IsGameLoaded)
            {
                UnloadGameLocally(() =>
                {
                    SetState(SessionState.Top);
                    OpenMenu(topScreen);
                    ShowMessage(title, body);
                });
                return;
            }

            SetStatus(string.Empty);
            SetState(backToBrowsing ? SessionState.Browsing : SessionState.Top);
            OpenMenu(backToBrowsing ? joinScreen : topScreen);
            if (backToBrowsing)
            {
                Discovery.StartSearch();
            }

            ShowMessage(title, body);
        }

        private int SeatOfClient(ulong clientId)
        {
            if (network == null)
            {
                return -1;
            }

            return network.ClientIdToPlayerIndex.TryGetValue(clientId, out var seat) ? seat : -1;
        }

        // ───────── ロビーのメッセージ ─────────

        private void HandleHello(ulong sender, FastBufferReader reader)
        {
            if (!IsHost)
            {
                return;
            }

            var name = LobbyMessages.ReadHello(reader);
            var seat = Roster.SeatOf(sender);
            if (seat < 0)
            {
                seat = SeatOfClient(sender);
                if (seat < 0)
                {
                    return;
                }

                Roster.Join(seat, sender, name, false);
            }

            Roster.SetName(seat, name);
        }

        private void HandleReady(ulong sender, FastBufferReader reader)
        {
            if (!IsHost)
            {
                return;
            }

            var ready = LobbyMessages.ReadReady(reader);
            var seat = Roster.SeatOf(sender);
            if (seat < 0)
            {
                return;
            }

            Roster.SetReady(seat, ready);
            if (!ready && Roster.phase == LobbyPhase.Countdown && !_forcedStart)
            {
                CancelCountdown($"{Roster.seats[seat].name} が準備OKを外したので、開始をやめました");
            }
        }

        private void HandleState(ulong sender, FastBufferReader reader)
        {
            if (IsHost)
            {
                return;
            }

            var before = Roster.phase;
            LobbyMessages.ReadState(reader, Roster);
            OnPhaseReceived(before, Roster.phase);
        }

        // ───────── 毎フレーム ─────────

        private void TickNetwork(float now)
        {
            if (State == SessionState.Connecting && now > _connectDeadline)
            {
                _expectingDisconnect = true;
                StopNetworking();
                _expectingDisconnect = false;
                SetState(SessionState.Browsing);
                OpenMenu(joinScreen);
                Discovery.StartSearch();
                SetStatus(string.Empty);
                ShowMessage("つながりませんでした", $"{connectTimeoutSeconds:0}秒待っても返事がありませんでした。\nホストのアドレス・ファイアウォールの許可・同じLANにいるかを確かめてください。");
                return;
            }

            if (!IsHost)
            {
                return;
            }

            var manager = NetworkManager.Singleton;
            if (now >= _nextPing)
            {
                _nextPing = now + PingInterval;
                var transport = manager.NetworkConfig.NetworkTransport;
                for (var seat = 0; seat < LobbyRoster.SeatCount; seat++)
                {
                    var entry = Roster.seats[seat];
                    if (entry.present && !entry.isHost && transport != null)
                    {
                        Roster.SetPing(seat, (int)transport.GetCurrentRtt(entry.clientId));
                    }
                }
            }

            // 1フレームに何度変わっても、配るのは1回だけ。
            if (Roster.Version != _broadcastVersion)
            {
                _broadcastVersion = Roster.Version;
                LobbyMessages.BroadcastState(manager, Roster);
            }
        }
    }
}
