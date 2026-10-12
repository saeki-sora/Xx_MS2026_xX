using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    // キャラクター選択: ホストの「ゲーム開始」→（3・2・1 はしない）→ 全員でキャラクター選択のシーンを重ねて読み込む →
    // 全員が決めたら、選択画面（Assets/CharacterSelect/ の CharacterSelectController）が ProceedToGame を呼ぶ →
    // 幕で隠して選択のシーンを外し、ゲームのシーンを読み込む（ここから先は今までの「ゲームを読み込む」流れと同じ）。
    // このクラスが知っているのはシーンの名前だけ（選択画面の中身は知らない）。
    public sealed partial class GameSession
    {
        [Header("キャラクター選択")]
        [Tooltip("「ゲーム開始」のあと全員で入るキャラクター選択のシーンの名前（ビルドの設定に入っていること）。ロビーの上に重ねて読み込む。\n空にすると今まで通り、3・2・1 のあとすぐゲームのシーンへ進む。")]
        public string characterSelectSceneName = "Character_Select";

        private const float SceneSwapTimeoutSeconds = 15f;

        /// <summary>幕を出す処理（onCovered: 覆いきったら呼ぶ、isReady: true になるまで覆ったまま、その後に開く）。</summary>
        public delegate void CoverPlayer(Action onCovered, Func<bool> isReady);

        /// <summary>
        /// キャラクター選択に入るときの幕。キャラ選択の側（Assets/CharacterSelect の歯磨き粉の幕）が起動時に入れる。
        /// null ならロビーのいつもの幕（gameTransition）を使う。
        /// </summary>
        public static CoverPlayer CharacterSelectCurtain;

        /// <summary>幕を出せたら true（false ならロビーのいつもの幕を使う）。</summary>
        public delegate bool CoverTry(Action onCovered, Func<bool> isReady);

        /// <summary>
        /// キャラクター選択の「GO!」からゲームへ進むときの幕。キャラ選択の側（歯磨き粉の幕）が起動時に入れる。
        /// null、または false を返したらロビーのいつもの幕（gameTransition）を使う。
        /// </summary>
        public static CoverTry GameStartCurtain;

        /// <summary>キャラクター選択からゲームへ進むときの幕を出す（差し替えが無ければ gameTransition）。</summary>
        private void PlayGameStartCover(Action onCovered, Func<bool> isReady)
        {
            if (GameStartCurtain != null && GameStartCurtain(onCovered, isReady))
            {
                return;
            }

            var root = UiRoot.Active;
            if (root != null)
            {
                root.PlayTransition(gameTransition, onCovered, isReady);
            }
            else
            {
                onCovered?.Invoke();
            }
        }

        /// <summary>キャラクター選択に入るときの幕を出す（差し替えが無ければ gameTransition）。</summary>
        private void PlayCharacterSelectCover(Action onCovered, Func<bool> isReady)
        {
            if (CharacterSelectCurtain != null)
            {
                CharacterSelectCurtain(onCovered, isReady);
                return;
            }

            var root = UiRoot.Active;
            if (root != null)
            {
                root.PlayTransition(gameTransition, onCovered, isReady);
            }
            else
            {
                onCovered?.Invoke();
            }
        }

        /// <summary>「ゲーム開始」のあとキャラクター選択を挟むか。</summary>
        public bool UsesCharacterSelect => !string.IsNullOrEmpty(characterSelectSceneName);

        public bool IsCharacterSelectLoaded => UsesCharacterSelect && SceneManager.GetSceneByName(characterSelectSceneName).isLoaded;

        /// <summary>ゲームかキャラクター選択のシーン（ロビーの上に重ねるシーン）が読み込まれているか。</summary>
        private bool IsMatchSceneLoaded => IsGameLoaded || IsCharacterSelectLoaded;

        private bool IsCharacterSelectScene(Scene scene) => UsesCharacterSelect && scene.name == characterSelectSceneName;

        // ───────── ホスト: ロビー → キャラクター選択 ─────────

        private void BeginCharacterSelect()
        {
            if (!IsHost || Roster.phase == LobbyPhase.CharacterSelect)
            {
                return;
            }

            ChangePhase(LobbyPhase.CharacterSelect);
            Action load = () =>
            {
                var status = NetworkManager.Singleton.SceneManager.LoadScene(characterSelectSceneName, LoadSceneMode.Additive);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogWarning($"[Lobby] キャラクター選択のシーン「{characterSelectSceneName}」を読み込めませんでした（{status}）。ビルドの設定にシーンが入っているか確かめてください。");
                    ChangePhase(LobbyPhase.Waiting);
                    ShowMessage("キャラクター選択に進めませんでした", $"シーン「{characterSelectSceneName}」を読み込めませんでした（{status}）。");
                }
            };

            PlayCharacterSelectCover(load, () => IsCharacterSelectLoaded || Roster.phase == LobbyPhase.Waiting);
        }

        // ───────── ホスト: キャラクター選択 → ゲーム ─────────

        /// <summary>
        /// 全員のキャラクターが決まったら、選択画面（ホストのPC）が呼ぶ。幕で隠して選択のシーンを外し、ゲームのシーンを全員で読み込む。
        /// ホスト以外・キャラクター選択中でないときは何もしない。
        /// </summary>
        public void ProceedToGame()
        {
            if (!IsHost || Roster.phase != LobbyPhase.CharacterSelect || !IsCharacterSelectLoaded)
            {
                return;
            }

            // 参加側は「読み込み中」を受け取ると、ゲームのシーンが届くまで幕で隠す（BeginLoadGame と同じ）。
            ChangePhase(LobbyPhase.Loading);
            PlayGameStartCover(() => StartCoroutine(SwapCharacterSelectForGame()), () => IsGameLoaded || Roster.phase == LobbyPhase.Waiting);
        }

        private IEnumerator SwapCharacterSelectForGame()
        {
            var deadline = Time.unscaledTime + SceneSwapTimeoutSeconds;
            // NGO のシーンの出来事は1つずつしか進められない（途中から入った人の読み込み中など）ので、空くまで待って何度か試す。
            var unloadStarted = !IsCharacterSelectLoaded;
            while (!unloadStarted && IsOnline)
            {
                var unloadStatus = NetworkManager.Singleton.SceneManager.UnloadScene(SceneManager.GetSceneByName(characterSelectSceneName));
                unloadStarted = unloadStatus == SceneEventProgressStatus.Started;
                if (!unloadStarted && (unloadStatus != SceneEventProgressStatus.SceneEventInProgress || Time.unscaledTime > deadline))
                {
                    FailToRoom("ゲームを始められませんでした", $"キャラクター選択のシーンを外せませんでした（{unloadStatus}）。");
                    yield break;
                }

                if (!unloadStarted)
                {
                    yield return null;
                }
            }

            // 外し終わるのを待ってから読み込む。
            while (IsCharacterSelectLoaded && IsOnline && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            while (IsOnline)
            {
                var status = NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Additive);
                if (status == SceneEventProgressStatus.Started)
                {
                    yield break;
                }

                if (status != SceneEventProgressStatus.SceneEventInProgress || Time.unscaledTime > deadline)
                {
                    Debug.LogWarning($"[Lobby] ゲームのシーン「{gameSceneName}」を読み込めませんでした（{status}）。ビルドの設定にシーンが入っているか確かめてください。");
                    FailToRoom("ゲームを始められませんでした", $"シーン「{gameSceneName}」を読み込めませんでした（{status}）。");
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>ホスト: キャラクター選択からゲームへ進めなかったので、全員を部屋の画面に戻す。</summary>
        private void FailToRoom(string title, string body)
        {
            // 先に「待っている」に戻す（選択のシーンを外したときに「ゲームへ進む途中」と見なされないように）。
            ChangePhase(LobbyPhase.Waiting);
            if (IsCharacterSelectLoaded)
            {
                // 外れたら HandleSceneUnloaded が部屋の画面に戻す。
                var status = NetworkManager.Singleton.SceneManager.UnloadScene(SceneManager.GetSceneByName(characterSelectSceneName));
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogWarning($"[Lobby] キャラクター選択のシーンを外せませんでした（{status}）。Esc の一時メニューの「全員でロビーに戻る」を試してください。");
                }
            }
            else
            {
                Roster.ClearReady();
                ShowRoomAgain();
            }

            ShowMessage(title, body);
        }

        // ───────── 外す ─────────

        /// <summary>ホスト: 重ねているシーン（ゲーム、なければキャラクター選択）を全員のPCから外す。</summary>
        private void UnloadMatchSceneForEveryone()
        {
            var sceneManager = NetworkManager.Singleton.SceneManager;
            if (IsGameLoaded)
            {
                sceneManager.UnloadScene(GameScene);
            }
            else if (IsCharacterSelectLoaded)
            {
                sceneManager.UnloadScene(SceneManager.GetSceneByName(characterSelectSceneName));
            }
        }

        /// <summary>通信を止めた後など、このPCだけでキャラクター選択のシーンを外す。</summary>
        private void UnloadCharacterSelectLocally()
        {
            if (IsCharacterSelectLoaded)
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(characterSelectSceneName));
            }
        }

        // ───────── 全員: シーン・部屋の状態の変化 ─────────

        private void HandleCharacterSelectLoaded(Scene scene)
        {
            // 光・空・新しく作る物（キャラクターのモデルなど）の置き場所を選択のシーンに合わせる。
            SceneManager.SetActiveScene(scene);
            ShowLobbyLook(false);
            var root = UiRoot.Active;
            if (root != null)
            {
                root.CloseLayer("Menu");
                root.Close(countdownScreen);
            }

            // 試合中と同じ扱い（Esc の一時メニューで「全員でロビーに戻る」「抜ける」「タイトルへ」が使える）。
            SetState(SessionState.InGame);
        }

        private void OnCharacterSelectPhase(LobbyPhase before, LobbyPhase after)
        {
            if (IsHost)
            {
                return;
            }

            if (after == LobbyPhase.CharacterSelect && before != LobbyPhase.CharacterSelect && !IsCharacterSelectLoaded)
            {
                // 参加側は NGO が自動で選択のシーンを読み込む（途中から入った人も）。読み終わるまで幕で隠す。
                PlayCharacterSelectCover(null, () => IsCharacterSelectLoaded || IsGameLoaded || !IsOnline || Roster.phase == LobbyPhase.Waiting);
            }

            // ホストがゲームへ進めなかった（選択のシーンはもう外れている）→ 部屋の画面へ。
            if (after == LobbyPhase.Waiting && State == SessionState.InGame && !IsMatchSceneLoaded)
            {
                ShowRoomAgain();
            }
        }

        /// <summary>重ねたシーンが無いまま試合中の扱いになっているとき、ロビーの見た目と部屋の画面に戻す。</summary>
        private void ShowRoomAgain()
        {
            ShowLobbyLook(true);
            UiRoot.Active?.CloseLayer("HUD");
            _localReady = false;
            _forcedStart = false;
            if (IsOnline)
            {
                SetState(SessionState.Room);
                OpenMenu(roomScreen);
            }
        }
    }
}
