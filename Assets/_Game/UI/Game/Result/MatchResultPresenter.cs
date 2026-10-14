using MS2026.Fortress;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合が終わったら（<see cref="FortressMatch.Ended"/>）リザルト画面（UIスタジオの画面 <see cref="ResultScreenId"/>）を開き、
    /// 中身の値（result.* / player.N.resultKills など）を <see cref="ResultRules"/> の順番どおりに1つずつ画面へ書いていく（数字はその時に数え上がる）。
    /// 最後の段階になったら、だれかが握り続ける（キーボードなら A / Enter を押し続ける）か、<see cref="AutoSeconds"/> 秒たったら、全員でタイトルへ戻る。
    /// ・ロビーから来たとき: <see cref="GameSession.ReturnToTitleFromResult"/>（ホストが全員に「タイトルへ」を配る）
    /// ・ゲームのシーンだけで遊んでいるとき: タイトルのシーンを読み込む（ビルドの設定に無ければ、ゲームのシーンを読み直す）
    /// 起動時に自動で作られる（シーンに置く物は無い）。画面が無いとき（UIスタジオで作っていない）は、ログを出してタイトルへの移動だけ行う。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MatchResultPresenter : MonoBehaviour
    {
        public const string ResultScreenId = "Result";
        public const string HudScreenId = "Hud";
        public const float AutoSeconds = 60f;
        public const float HoldSeconds = 1.2f;
        public const float GripThreshold = 0.35f;

        private bool _showing;
        private bool _clear;
        private float _startedAt;
        private int _step;
        private float _hold;
        private bool _holdArmed;
        private bool _leaving;
        private MatchResult _result;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("[UI] MatchResult");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<MatchResultPresenter>();
        }

        private void OnEnable()
        {
            FortressMatch.Ended += OnEnded;
        }

        private void OnDisable()
        {
            FortressMatch.Ended -= OnEnded;
        }

        private void OnEnded(MatchResult result)
        {
            _result = result;
            _clear = result.outcome == MatchOutcome.Clear;
            _startedAt = Time.unscaledTime;
            _step = ResultRules.StepNone;
            _hold = 0f;
            _holdArmed = false;
            _leaving = false;
            _showing = true;
            ClearValues();
            UiValues.Set(FortressUiKeys.ResultClear, _clear);

            var root = UiRoot.Active;
            if (root == null || !root.Open(ResultScreenId))
            {
                Debug.LogWarning($"[Result] リザルト画面「{ResultScreenId}」がありません。UIスタジオの「はじめに」の「リザルト画面を作る」で作ってください（今回はそのまま進みます）。");
            }
        }

        private void Update()
        {
            if (!_showing)
            {
                return;
            }

            // 新しい試合が始まった（リザルトの途中で全員がロビーに戻された など）、またはゲームのシーンを出た → 片づける。
            // 出た後も残すと、ほかのPCの合図でタイトルへ戻ったPCで「60秒たった／握り続けた」がタイトル・キャラ選択でも数えられ、
            // 次のキャラ選択で READY のために握ったときなどに、もう一度「全員でタイトルへ」を出してしまう（2026-10-14 実機で発生）。
            if (!FortressMatch.HasEnded || !FortressMatch.InMatch)
            {
                Close();
                return;
            }

            var elapsed = Time.unscaledTime - _startedAt;
            var step = ResultRules.StepAt(_clear, elapsed);
            while (_step < step)
            {
                _step++;
                Reveal(_step);
            }

            var panelAt = ResultRules.PanelTime(_clear);
            var autoLeft = Mathf.Max(0f, AutoSeconds - Mathf.Max(0f, elapsed - panelAt));
            UiValues.Set(FortressUiKeys.ResultAutoSeconds, Mathf.Ceil(autoLeft));
            if (_step < ResultRules.StepNext)
            {
                return;
            }

            TickHold();
            if (_hold >= 1f || autoLeft <= 0f)
            {
                GoNext();
            }
        }

        // 段階ごとに、その段階の値を画面へ書く（書いた瞬間に数字が数え上がり、出てくる動きが始まる）。
        private void Reveal(int step)
        {
            UiValues.Set(FortressUiKeys.ResultStep, step);
            switch (step)
            {
                case ResultRules.StepPanel:
                    UiRoot.Active?.Close(HudScreenId);
                    PublishCoreColors();
                    UiValues.Set(FortressUiKeys.ResultTotalKills, _result.totalKills);
                    for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
                    {
                        UiValues.Set(FortressUiKeys.PlayerInResult(p), IsInResult(p));
                    }

                    break;
                case ResultRules.StepDurability:
                    UiValues.Set(FortressUiKeys.ResultDurability01, _result.coreHealth01);
                    break;
                case ResultRules.StepBrightness:
                    UiValues.Set(FortressUiKeys.ResultBrightness01, Brightness());
                    break;
                case ResultRules.StepPlayers:
                    for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
                    {
                        UiValues.Set(FortressUiKeys.PlayerResultKills(p), Get(_result.kills, p));
                    }

                    break;
                case ResultRules.StepCombos:
                    var mvp = _result.Mvp;
                    for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
                    {
                        UiValues.Set(FortressUiKeys.PlayerMaxCombo(p), Get(_result.maxCombos, p));
                        UiValues.Set(FortressUiKeys.PlayerIsMvp(p), p == mvp);
                    }

                    break;
                case ResultRules.StepRank:
                    var rank = ResultRules.Rank(_result.coreHealth01);
                    UiValues.Set(FortressUiKeys.ResultRank, rank);
                    UiValues.Set(FortressUiKeys.ResultRankColor, ResultRules.RankColor(rank));
                    UiValues.Set(FortressUiKeys.ResultRankComment, ResultRules.RankComment(rank));
                    break;
            }
        }

        private bool IsInResult(int player)
        {
            UiValues.TryGet(FortressUiKeys.PlayerConnected(player), out var connected);
            return connected.AsBool || Get(_result.kills, player) > 0;
        }

        private static int Get(int[] values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0;

        private void PublishCoreColors()
        {
            var dirt = FindFirstObjectByType<CoreDirtVisual>();
            var before = dirt != null ? dirt.CleanColor : new Color(0.75f, 0.95f, 1f);
            var after = dirt != null ? dirt.ColorAt(_result.coreHealth01) : Color.Lerp(new Color(0.3f, 0.22f, 0.18f), before, _result.coreHealth01);
            UiValues.Set(FortressUiKeys.ResultCoreColorBefore, before);
            UiValues.Set(FortressUiKeys.ResultCoreColorAfter, after);
        }

        // 輝き = 汚れの少なさ（コアの見た目の色の変わり方と同じ計算）。
        private float Brightness()
        {
            var dirt = FindFirstObjectByType<CoreDirtVisual>();
            return dirt != null ? 1f - dirt.DirtAt(_result.coreHealth01) : _result.coreHealth01;
        }

        // ───────── 次へ（握り続ける） ─────────

        private void TickHold()
        {
            var pressed = GripPressed() || KeyPressed();
            // 最後の段階になったときに握っていたら、一度ゆるめるまで数えない（試合中の握りのまま進まないように）。
            if (!_holdArmed)
            {
                _holdArmed = !pressed;
                _hold = 0f;
            }
            else if (pressed)
            {
                _hold += Time.unscaledDeltaTime / HoldSeconds;
            }
            else
            {
                _hold = Mathf.Max(0f, _hold - Time.unscaledDeltaTime * 2f / HoldSeconds);
            }

            UiValues.Set(FortressUiKeys.ResultNextHold01, Mathf.Clamp01(_hold));
        }

        private static bool GripPressed()
        {
            var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
            if (provider == null)
            {
                return false;
            }

            for (var p = 0; p < 4; p++)
            {
                if (provider.GetGripValue(p) >= GripThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool KeyPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.aKey.isPressed || keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed);
        }

        private void GoNext()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            var session = GameSession.Active;
            if (session != null)
            {
                session.ReturnToTitleFromResult();
                return;
            }

            // ゲームのシーンだけで遊んでいるとき。
            Close();
            var title = session != null ? session.titleSceneName : "Title";
            if (Application.CanStreamedLevelBeLoaded(title))
            {
                SceneManager.LoadScene(title);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void Close()
        {
            _showing = false;
            UiRoot.Active?.Close(ResultScreenId);
            ClearValues();
        }

        private static void ClearValues()
        {
            UiValues.Set(FortressUiKeys.ResultStep, ResultRules.StepNone);
            UiValues.Set(FortressUiKeys.ResultDurability01, 0f);
            UiValues.Set(FortressUiKeys.ResultBrightness01, 0f);
            UiValues.Set(FortressUiKeys.ResultTotalKills, 0);
            UiValues.Set(FortressUiKeys.ResultRank, string.Empty);
            UiValues.Set(FortressUiKeys.ResultRankComment, string.Empty);
            UiValues.Set(FortressUiKeys.ResultNextHold01, 0f);
            for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
            {
                UiValues.Set(FortressUiKeys.PlayerResultKills(p), 0);
                UiValues.Set(FortressUiKeys.PlayerMaxCombo(p), 0);
                UiValues.Set(FortressUiKeys.PlayerIsMvp(p), false);
            }
        }
    }
}
