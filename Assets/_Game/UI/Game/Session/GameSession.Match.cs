using System;
using System.Collections;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using MS2026.Fortress;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    // 試合の流れ: 準備OK → 3・2・1 → 全員でゲームのシーンを重ねて読み込む → 全員でロビーに戻る／抜ける／タイトルへ。
    public sealed partial class GameSession
    {
        private bool _localReady;
        private bool _forcedStart;
        private float _countdownEnd;
        private int _shownCountdown;

        public bool LocalReady => _localReady;

        public bool IsGameLoaded => SceneManager.GetSceneByName(gameSceneName).isLoaded;

        public Scene GameScene => SceneManager.GetSceneByName(gameSceneName);

        // ───────── 準備OK・開始 ─────────

        public void ToggleReady() => SetReady(!_localReady);

        public void SetReady(bool ready)
        {
            if (State != SessionState.Room || (Roster.phase != LobbyPhase.Waiting && Roster.phase != LobbyPhase.Countdown))
            {
                return;
            }

            _localReady = ready;
            if (IsHost)
            {
                Roster.SetReady(LocalSeat, ready);
                if (!ready && Roster.phase == LobbyPhase.Countdown && !_forcedStart)
                {
                    CancelCountdown("準備OKを外したので、開始をやめました");
                }
            }
            else if (IsOnline)
            {
                LobbyMessages.SendReady(NetworkManager.Singleton, ready);
            }

            RaiseChanged();
        }

        /// <summary>ホストがゲームを始める（force なら全員の準備OKを待たない）。</summary>
        public void StartMatch(bool force = false)
        {
            force &= allowForceStart;
            if (!IsHost || State != SessionState.Room || !Roster.CanStart(minPlayers, force))
            {
                return;
            }

            _forcedStart = force;
            if (countdownSeconds <= 0f)
            {
                BeginLoadGame();
                return;
            }

            _countdownEnd = Time.unscaledTime + countdownSeconds;
            ChangePhase(LobbyPhase.Countdown, countdownSeconds);
        }

        /// <summary>ホストがカウントダウンをやめる。</summary>
        public void CancelCountdown(string reason = "開始をやめました")
        {
            if (!IsHost || Roster.phase != LobbyPhase.Countdown)
            {
                return;
            }

            _forcedStart = false;
            ChangePhase(LobbyPhase.Waiting);
            Toast(reason);
        }

        private void TickCountdown(float now)
        {
            if (Roster.phase != LobbyPhase.Countdown)
            {
                _shownCountdown = 0;
                return;
            }

            var remaining = _countdownEnd - now;
            var shown = Mathf.Max(1, Mathf.CeilToInt(remaining));
            if (shown != _shownCountdown)
            {
                _shownCountdown = shown;
                FortressEffectPlayer.PlaySound(onCountdownTick, Vector3.zero);
            }

            if (IsHost && remaining <= 0f)
            {
                BeginLoadGame();
            }
        }

        // ───────── 部屋の状態の変化（ホストは自分で、参加側は届いたとき）─────────

        /// <summary>ホストが部屋の状態を変える（変化に合わせた画面の動きも自分で行う）。</summary>
        private void ChangePhase(LobbyPhase phase, float countdown = 0f)
        {
            var before = Roster.phase;
            Roster.SetPhase(phase, countdown);
            OnPhaseReceived(before, phase);
        }

        private void OnPhaseReceived(LobbyPhase before, LobbyPhase after)
        {
            var root = UiRoot.Active;
            if (after == LobbyPhase.Countdown && before != LobbyPhase.Countdown)
            {
                if (!IsHost)
                {
                    _countdownEnd = Time.unscaledTime + Roster.countdownRemaining;
                }

                root?.Open(countdownScreen);
            }
            else if (before == LobbyPhase.Countdown && after != LobbyPhase.Countdown)
            {
                root?.Close(countdownScreen);
                if (after == LobbyPhase.Waiting && !IsHost)
                {
                    Toast("開始が取り消されました");
                }
            }

            if (after == LobbyPhase.Loading && before != LobbyPhase.Loading)
            {
                FortressEffectPlayer.PlaySound(onMatchStart, Vector3.zero);
                if (!IsHost && !IsGameLoaded)
                {
                    // 参加側は NGO が自動でゲームのシーンを読み込む。読み終わるまで幕で隠す。
                    root?.PlayTransition(gameTransition, null, () => IsGameLoaded || !IsOnline || Roster.phase == LobbyPhase.Waiting);
                }
            }

            if (after == LobbyPhase.InGame && !IsGameLoaded && !IsHost && State == SessionState.Room)
            {
                // 試合中の部屋に後から入った。ゲームのシーンが届くまで幕で隠す。
                root?.PlayTransition(gameTransition, null, () => IsGameLoaded || !IsOnline);
            }

            if (after == LobbyPhase.Returning && before != LobbyPhase.Returning && !IsHost)
            {
                root?.Close(pauseScreen);
                root?.PlayTransition(gameTransition, null, () => !IsGameLoaded || !IsOnline);
            }

            RaiseChanged();
        }

        // ───────── ゲームのシーンを読み込む・外す ─────────

        private void BeginLoadGame()
        {
            if (!IsHost || Roster.phase == LobbyPhase.Loading)
            {
                return;
            }

            ChangePhase(LobbyPhase.Loading);
            var root = UiRoot.Active;
            Action load = () =>
            {
                var status = NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Additive);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogWarning($"[Lobby] ゲームのシーン「{gameSceneName}」を読み込めませんでした（{status}）。ビルドの設定にシーンが入っているか確かめてください。");
                    ChangePhase(LobbyPhase.Waiting);
                    ShowMessage("ゲームを始められませんでした", $"シーン「{gameSceneName}」を読み込めませんでした（{status}）。");
                }
            };

            if (root != null)
            {
                root.PlayTransition(gameTransition, load, () => IsGameLoaded || Roster.phase == LobbyPhase.Waiting);
            }
            else
            {
                load();
            }
        }

        /// <summary>ホストが、試合中の全員をロビー（部屋の画面）へ戻す。</summary>
        public void ReturnEveryoneToLobby()
        {
            if (!IsHost || !IsGameLoaded || Roster.phase == LobbyPhase.Returning)
            {
                return;
            }

            var root = UiRoot.Active;
            root?.Close(pauseScreen);
            ChangePhase(LobbyPhase.Returning);
            Action unload = () => NetworkManager.Singleton.SceneManager.UnloadScene(GameScene);
            if (root != null)
            {
                root.PlayTransition(gameTransition, unload, () => !IsGameLoaded);
            }
            else
            {
                unload();
            }
        }

        /// <summary>試合から自分だけ抜けて最初の画面へ（ホストが抜けると部屋が閉じる）。</summary>
        public void LeaveMatch()
        {
            if (State != SessionState.InGame)
            {
                return;
            }

            UiRoot.Active?.Close(pauseScreen);
            _expectingDisconnect = true;
            StopNetworking();
            Roster.Clear();
            _localReady = false;
            SetState(SessionState.Leaving);
            UnloadGameLocally(() =>
            {
                SetState(SessionState.Top);
                OpenMenu(topScreen);
            });
        }

        /// <summary>通信を終えてタイトルのシーンへ戻る（ロビーのシーンごと閉じる）。</summary>
        public void GoToTitle()
        {
            if (State == SessionState.Leaving && !IsGameLoaded)
            {
                return;
            }

            UiRoot.Active?.Close(pauseScreen);
            _expectingDisconnect = true;
            StopNetworking();
            Discovery.StopSearch();
            SetState(SessionState.Leaving);
            var root = UiRoot.Active;
            if (root != null)
            {
                root.PlayTransition(titleTransition, () => StartCoroutine(LoadTitleWhenShutDown()));
            }
            else
            {
                StartCoroutine(LoadTitleWhenShutDown());
            }
        }

        private IEnumerator LoadTitleWhenShutDown()
        {
            var timeout = Time.unscaledTime + 5f;
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress && Time.unscaledTime < timeout)
            {
                yield return null;
            }

            // NetworkManager は自分で「シーンをまたいで残る」場所へ移るので、ここで消す（次にロビーへ来たとき新しく作られる）。
            if (NetworkManager.Singleton != null)
            {
                Destroy(NetworkManager.Singleton.gameObject);
            }

            // シーンをまたいで残る設定になっている物（D-Drive・UIの置き場所）も、ロビーの物ならここで消す。
            DestroyIfPersistent(DDriveRuntimeBootstrap.Instance != null ? DDriveRuntimeBootstrap.Instance.gameObject : null);
            DestroyIfPersistent(UiRoot.Active != null ? UiRoot.Active.gameObject : null);
            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
        }

        private static void DestroyIfPersistent(GameObject go)
        {
            if (go != null && go.scene.name == "DontDestroyOnLoad")
            {
                Destroy(go);
            }
        }

        private void UnloadGameLocally(Action done)
        {
            var root = UiRoot.Active;
            Action unload = () =>
            {
                var scene = GameScene;
                if (scene.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            };

            if (root != null)
            {
                root.PlayTransition(gameTransition, unload, () => !IsGameLoaded, done);
            }
            else
            {
                unload();
                done?.Invoke();
            }
        }

        // ───────── シーンの読み込み・外しの合図 ─────────

        private void SubscribeScenes()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void UnsubscribeScenes()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameSceneName)
            {
                return;
            }

            // 光・空・新しく作る物の置き場所をゲームのシーンに合わせる。
            SceneManager.SetActiveScene(scene);
            ShowLobbyLook(false);
            var root = UiRoot.Active;
            if (root != null)
            {
                root.CloseLayer("Menu");
                root.Close(countdownScreen);
                root.Open(hudScreen);
            }

            SetState(SessionState.InGame);
            if (IsHost)
            {
                ChangePhase(LobbyPhase.InGame);
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != gameSceneName)
            {
                return;
            }

            ShowLobbyLook(true);
            var root = UiRoot.Active;
            if (root != null)
            {
                root.CloseLayer("HUD");
                root.Close(pauseScreen);
            }

            _localReady = false;
            _forcedStart = false;
            PlayLobbyBgm();
            if (IsOnline && State != SessionState.Leaving)
            {
                SetState(SessionState.Room);
                OpenMenu(roomScreen);
                if (IsHost)
                {
                    Roster.ClearReady();
                    ChangePhase(LobbyPhase.Waiting);
                }
            }
        }

        // ───────── 見た目・音 ─────────

        private void ShowLobbyLook(bool show)
        {
            var root = UiRoot.Active;
            if (root != null && !string.IsNullOrEmpty(backgroundScreen))
            {
                if (show)
                {
                    root.Open(backgroundScreen);
                }
                else
                {
                    root.Close(backgroundScreen);
                }
            }

            if (lobbyCamera != null)
            {
                lobbyCamera.gameObject.SetActive(show);
            }

            foreach (var go in hideDuringGame)
            {
                if (go != null)
                {
                    go.SetActive(show);
                }
            }
        }

        private void PlayLobbyBgm()
        {
            if (lobbyBgm.IsValid && isActiveAndEnabled)
            {
                StartCoroutine(PlayBgmWhenReady());
            }
        }

        private IEnumerator PlayBgmWhenReady()
        {
            var timeout = Time.unscaledTime + 10f;
            while ((DDriveRuntimeBootstrap.Instance == null || !DDriveRuntimeBootstrap.Instance.IsReady) && Time.unscaledTime < timeout)
            {
                yield return null;
            }

            if (Audio.IsBound && !IsGameLoaded)
            {
                Audio.PlayBgm(lobbyBgm);
            }
        }
    }
}
