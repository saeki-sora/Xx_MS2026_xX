using MS2026.CharacterSelect;
using UnityEditor;
using UnityEngine;

namespace MS2026.HowToPlay.EditorTools
{
    /// <summary>
    /// 練習の画面「新人研修」を、通信もキャラ選択も無しで試す（エディタの再生中だけ）。
    /// 自分 = キャラ選択で自分が入った席（通信していなければ 1P）。このPCの握力（キーボードなら Q）で動かす。ほかの3席は自動で動かす（それぞれ違う速さで研修を終える）。
    /// キャラ選択を通っていなければ、キャラの一覧の上から順に 1P〜4P に入れる。全員終わると数秒後にもう一度始まる。
    /// </summary>
    internal sealed class PracticeScreenPreview : MonoBehaviour
    {
        private const string RosterPath = "Assets/CharacterSelect/Data/CharacterRoster.asset";

        private readonly PracticeBoard _board = new PracticeBoard();
        private readonly PracticeTrainer[] _bots = new PracticeTrainer[PracticeBoard.SeatCount];
        private readonly float[] _botDelay = { 0f, 0.4f, 1.6f, 0.9f };
        private readonly float[] _botSkillAt = new float[PracticeBoard.SeatCount];
        private PracticeTrainer _local;
        private int _seat;
        private HowToPlaySettings _settings;
        private float _time;
        private float _finishedAt = -1f;

        [MenuItem("Tools/あそびかた/練習の画面を試す（再生中に押す）")]
        private static void Toggle()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("練習の画面を試す", "再生してから押してください（どのシーンでも試せます）。\n自分は 1P（キーボードなら Q で握る）、2P〜4P は自動で動きます。", "OK");
                return;
            }

            var existing = FindAnyObjectByType<PracticeScreenPreview>();
            if (existing != null)
            {
                PracticeScreen.Hide(true);
                Destroy(existing.gameObject);
                return;
            }

            if (!CharacterSelectionResult.HasResult)
            {
                var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
                if (roster != null)
                {
                    var chosen = new CharacterDefinition[PracticeBoard.SeatCount];
                    for (var i = 0; i < chosen.Length; i++)
                    {
                        chosen[i] = roster.Get(i % Mathf.Max(1, roster.Count));
                    }

                    CharacterSelectionResult.Store(chosen);
                }
            }

            var go = new GameObject("[HowToPlay] Practice Preview");
            DontDestroyOnLoad(go);
            go.AddComponent<PracticeScreenPreview>();
        }

        private void Start()
        {
            _settings = HowToPlaySettings.Load();
            Restart();
        }

        private void Restart()
        {
            _time = 0f;
            _finishedAt = -1f;
            var bootstrap = MS2026.Fortress.Net.FortressNetworkBootstrap.Instance;
            _seat = bootstrap != null && PracticeBoard.IsValid(bootstrap.LocalPlayerIndex) ? bootstrap.LocalPlayerIndex : 0;
            _board.Reset(_settings.timeLimitSeconds);
            for (var i = 0; i < PracticeBoard.SeatCount; i++)
            {
                _board.SetPresent(i, true);
                _bots[i] = null;
                if (i != _seat)
                {
                    _bots[i] = new PracticeTrainer();
                    _bots[i].Reset();
                }
            }

            // 自分の係は最後に作る（画面は「いちばん最近始めた係」を自分の砲台として描く）。
            _local = new PracticeTrainer();
            _local.Reset();
            var plate = _settings.screen != null ? _settings.screen.FindPlate(null) : null;
            PracticeScreen.Play(_settings, _board, _seat, null, true, plate != null ? plate.sceneName : null);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _time += dt;
            var rules = _settings.rules;
            var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
            var gripping = provider != null && provider.IsGripping(0);
            var grip = provider != null ? provider.GetGripValue(0) : 0f;
            if (_board.finished)
            {
                _local.Complete();
            }

            _local.Tick(gripping, grip, dt, rules);
            _board.SetSeat(_seat, _local.Grip01, _local.Heat01, _local.Gripping, _local.Overheated, _local.Step);

            for (var i = 0; i < PracticeBoard.SeatCount; i++)
            {
                var bot = _bots[i];
                if (bot == null)
                {
                    continue;
                }

                if (_board.finished)
                {
                    bot.Complete();
                }

                var botGrip = BotGrip(i, bot);
                var events = bot.Tick(botGrip > 0f, botGrip, dt, rules);
                if ((events & PracticeEvents.BubbleBroken) != 0)
                {
                    _botSkillAt[i] = _time + 0.8f + (_botDelay[i] * 0.5f);
                }

                _board.SetSeat(i, bot.Grip01, bot.Heat01, bot.Gripping, bot.Overheated, bot.Step);
            }

            _board.Tick(dt);
            if (!_board.finished && (_board.AllDone || _board.TimeUp))
            {
                _board.Finish();
            }

            if (_board.finished)
            {
                if (_finishedAt < 0f)
                {
                    _finishedAt = _time;
                }
                else if (_time - _finishedAt > 5f)
                {
                    Restart();
                }
            }
        }

        /// <summary>2P〜4P の「人のつもり」の握り方（課題を見て、少し遅れて反応する）。</summary>
        private float BotGrip(int seat, PracticeTrainer bot)
        {
            var t = _time - 3.5f - _botDelay[seat];
            if (t < 0f)
            {
                return 0f;
            }

            var wobble = 0.05f * Mathf.Sin((_time * 7f) + seat);
            switch (bot.Step)
            {
                case PracticeStep.Grip:
                case PracticeStep.Hold:
                    return 0.55f + wobble;
                case PracticeStep.Release:
                case PracticeStep.Cool:
                    return 0f;
                case PracticeStep.Overheat:
                    return 0.45f + wobble;
                case PracticeStep.Bubble:
                    return Mathf.Abs(Mathf.DeltaAngle(bot.TurretDegrees, bot.BubbleDegrees)) < 40f && !bot.Overheated ? 0.6f : 0f;
                case PracticeStep.Skill:
                    return _time >= _botSkillAt[seat] ? 0.6f : 0f;
                default:
                    return Mathf.Repeat((_time * 0.6f) + seat, 3f) < 1f ? 0.5f : 0f;
            }
        }

        private void OnDestroy()
        {
            PracticeScreen.Hide(true);
        }
    }
}
