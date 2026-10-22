using System;
using System.Collections;
using MS2026.Stages;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    // ステージ（2026-10-21、ユーザー決定: ステージはランダム。キャラ選択の後にルーレットの画面を挟む／共通のゲームのシーン＋ステージの中身のシーン）。
    // ・ホスト: 全員が決めたら、ステージの一覧（Resources/StageCatalog）からランダムに選び、部屋の状態（stageIndex と段階 StageRoulette）で全員に配る。
    //   全員の PC がルーレットを見せ、ホストは見せ終わったら「ステージ → ゲーム」の順に NGO で重ねて読み込む
    //   （ゲームのシーンの通信の部品が動き出す時点で、ステージの壊せる物がそろっているように）。
    // ・ゲームのシーンが外れたら、ステージのシーンも外す。
    // ・ステージの一覧が無い／空なら、今までどおりゲームのシーンだけを読み込む。
    public sealed partial class GameSession
    {
        private const float StageLoadTimeoutSeconds = 25f;

        private static int _lastStageIndex = -1;
        private Coroutine _stageRoulette;
        private Coroutine _stageUnload;

        private static StageCatalog Stages => StageCatalog.Load();

        /// <summary>ステージを使うか（一覧があって、読み込めるステージが 1 つ以上ある）。</summary>
        private static bool UsesStages
        {
            get
            {
                var catalog = Stages;
                if (catalog == null)
                {
                    return false;
                }

                for (var i = 0; i < catalog.Count; i++)
                {
                    if (catalog.IsPlayable(i))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>ステージのシーンが読み込まれているか。</summary>
        public bool IsStageLoaded => Stages != null && Stages.FindLoadedStageScene().isLoaded;

        /// <summary>この試合のステージのシーンの名前（決まっていなければ null）。</summary>
        private string ChosenStageSceneName
        {
            get
            {
                var stage = Stages != null ? Stages.Get(Roster.stageIndex) : null;
                return stage != null ? stage.sceneName : null;
            }
        }

        /// <summary>ホスト: この試合のステージを選ぶ（ルーレットに出るステージからランダム。無ければ読み込める最初の物）。</summary>
        private int ChooseStage()
        {
            var catalog = Stages;
            if (catalog == null)
            {
                return -1;
            }

            var index = catalog.PickRandom(_lastStageIndex);
            if (index < 0)
            {
                for (var i = 0; i < catalog.Count && index < 0; i++)
                {
                    if (catalog.IsPlayable(i))
                    {
                        index = i;
                    }
                }
            }

            if (index >= 0)
            {
                _lastStageIndex = index;
            }

            return index;
        }

        // ───────── ホスト: キャラ選択 → ルーレット → ゲーム ─────────

        /// <summary>
        /// ホスト: キャラ選択が終わったとき。ステージを選び、ルーレットを見せるなら段階を StageRoulette にして true を返す
        /// （見せ終わったら <see cref="ProceedAfterRoulette"/> がゲームへ進める）。ルーレットを見せないなら false（すぐゲームへ）。
        /// </summary>
        private bool TryBeginStageRoulette()
        {
            if (!UsesStages)
            {
                Roster.SetStage(-1);
                return false;
            }

            Roster.SetStage(ChooseStage());
            var settings = Stages.roulette;
            if (settings == null || !settings.enabled || Stages.RouletteCount < 2 || Roster.stageIndex < 0)
            {
                return false;
            }

            ChangePhase(LobbyPhase.StageRoulette);
            if (_stageRoulette != null)
            {
                StopCoroutine(_stageRoulette);
            }

            _stageRoulette = StartCoroutine(WaitForRoulette(settings.TotalSeconds));
            return true;
        }

        private IEnumerator WaitForRoulette(float seconds)
        {
            var end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && IsHost && Roster.phase == LobbyPhase.StageRoulette)
            {
                yield return null;
            }

            _stageRoulette = null;
            if (IsHost && Roster.phase == LobbyPhase.StageRoulette)
            {
                ProceedAfterRoulette();
            }
        }

        /// <summary>全員: 段階が StageRoulette になったら、選ばれたステージで止まるルーレットを見せる。</summary>
        private void OnStageRoulettePhase(LobbyPhase before, LobbyPhase after)
        {
            if (after == LobbyPhase.StageRoulette && before != LobbyPhase.StageRoulette)
            {
                // ゲームのシーンが届くまで（歯磨き粉の幕の下で）出したまま。中止されたら消える。
                StageRouletteView.Play(Stages, Roster.stageIndex,
                    () => IsOnline && !IsGameLoaded && (Roster.phase == LobbyPhase.StageRoulette || Roster.phase == LobbyPhase.Loading));
            }
        }

        // ───────── ホスト: ステージ → ゲームの順に読み込む ─────────

        /// <summary>
        /// ホスト: ステージ（決まっていれば）を全員で読み込み、読み終わったらゲームのシーンを読み込む。
        /// NGO のシーンの出来事は 1 つずつなので、前の出来事（参加側の読み込み）が終わるまで待って何度か試す。
        /// 失敗したら fail(見出し, 本文) を呼ぶ。
        /// </summary>
        private IEnumerator LoadStageThenGame(Action<string, string> fail)
        {
            var deadline = Time.unscaledTime + StageLoadTimeoutSeconds;
            var stageScene = ChosenStageSceneName;
            if (!string.IsNullOrEmpty(stageScene) && !SceneManager.GetSceneByName(stageScene).isLoaded)
            {
                var started = false;
                while (!started && IsOnline)
                {
                    var status = NetworkManager.Singleton.SceneManager.LoadScene(stageScene, LoadSceneMode.Additive);
                    started = status == SceneEventProgressStatus.Started;
                    if (!started && (status != SceneEventProgressStatus.SceneEventInProgress || Time.unscaledTime > deadline))
                    {
                        Debug.LogWarning($"[Lobby] ステージのシーン「{stageScene}」を読み込めませんでした（{status}）。ビルドの設定にシーンが入っているか確かめてください。");
                        fail("ゲームを始められませんでした", $"ステージ「{stageScene}」を読み込めませんでした（{status}）。");
                        yield break;
                    }

                    if (!started)
                    {
                        yield return null;
                    }
                }

                while (!SceneManager.GetSceneByName(stageScene).isLoaded && IsOnline && Time.unscaledTime < deadline)
                {
                    yield return null;
                }
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
                    fail("ゲームを始められませんでした", $"シーン「{gameSceneName}」を読み込めませんでした（{status}）。");
                    yield break;
                }

                yield return null;
            }
        }

        // ───────── 外す ─────────

        /// <summary>
        /// ゲームのシーンが外れたとき（全員）。ホストは NGO で全員のステージを外す。通信していなければこの PC だけで外す。
        /// 参加側は何もしない（ホストが外すと NGO が外す）。
        /// </summary>
        private void UnloadStageAfterGame()
        {
            if (!IsStageLoaded)
            {
                return;
            }

            if (!IsOnline)
            {
                UnloadStageLocally();
                return;
            }

            if (!IsHost)
            {
                return;
            }

            if (_stageUnload != null)
            {
                StopCoroutine(_stageUnload);
            }

            _stageUnload = StartCoroutine(UnloadStageForEveryone());
        }

        private IEnumerator UnloadStageForEveryone()
        {
            var deadline = Time.unscaledTime + StageLoadTimeoutSeconds;
            while (IsStageLoaded && IsOnline && IsHost)
            {
                var scene = Stages.FindLoadedStageScene();
                var status = NetworkManager.Singleton.SceneManager.UnloadScene(scene);
                if (status == SceneEventProgressStatus.Started)
                {
                    break;
                }

                if (status != SceneEventProgressStatus.SceneEventInProgress || Time.unscaledTime > deadline)
                {
                    Debug.LogWarning($"[Lobby] ステージのシーン「{scene.name}」を外せませんでした（{status}）。このPCだけで外します。");
                    UnloadStageLocally();
                    break;
                }

                yield return null;
            }

            _stageUnload = null;
        }

        /// <summary>この PC だけでステージのシーンを外す（通信を止めた後など）。</summary>
        private void UnloadStageLocally()
        {
            var catalog = Stages;
            if (catalog == null)
            {
                return;
            }

            var scene = catalog.FindLoadedStageScene();
            if (scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        /// <summary>ルーレットを止めて消す（中止・抜けたとき）。</summary>
        private void CancelStageRoulette()
        {
            if (_stageRoulette != null)
            {
                StopCoroutine(_stageRoulette);
                _stageRoulette = null;
            }

            StageRouletteView.Hide();
        }
    }
}
