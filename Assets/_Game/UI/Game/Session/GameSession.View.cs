using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.UI.Game
{
    // 画面: どの画面を出すか、お知らせ・通知、値の掲示板（UiValues）への書き込み、握って準備OK、Esc の一時メニュー。
    public sealed partial class GameSession
    {
        private const float AddressRefreshSeconds = 5f;
        private const float JoinToastDelay = 0.5f;

        private readonly LobbyRoster.Seat[] _lastSeats = new LobbyRoster.Seat[LobbyRoster.SeatCount];
        private readonly List<(int seat, float at)> _pendingJoinToasts = new List<(int, float)>();
        private int _seenRosterVersion = -1;
        private float _nextAddressRefresh;
        private string _localAddresses = string.Empty;
        private float _gripHold;
        private bool _gripNeedsRelease;
        private bool _pauseNextFrame;

        // ───────── 画面 ─────────

        private void OpenFirstScreen()
        {
            var root = UiRoot.Active;
            if (root == null)
            {
                Debug.LogWarning("[Lobby] UIの置き場所（UiRoot）がありません。UIスタジオの「流れを組み立てる」でロビーを作り直してください。");
                return;
            }

            root.Open(topScreen);
            SetStatus("名前とプレイヤー番号を決めて、「部屋を作る」か「部屋に参加する」を押してください");
        }

        /// <summary>ロビーのメニューの画面を出す（メニューの段は1つずつなので、前の画面は自動で閉じる）。</summary>
        private void OpenMenu(string screenId, UiScreenTransition transition = null)
        {
            var root = UiRoot.Active;
            if (root == null)
            {
                return;
            }

            if (transition != null)
            {
                root.SwitchTo(screenId, transition);
            }
            else
            {
                root.Open(screenId);
            }
        }

        /// <summary>お知らせの小窓を出す（OKで閉じる）。</summary>
        public void ShowMessage(string title, string body)
        {
            UiValues.Set(FortressUiKeys.MessageTitle, title ?? string.Empty);
            UiValues.Set(FortressUiKeys.MessageBody, body ?? string.Empty);
            FortressEffectPlayer.PlaySound(onProblem, Vector3.zero);
            var root = UiRoot.Active;
            if (root == null || !root.Open(messageScreen))
            {
                Debug.LogWarning($"[Lobby] {title}: {body}");
            }
        }

        /// <summary>画面の端に少しだけ出る通知。</summary>
        public void Toast(string text)
        {
            UiValues.Set(FortressUiKeys.ToastText, text ?? string.Empty);
            var root = UiRoot.Active;
            if (root == null)
            {
                return;
            }

            var screen = root.Get(toastScreen);
            if (screen != null && screen.TryGetComponent<ToastScreen>(out var toast) && root.IsOpen(toastScreen))
            {
                toast.Restart();
                return;
            }

            root.Open(toastScreen);
        }

        private void SetStatus(string text) => UiValues.Set(FortressUiKeys.LobbyStatus, text ?? string.Empty);

        // ───────── 握って準備OK ─────────

        private void TickGripReady(float dt)
        {
            var canUse = gripToReady && State == SessionState.Room && (Roster.phase == LobbyPhase.Waiting || Roster.phase == LobbyPhase.Countdown);
            var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
            // 各PCのセンサーは、そのPCでは常にP1（番号0）として読む（キーボードならQ）。
            var grip = provider != null ? provider.GetGripValue(0) : 0f;
            if (State != SessionState.InGame)
            {
                UiValues.Set(FortressUiKeys.LocalGrip, grip);
            }

            if (!canUse)
            {
                _gripHold = 0f;
                UiValues.Set(FortressUiKeys.LobbyGripHold, 0f);
                return;
            }

            if (_gripNeedsRelease)
            {
                _gripNeedsRelease = grip > gripReadyThreshold * 0.6f;
                _gripHold = 0f;
            }
            else if (grip >= gripReadyThreshold)
            {
                _gripHold += dt;
                if (_gripHold >= gripReadyHoldSeconds)
                {
                    _gripHold = 0f;
                    _gripNeedsRelease = true;
                    ToggleReady();
                }
            }
            else
            {
                _gripHold = Mathf.Max(0f, _gripHold - dt * 2f);
            }

            UiValues.Set(FortressUiKeys.LobbyGripHold, Mathf.Clamp01(_gripHold / gripReadyHoldSeconds));
        }

        // ───────── Esc ─────────

        private void TickInput()
        {
            var root = UiRoot.Active;
            if (root == null)
            {
                return;
            }

            if (_pauseNextFrame)
            {
                _pauseNextFrame = false;
                if (State == SessionState.InGame && !root.IsOpen(pauseScreen))
                {
                    root.Open(pauseScreen);
                }
            }

            var back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                       (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));
            if (!back || (root.Overlay != null && root.Overlay.IsPlaying))
            {
                return;
            }

            // 小窓（一時メニュー・お知らせ）が開いていれば、UIの置き場所がそれを閉じるので何もしない。
            if (root.IsOpen(pauseScreen) || root.IsOpen(messageScreen))
            {
                return;
            }

            switch (State)
            {
                case SessionState.InGame:
                    // 同じフレームで UIの置き場所が「戻る」として閉じてしまわないよう、次のフレームで開く。
                    _pauseNextFrame = true;
                    break;
                case SessionState.Browsing:
                case SessionState.Connecting:
                    BackToTop();
                    break;
            }
        }

        // ───────── 値の掲示板へ ─────────

        private void PublishValues(float now)
        {
            if (now >= _nextAddressRefresh)
            {
                _nextAddressRefresh = now + AddressRefreshSeconds;
                _localAddresses = LanDiscovery.DescribeLocalAddresses();
                HideDDriveConnectOverlay();
            }

            var inRoom = State == SessionState.Room || State == SessionState.InGame;
            UiValues.Set(FortressUiKeys.LobbyName, LocalName);
            UiValues.Set(FortressUiKeys.LobbySelectedPlayer, SelectedSeat);
            UiValues.Set(FortressUiKeys.LobbySelectedLabel, $"P{SelectedSeat + 1}");
            UiValues.Set(FortressUiKeys.LobbyIsHost, IsHost);
            UiValues.Set(FortressUiKeys.LobbyInRoom, inRoom);
            UiValues.Set(FortressUiKeys.LobbyInGame, State == SessionState.InGame);
            UiValues.Set(FortressUiKeys.LobbyConnecting, State == SessionState.Connecting);
            UiValues.Set(FortressUiKeys.LobbyAddress, _localAddresses);
            UiValues.Set(FortressUiKeys.LobbyHostAddress, inRoom ? RoomAddress : string.Empty);
            UiValues.Set(FortressUiKeys.LobbyFoundHosts, Discovery != null ? Discovery.Hosts.Count : 0);
            UiValues.Set(FortressUiKeys.LobbyPlayers, Roster.PresentCount);
            UiValues.Set(FortressUiKeys.LobbyReadyCount, Roster.ReadyCount);
            UiValues.Set(FortressUiKeys.LobbyAllReady, Roster.AllReady);
            UiValues.Set(FortressUiKeys.LobbyCanStart, IsHost && State == SessionState.Room && Roster.CanStart(minPlayers, false));
            UiValues.Set(FortressUiKeys.LobbyCountingDown, Roster.phase == LobbyPhase.Countdown);
            UiValues.Set(FortressUiKeys.LobbyCountdown, Roster.phase == LobbyPhase.Countdown ? Mathf.Max(1, Mathf.CeilToInt(_countdownEnd - now)) : 0);
            UiValues.Set(FortressUiKeys.LobbyLocalReady, _localReady);
            UiValues.Set(FortressUiKeys.LobbyPhase, inRoom ? PhaseLabel(Roster.phase) : string.Empty);

            var local = LocalSeat;
            for (var seat = 0; seat < LobbyRoster.SeatCount; seat++)
            {
                var entry = Roster.seats[seat];
                UiValues.Set(FortressUiKeys.PlayerName(seat), entry.present ? entry.name : string.Empty);
                UiValues.Set(FortressUiKeys.PlayerReady(seat), entry.present && entry.ready);
                UiValues.Set(FortressUiKeys.PlayerIsHost(seat), entry.present && entry.isHost);
                UiValues.Set(FortressUiKeys.PlayerIsLocal(seat), inRoom ? seat == local : seat == SelectedSeat);
                UiValues.Set(FortressUiKeys.PlayerPing(seat), entry.present && !entry.isHost ? entry.pingMs : 0);
                UiValues.Set(FortressUiKeys.PlayerColor(seat), FortressColors.PlayerColor(seat));
            }

            UpdateRosterNotices(now);
        }

        /// <summary>部屋に入っている人の変化を、通知と音で知らせる（全員のPCで、届いた一覧を見て自分で出す）。</summary>
        private void UpdateRosterNotices(float now)
        {
            if (Roster.Version != _seenRosterVersion)
            {
                _seenRosterVersion = Roster.Version;
                var inRoom = State == SessionState.Room || State == SessionState.InGame;
                for (var seat = 0; seat < LobbyRoster.SeatCount; seat++)
                {
                    var before = _lastSeats[seat];
                    var after = Roster.seats[seat];
                    var isMe = seat == LocalSeat;
                    if (inRoom && !isMe)
                    {
                        if (after.present && !before.present)
                        {
                            // 名前は少し遅れて届くので、少し待ってから知らせる。
                            _pendingJoinToasts.Add((seat, now + JoinToastDelay));
                            FortressEffectPlayer.PlaySound(onPlayerJoined, Vector3.zero);
                        }
                        else if (!after.present && before.present)
                        {
                            Toast($"{before.name} が抜けました");
                            FortressEffectPlayer.PlaySound(onPlayerLeft, Vector3.zero);
                        }
                    }

                    if (inRoom && after.present && before.present && after.ready != before.ready)
                    {
                        FortressEffectPlayer.PlaySound(onReadyChanged, Vector3.zero);
                    }

                    _lastSeats[seat] = after;
                }

                if (!IsHost && inRoom && Roster.seats[LocalSeat].present)
                {
                    _localReady = Roster.seats[LocalSeat].ready;
                }
            }

            for (var i = _pendingJoinToasts.Count - 1; i >= 0; i--)
            {
                var (seat, at) = _pendingJoinToasts[i];
                if (now < at)
                {
                    continue;
                }

                _pendingJoinToasts.RemoveAt(i);
                if (Roster.seats[seat].present)
                {
                    Toast($"{Roster.seats[seat].name}（P{seat + 1}）が入りました");
                }
            }
        }

        /// <summary>
        /// D-Drive の開発用の「手動接続」の小窓（左下）を隠す。そこから接続するとプレイヤー番号を送らないので承認で弾かれる。
        /// ゲームのシーンでは FortressConnectUI が同じことをしている。小窓は後から作られることもあるので、ときどき確かめる。
        /// </summary>
        private static void HideDDriveConnectOverlay()
        {
            foreach (var overlay in FindObjectsByType<DDrive.Runtime.Net.NetManualConnectOverlay>(FindObjectsSortMode.None))
            {
                overlay.Visible = false;
            }
        }

        private static string PhaseLabel(LobbyPhase phase) => phase switch
        {
            LobbyPhase.Countdown => "まもなく開始",
            LobbyPhase.Loading => "読み込み中",
            LobbyPhase.InGame => "試合中",
            LobbyPhase.Returning => "ロビーに戻っています",
            _ => "準備OKを待っています"
        };
    }
}
