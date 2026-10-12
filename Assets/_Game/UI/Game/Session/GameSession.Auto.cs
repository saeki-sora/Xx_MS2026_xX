using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    // 展示用の自動接続（2026-10-12、ユーザー決定）: 会場ではキーボードを置かず、握力センサーだけで「タイトル → キャラ選択 → ゲーム」。
    // ・ロビーの画面は出さず、歯磨き粉の幕で隠したまま同じLANの部屋を少しさがす
    // ・部屋があれば空いている一番小さい席（来た順）で入る（キャラ選択中・試合中の部屋にも入る）
    // ・無ければ自分がホスト（P1）になり、すぐ全員でキャラクター選択へ
    // ・ほぼ同時に2台がホストになったら、人が少ない部屋（同じなら部屋の番号が大きい方）のホストが、もう片方に入り直す（AutoMatchRules）
    // ・切れた・入れなかったときは、お知らせを出さずにさがし直す（ホストが落ちたら、残った人の中で最初にさがし終えた人が新しいホスト）
    // 起動オプション -lobby（lobbyLaunchArgument）を付けると、今まで通りのロビー画面（デバッグ用）。
    public sealed partial class GameSession
    {
        [Header("展示用の自動接続")]
        [Tooltip("ONなら、ロビーの画面を出さずに自動でつなぐ（同じLANの部屋を少しさがし、あれば空いている席で入る。無ければ自分がホストになってすぐキャラクター選択へ）。\n" +
                 "下の起動オプション（既定 -lobby）を付けて起動したときは OFF 扱いになり、今まで通りのロビー画面になる。")]
        public bool autoConnect = true;

        [Tooltip("この起動オプションを付けて .exe を起動すると、自動でつながずにロビー画面を出す（デバッグ用）。")]
        public string lobbyLaunchArgument = "-lobby";

        [Tooltip("部屋をさがす秒数。この間に部屋が見つからなければ自分がホストになる。")]
        [Min(0.5f)]
        public float autoSearchSeconds = 1.5f;

        [Tooltip("さがす秒数に足す、ばらつきの最大秒数（PCごとに毎回ランダム）。ほぼ同時に始めた2台が両方ホストになりにくくする。")]
        [Min(0f)]
        public float autoSearchJitterSeconds = 1f;

        // 入れなかった・切れた・譲ったあと、前の通信が終わって部屋の一覧が新しくなるまで待つ秒数（一覧は1秒ごとに届く）。
        private const float AutoRetrySeconds = 1.5f;

        // ホストがキャラクター選択に進めなかったとき、次に試すまでの秒数。
        private const float AutoStartCooldownSeconds = 3f;

        private bool _autoStarted;
        private bool _autoWaitingForMatch;
        private float _autoSearchUntil;
        private float _autoNotBefore;
        private float _autoNextStart;
        private int _roomId;
        private int _ignoreRoomId;

        /// <summary>展示用の自動接続で動いているか（autoConnect が ON で、起動オプション -lobby が無い）。</summary>
        public bool IsAutoConnect { get; private set; }

        private void InitAutoConnect()
        {
            IsAutoConnect = autoConnect && !HasLaunchArgument(lobbyLaunchArgument);
            if (IsAutoConnect)
            {
                Debug.Log($"[Lobby] 展示用の自動接続で動きます（ロビー画面を使うときは起動オプション {lobbyLaunchArgument} を付けて起動）。");
            }
        }

        private static bool HasLaunchArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return false;
            }

            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // ───────── 毎フレーム ─────────

        private void TickAuto(float now)
        {
            if (!IsAutoConnect || !_autoStarted)
            {
                return;
            }

            // キャラクター選択かゲームのシーンに入れたら、幕を開けてよい。
            if (_autoWaitingForMatch && State == SessionState.InGame && IsMatchSceneLoaded)
            {
                _autoWaitingForMatch = false;
            }

            switch (State)
            {
                case SessionState.Top:
                    BeginAutoSearch(now, 0f, null);
                    break;
                case SessionState.Browsing:
                    TickAutoSearch(now);
                    break;
                case SessionState.Room:
                case SessionState.InGame:
                    if (!IsOnline && (NetworkManager.Singleton == null || !NetworkManager.Singleton.ShutdownInProgress))
                    {
                        // 知らせ無しに通信が止まっていた（ホストの開始に失敗した など）。
                        AutoRecover("通信が止まっていました");
                    }
                    else if (IsHost)
                    {
                        TickAutoHost(now);
                    }

                    break;
            }
        }

        /// <summary>幕で隠して、部屋をさがし始める（onCovered: 覆いきったときにすること）。</summary>
        private void BeginAutoSearch(float now, float delay, Action onCovered)
        {
            SetState(SessionState.Browsing);
            Discovery.StartSearch();
            _autoNotBefore = now + delay;
            _autoSearchUntil = _autoNotBefore + autoSearchSeconds + UnityEngine.Random.Range(0f, autoSearchJitterSeconds);
            SetStatus("ほかのPCの部屋をさがしています…");

            // キャラクター選択かゲームのシーンが読み込まれるまで隠したまま（入った後は、その時々の幕に引き継がれる）。
            _autoWaitingForMatch = true;
            PlayCharacterSelectCover(onCovered, () => !_autoWaitingForMatch);
        }

        private void TickAutoSearch(float now)
        {
            if (now < _autoNotBefore || network == null)
            {
                return;
            }

            var manager = NetworkManager.Singleton;
            if (manager == null || manager.ShutdownInProgress)
            {
                return;
            }

            if (manager.IsListening)
            {
                // さがしている間に前の通信が残っていたら終わらせる（終わるまで待ってから入る）。
                _expectingDisconnect = true;
                StopNetworking();
                return;
            }

            var choice = AutoMatchRules.Choose(Discovery.Hosts, _ignoreRoomId, out var index);
            if (choice == AutoMatchChoice.Join)
            {
                var host = Discovery.Hosts[index];
                var seat = AutoMatchRules.FirstFreeSeat(host.reply.takenMask);
                SetAutoSeat(seat);
                Debug.Log($"[Lobby] 自動接続: {host.address} の部屋（{host.reply.players}人・{PhaseLabel(host.reply.phase)}）に P{seat + 1} で入ります。");
                Join(host.address);
                if (State != SessionState.Connecting)
                {
                    _autoNotBefore = now + AutoRetrySeconds;
                }

                return;
            }

            // 部屋はあるが今は入れない（読み込み中・満員）ならホストにならずに待つ。
            if (choice == AutoMatchChoice.Wait || now < _autoSearchUntil)
            {
                return;
            }

            SetAutoSeat(0);
            Debug.Log("[Lobby] 自動接続: 部屋が見つからないので、ホストになります（P1）。");
            Host();
            if (State == SessionState.Room)
            {
                // ほかにホストができていないか見張るため、ホストになった後もさがし続ける。
                Discovery.StartSearch();
                _autoNextStart = now;
            }
            else
            {
                _autoNotBefore = now + AutoRetrySeconds;
                _autoSearchUntil = _autoNotBefore;
            }
        }

        private void TickAutoHost(float now)
        {
            // 部屋で待っている（ホストになった直後・全員でロビーに戻った後）なら、すぐキャラクター選択へ。
            if (State == SessionState.Room && Roster.phase == LobbyPhase.Waiting)
            {
                if (now >= _autoNextStart)
                {
                    _autoNextStart = now + AutoStartCooldownSeconds;
                    _forcedStart = true;
                    if (UsesCharacterSelect)
                    {
                        BeginCharacterSelect();
                    }
                    else
                    {
                        BeginLoadGame();
                    }
                }

                return;
            }

            // 部屋が2つできていたら、試合が始まる前に限って片方にまとめる（始まった試合は止めない）。
            if (Roster.phase != LobbyPhase.CharacterSelect)
            {
                return;
            }

            foreach (var other in Discovery.Hosts)
            {
                if (AutoMatchRules.ShouldYield(Roster.PresentCount, _roomId, other))
                {
                    Debug.Log($"[Lobby] 自動接続: 同じLANにもう1つ部屋がありました（{other.address}、{other.reply.players}人）。そちらに入り直します。");
                    _ignoreRoomId = _roomId;
                    _expectingDisconnect = true;
                    StopNetworking();
                    AutoRecover("部屋をまとめるため、入り直します");
                    return;
                }
            }
        }

        // ───────── 困ったとき ─────────

        /// <summary>切れた・入れなかった・譲ったとき、お知らせを出さずに幕で隠してさがし直す。</summary>
        private void AutoRecover(string reason)
        {
            Debug.Log($"[Lobby] 自動接続: {reason}。もう一度さがします。");
            Roster.Clear();
            _localReady = false;
            _forcedStart = false;
            Discovery.StopResponder();
            Action unload = IsMatchSceneLoaded ? UnloadMatchScenesLocally : (Action)null;
            BeginAutoSearch(Time.unscaledTime, AutoRetrySeconds, unload);
        }

        private void UnloadMatchScenesLocally()
        {
            var game = GameScene;
            if (game.isLoaded)
            {
                SceneManager.UnloadSceneAsync(game);
            }

            UnloadCharacterSelectLocally();
        }

        /// <summary>自動接続のときの席（来た順。好みの席として保存はしない）。</summary>
        private void SetAutoSeat(int seat)
        {
            if (!LobbyRoster.IsValid(seat) || SelectedSeat == seat)
            {
                return;
            }

            SelectedSeat = seat;
            RaiseChanged();
        }
    }
}
