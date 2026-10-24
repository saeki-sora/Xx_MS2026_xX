using System;
using MS2026.Fortress;
using MS2026.UI;
using MS2026.UI.Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラクター選択の進行役（キャラ選択のシーンに1つ）。
    /// ・短く握る（仮: A キー）→ 次のキャラ、長く握る → 決定（決定後にもう一度長く握ると取り消し）
    /// ・全員が決めたら「全員決定！」を見せて、ロビーから来たときはホストが全員でゲームへ進める（<see cref="GameSession.ProceedToGame"/>）。
    ///   このシーンだけで再生したときは、1台で P1〜P4 をキー（A/S/D/F）で試せ、決まったら <see cref="offlineNextScene"/> を読み込む。
    /// ネット対戦ではホストだけがルールを動かし（<see cref="CharacterSelectBoard"/>）、変わるたびに全員へ配る。参加側は自分の操作をホストへ送るだけ。
    /// 見た目は <see cref="CharacterSelectView"/>。選んだ結果は <see cref="CharacterSelectionResult"/>（ゲームのシーンから読む）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("キャラ選択/キャラクター選択の進行 (CharacterSelectController)")]
    public sealed class CharacterSelectController : MonoBehaviour
    {
        [Header("キャラクター")]
        [Tooltip("選べるキャラの一覧。キャラを足す・並べ替えるときはこの一覧を直す（中身の差し替えは各キャラのファイルで）。")]
        public CharacterRoster roster;

        [Header("ルール")]
        [Tooltip("OFF: 誰かが決定したキャラは、ほかの人は選べない（早い者勝ち）。\nON: 同じキャラを何人でも選べる。\nネット対戦ではホストのPCの設定が全員に使われる。再生中に切り替えてもよい。")]
        public bool allowSameCharacter;

        [Tooltip("この秒数まで握り続けると「決定（READY）」。それより前に離すと「次のキャラ」。")]
        [Range(0.2f, 2f)]
        public float longPressSeconds = 0.6f;

        [Tooltip("ONなら、決定した後にもう一度長く握ると決定を取り消せる。")]
        public bool longPressCancels = true;

        [Tooltip("制限時間（秒）。0 なら無し。時間切れになると、まだ決めていない人は今のキャラ（使われていれば空いているキャラ）に決まる。")]
        [Min(0f)]
        public float timeLimitSeconds;

        [Tooltip("全員が決まってから次へ進むまでの秒数（「GO!」を見せる時間）。")]
        [Min(0f)]
        public float finishedHoldSeconds = 1.5f;

        [Header("入力（仮）")]
        [Tooltip("このPCのプレイヤーが使うキー（握力センサーの代わりの仮のキー）。")]
        public Key key = Key.A;

        [Tooltip("ONなら握力センサー（握力入力ブリッジ）でも操作できる（キーボードの握力の代わり Q なども効く）。ネット対戦では各PCのセンサーを P1 として読む。")]
        public bool useGripSensor = true;

        [Tooltip("ONなら、握力センサーのキーボードでの代わり（Q・W・O・P キー）では操作しない（本物のセンサーだけを読む）。キャラ選択は仮のキー（A）で操作するため。")]
        public bool ignoreKeyboardGrip = true;

        [Tooltip("握力センサー: この強さ（0〜1）以上で「握った」。")]
        [Range(0.05f, 1f)]
        public float gripOnThreshold = 0.35f;

        [Tooltip("握力センサー: この強さ未満で「離した」。")]
        [Range(0f, 1f)]
        public float gripOffThreshold = 0.2f;

        [Tooltip("握力センサー: いちばん強く握ったところから、これだけゆるんだら「離した」と数える（すばやい握り直し＝連打を1回ずつ数えるため）。")]
        [Range(0.05f, 1f)]
        public float gripReleaseDip = 0.25f;

        [Header("このシーンだけで試すとき（ロビーを通さずに再生）")]
        [Tooltip("参加する人数（P1 から順に）。")]
        [Range(1, CharacterSelectBoard.SeatCount)]
        public int offlinePlayers = CharacterSelectBoard.SeatCount;

        [Tooltip("P1〜P4 のキー（1台で4人分を試すため）。握力センサーは P1〜P4 の番号のまま読む。")]
        public Key[] offlineKeys = { Key.A, Key.S, Key.D, Key.F };

        [Tooltip("全員が決まったら読み込むシーン（空なら何もしない）。ロビーから来たときは使わない（全員でゲームのシーンへ進む）。")]
        public string offlineNextScene = "Game";

        [Header("音・演出（D-Drive の番号札。空なら鳴らさない）")]
        [Tooltip("カーソルが次のキャラへ動いたとき（そのキャラの位置・その人の色）。")]
        public FortressEffect onMove = new FortressEffect();

        [Tooltip("キャラを決定したとき（そのキャラの位置・その人の色）。")]
        public FortressEffect onConfirm = new FortressEffect();

        [Tooltip("決定を取り消したとき。")]
        public FortressEffect onCancel = new FortressEffect();

        [Tooltip("ほかの人が決定済みのキャラで決定しようとしたとき（そのPCだけで鳴る）。")]
        public FortressEffect onRejected = new FortressEffect();

        [Tooltip("全員のキャラが決まったとき。")]
        public FortressEffect onAllDecided = new FortressEffect();

        [Header("参照")]
        [Tooltip("見た目を受け持つ部品（空なら同じ物・シーンから探す）。")]
        public CharacterSelectView view;

        private static readonly string[] DefaultNames = { "P1", "P2", "P3", "P4" };

        private readonly GripPressDetector[] _detectors = new GripPressDetector[CharacterSelectBoard.SeatCount];
        private readonly GripSqueezeTracker[] _grips = new GripSqueezeTracker[CharacterSelectBoard.SeatCount];
        private readonly int[] _holdCursor = new int[CharacterSelectBoard.SeatCount];
        private readonly System.Collections.Generic.List<KeyPressTracker> _keys = new System.Collections.Generic.List<KeyPressTracker>();
        private readonly CharacterSelectBoard.Seat[] _seen = new CharacterSelectBoard.Seat[CharacterSelectBoard.SeatCount];
        private GameSession _session;
        private NetworkManager _registered;
        private int _seenVersion = -1;
        private bool _seenFinished;
        private int _sentVersion = -1;
        private bool _hasState;
        private float _proceedAt = -1f;
        private bool _proceeded;

        /// <summary>選択の状態（ネット対戦の参加側はホストから届いた物）。</summary>
        public CharacterSelectBoard Board { get; } = new CharacterSelectBoard();

        /// <summary>ロビーを通さずにこのシーンだけで再生している（1台で P1〜P4 を試す）。</summary>
        public bool IsStandalone => _session == null;

        /// <summary>ロビーから来て、通信している。</summary>
        public bool IsOnline => _session != null && _session.IsOnline;

        /// <summary>ルールを動かす側（このシーンだけのとき、またはホスト）。</summary>
        public bool IsAuthority => IsStandalone || (IsOnline && _session.IsHost);

        /// <summary>状態を受け取って、操作・表示できる。</summary>
        public bool HasState => _hasState;

        /// <summary>次の画面へ進むまでの秒数（全員決定の後、ルールを動かす側だけ。決まっていなければ -1、進んだ後は 0）。</summary>
        public float SecondsUntilProceed => _proceedAt < 0f ? -1f : Mathf.Max(0f, _proceedAt - Time.unscaledTime);

        /// <summary>誰かのカーソルが動いた（席の番号）。</summary>
        public event Action<int> SeatMoved;

        /// <summary>誰かが決定した。</summary>
        public event Action<int> SeatConfirmed;

        /// <summary>誰かが決定を取り消した。</summary>
        public event Action<int> SeatCanceled;

        /// <summary>このPCのプレイヤーが、使われているキャラで決定しようとした。</summary>
        public event Action<int> SeatRejected;

        /// <summary>全員のキャラが決まった。</summary>
        public event Action AllDecided;

        private void Awake()
        {
            _session = GameSession.Active;
            view = view != null ? view : GetComponent<CharacterSelectView>();
            view = view != null ? view : FindFirstObjectByType<CharacterSelectView>();
            for (var i = 0; i < _detectors.Length; i++)
            {
                _detectors[i] = new GripPressDetector();
                _grips[i] = new GripSqueezeTracker();
            }

            if (roster == null || roster.Count == 0)
            {
                Debug.LogWarning("[キャラ選択] キャラの一覧（roster）が空です。メニュー「Tools/キャラ選択/シーンを組み立てる」で作り直すか、一覧にキャラを入れてください。");
            }

            Board.Reset(roster != null ? roster.Count : 0, allowSameCharacter, timeLimitSeconds);
            CharacterSelectionResult.Clear();
        }

        private void OnDisable()
        {
            UnregisterMessages();
        }

        private void OnDestroy()
        {
            foreach (var tracker in _keys)
            {
                tracker.Dispose();
            }

            _keys.Clear();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var now = Time.unscaledTime;
            EnsureMessaging();
            if (IsAuthority)
            {
                _hasState = true;
                TickRules(dt);
            }
            else
            {
                Board.Tick(dt);
            }

            TickInput(dt);
            DetectChanges(now);
            if (IsAuthority)
            {
                TickFinish(now);
            }

            if (IsOnline && IsAuthority && Board.Version != _sentVersion)
            {
                // 1フレームに何度変わっても、配るのは1回だけ。
                _sentVersion = Board.Version;
                CharacterSelectMessages.BroadcastState(NetworkManager.Singleton, Board);
            }
        }

        // ───────── 見た目から使う情報 ─────────

        /// <summary>そのキャラの設定（範囲外は null）。</summary>
        public CharacterDefinition GetCharacter(int index) => roster != null ? roster.Get(index) : null;

        public int CharacterCount => roster != null ? roster.Count : 0;

        /// <summary>そのプレイヤーをこのPCで操作しているか（「あなた」の印・握りのゲージを出す）。</summary>
        public bool IsLocalSeat(int seat)
        {
            if (IsStandalone)
            {
                return seat >= 0 && seat < offlinePlayers;
            }

            return IsOnline && seat == _session.LocalSeat;
        }

        /// <summary>長く握るまでの進み具合（0〜1。このPCのプレイヤーだけ。ほかの人は 0）。</summary>
        public float HoldProgress(int seat)
        {
            if (!IsLocalSeat(seat) || !CharacterSelectBoard.IsValidSeat(seat) || (Board.seats[seat].confirmed && !longPressCancels))
            {
                return 0f;
            }

            return _detectors[seat].Progress;
        }

        /// <summary>そのプレイヤーの名前（ネット対戦ならロビーで決めた名前。1台で試すときは空＝「1P」の札だけ）。</summary>
        public string PlayerName(int seat)
        {
            if (IsOnline && LobbyRoster.IsValid(seat) && _session.Roster.seats[seat].present && !string.IsNullOrEmpty(_session.Roster.seats[seat].name))
            {
                return _session.Roster.seats[seat].name;
            }

            return IsOnline && seat >= 0 && seat < DefaultNames.Length ? DefaultNames[seat] : string.Empty;
        }

        /// <summary>操作の説明（今の遊び方に合わせたキーの名前入り）。</summary>
        public string DescribeControls()
        {
            if (IsStandalone)
            {
                var keys = string.Empty;
                for (var i = 0; i < offlinePlayers && i < offlineKeys.Length; i++)
                {
                    keys += (i > 0 ? "・" : string.Empty) + $"{i + 1}P {offlineKeys[i]}";
                }

                return $"短く握る→カーソル移動　長く握る→READY\n仮のキー：{keys}";
            }

            return $"短く握る→カーソル移動　長く握る→READY\n仮のキー：{key}";
        }

        // ───────── ルール（ホスト・このシーンだけのとき）─────────

        private void TickRules(float dt)
        {
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                Board.SetActive(seat, IsSeatPresent(seat));
            }

            Board.SetAllowDuplicates(allowSameCharacter);
            Board.Tick(dt);
            if (!Board.Finished && (Board.AllConfirmed || Board.TimeUp))
            {
                Board.Finish();
            }
        }

        private bool IsSeatPresent(int seat)
        {
            if (IsStandalone)
            {
                return seat < offlinePlayers;
            }

            return IsOnline && _session.Roster.seats[seat].present;
        }

        private void TickFinish(float now)
        {
            if (_proceeded || _proceedAt < 0f || now < _proceedAt)
            {
                return;
            }

            _proceeded = true;
            if (IsStandalone)
            {
                if (string.IsNullOrEmpty(offlineNextScene))
                {
                    return;
                }

                if (Application.CanStreamedLevelBeLoaded(offlineNextScene))
                {
                    var next = offlineNextScene;
                    if (PasteCurtain.UseForGameStart)
                    {
                        // 歯磨き粉の幕で上から覆ってから切り替え、次のシーンが始まったら流れ落ちる（幕はシーンをまたいで残る）。
                        PasteCurtain.Play(() => SceneManager.LoadScene(next), () => SceneManager.GetSceneByName(next).isLoaded);
                    }
                    else
                    {
                        SceneManager.LoadScene(next);
                    }
                }
                else
                {
                    Debug.LogWarning($"[キャラ選択] シーン「{offlineNextScene}」がビルドの設定に無いので読み込めません。");
                }

                return;
            }

            if (IsOnline && _session.IsHost)
            {
                _session.ProceedToGame();
            }
        }

        // ───────── 入力 ─────────

        private void TickInput(float dt)
        {
            var blocked = !_hasState || Board.Finished || IsMenuOpen();
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                var detector = _detectors[seat];
                if (!IsLocalSeat(seat))
                {
                    detector.Reset();
                    continue;
                }

                // ネット対戦では、各PCのセンサー・キーはそのPCでは常に1人目として読む。
                // 止めている間も読み取りは続ける（溜まった「離した」回数を後で使わないように）。
                var input = IsStandalone ? seat : 0;
                var pressed = SampleInput(input, out var releases);
                if (blocked || !Board.seats[seat].active)
                {
                    detector.Reset();
                    continue;
                }

                // 長押しの途中で、ほかの人が先に同じキャラで READY になって、カーソルが次の空いているキャラへ動かされた。
                // そのまま長押しを続けると、動かされた先のキャラで一瞬で決まってしまうので、この長押しは取り消す（一度離すまで数えない）。
                var cursor = Board.seats[seat].cursor;
                if (detector.IsPressing && cursor != _holdCursor[seat])
                {
                    detector.Reset();
                    _holdCursor[seat] = cursor;
                    Play(onRejected, seat, cursor);
                    SeatRejected?.Invoke(seat);
                    continue;
                }

                var result = detector.Tick(pressed, releases, dt, longPressSeconds);
                if (!detector.IsPressing)
                {
                    // 握っていない間は、今のカーソルを覚えておく（握り始めたキャラ＝長押しで決めるキャラ）。
                    _holdCursor[seat] = cursor;
                }

                switch (result)
                {
                    case GripPressDetector.Result.Short:
                        Request(seat, CharacterSelectAction.Next);
                        break;
                    case GripPressDetector.Result.Long:
                        if (!Board.seats[seat].confirmed)
                        {
                            Request(seat, CharacterSelectAction.Confirm);
                        }
                        else if (longPressCancels)
                        {
                            Request(seat, CharacterSelectAction.Cancel);
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// 1人分の入力を読む（1フレームに1回）。キーは押した・離したを1回ずつ数え、握力センサーは握り直しも「離した」と数える。
        /// 戻り値 = 今押しているか、releases = 前のフレームからの間に離した回数。
        /// </summary>
        private bool SampleInput(int input, out int releases)
        {
            releases = 0;
            var pressed = false;
            var k = IsStandalone ? (input < offlineKeys.Length ? offlineKeys[input] : Key.None) : key;
            var tracker = KeyTracker(k);
            if (tracker != null)
            {
                pressed = tracker.IsPressed;
                releases += tracker.TakeReleases();
            }

            if (useGripSensor && input >= 0 && input < _grips.Length)
            {
                var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
                var value = provider != null ? provider.GetGripValue(input) : 0f;
                if (ignoreKeyboardGrip && MS2026.GripInputBridge.GripInputBridge.Simulator.GetRawValue(input) > 0.001f)
                {
                    // キーボードでの代わり（Q など）が押されている（またはゆるんでいく途中）間は、握力として数えない。
                    value = 0f;
                }
                releases += _grips[input].Tick(value, gripOnThreshold, Mathf.Min(gripOffThreshold, gripOnThreshold), gripReleaseDip);
                pressed |= _grips[input].IsPressed;
            }

            return pressed;
        }

        /// <summary>そのキーの「押した・離した」を数える物（無ければ作る。キーボードが無ければ null）。</summary>
        private KeyPressTracker KeyTracker(Key k)
        {
            if (k == Key.None || Keyboard.current == null)
            {
                return null;
            }

            foreach (var tracker in _keys)
            {
                if (tracker.Key == k)
                {
                    return tracker;
                }
            }

            var created = new KeyPressTracker(k);
            _keys.Add(created);
            return created;
        }

        /// <summary>ロビーの一時メニュー・お知らせが開いている間は操作しない。</summary>
        private bool IsMenuOpen()
        {
            var root = UiRoot.Active;
            return _session != null && root != null && (root.IsOpen(_session.pauseScreen) || root.IsOpen(_session.messageScreen));
        }

        private void Request(int seat, CharacterSelectAction action)
        {
            // 使われているキャラでの決定は、その場で断る（ホストに送らない）。
            if (action == CharacterSelectAction.Confirm && Board.IsLockedFor(Board.seats[seat].cursor, seat))
            {
                Play(onRejected, seat, Board.seats[seat].cursor);
                SeatRejected?.Invoke(seat);
                return;
            }

            if (IsAuthority)
            {
                Apply(seat, action);
            }
            else if (IsOnline)
            {
                CharacterSelectMessages.SendInput(NetworkManager.Singleton, action, Board.seats[seat].cursor);
            }
        }

        private void Apply(int seat, CharacterSelectAction action)
        {
            switch (action)
            {
                case CharacterSelectAction.Next:
                    Board.MoveNext(seat);
                    break;
                case CharacterSelectAction.Confirm:
                    Board.Confirm(seat);
                    break;
                case CharacterSelectAction.Cancel:
                    Board.Cancel(seat);
                    break;
            }
        }

        // ───────── 変化を見て、音・見た目・結果へ（全員のPCで同じように）─────────

        private void DetectChanges(float now)
        {
            if (!_hasState || Board.Version == _seenVersion)
            {
                return;
            }

            var first = _seenVersion < 0;
            _seenVersion = Board.Version;
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                var before = _seen[seat];
                var after = Board.seats[seat];
                _seen[seat] = after;
                if (first || !after.active || !before.active)
                {
                    continue;
                }

                if (after.confirmed && !before.confirmed)
                {
                    Play(onConfirm, seat, after.cursor);
                    SeatConfirmed?.Invoke(seat);
                }
                else if (!after.confirmed && before.confirmed)
                {
                    Play(onCancel, seat, after.cursor);
                    SeatCanceled?.Invoke(seat);
                }
                else if (after.cursor != before.cursor)
                {
                    Play(onMove, seat, after.cursor);
                    SeatMoved?.Invoke(seat);
                }
            }

            if (Board.Finished && !_seenFinished)
            {
                StoreResult();
                Play(onAllDecided, -1, -1);
                AllDecided?.Invoke();
                if (IsAuthority)
                {
                    _proceedAt = now + finishedHoldSeconds;
                }
            }

            _seenFinished = Board.Finished;
        }

        private void StoreResult()
        {
            var chosen = Board.GetChosen();
            var definitions = new CharacterDefinition[chosen.Length];
            for (var seat = 0; seat < chosen.Length; seat++)
            {
                definitions[seat] = GetCharacter(chosen[seat]);
            }

            CharacterSelectionResult.Store(definitions);
        }

        private void Play(FortressEffect effect, int seat, int character)
        {
            var position = view != null && character >= 0 ? view.CharacterPosition(character) : Vector3.zero;
            CharacterSelectEffects.Play(effect, position, seat);
        }

        // ───────── 通信 ─────────

        private void EnsureMessaging()
        {
            var manager = IsOnline ? NetworkManager.Singleton : null;
            if (manager == _registered)
            {
                return;
            }

            UnregisterMessages();
            if (manager == null)
            {
                return;
            }

            CharacterSelectMessages.Register(manager, HandleInput, HandleState);
            _registered = manager;
            if (!manager.IsServer)
            {
                // ホストはこのシーンを先に読み込んで状態を配っているので、取りこぼした分をもらう。
                CharacterSelectMessages.SendInput(manager, CharacterSelectAction.RequestState);
            }
        }

        private void UnregisterMessages()
        {
            if (_registered == null)
            {
                return;
            }

            CharacterSelectMessages.Unregister(_registered);
            _registered = null;
        }

        private void HandleInput(ulong sender, FastBufferReader reader)
        {
            if (!IsOnline || !_session.IsHost)
            {
                return;
            }

            var action = CharacterSelectMessages.ReadInput(reader, out var character);
            if (action == CharacterSelectAction.RequestState)
            {
                CharacterSelectMessages.SendState(NetworkManager.Singleton, sender, Board);
                return;
            }

            var seat = _session.Roster.SeatOf(sender);
            if (seat < 0)
            {
                return;
            }

            // 参加側が長押ししている間に、ほかの人が先にそのキャラで READY になり、ホストではカーソルが動いていた。
            // 動いた先のキャラで決めてしまわないよう断る（参加側には次の状態が届き、カーソルが動いたのが見える）。
            if (action == CharacterSelectAction.Confirm && Board.seats[seat].cursor != character)
            {
                return;
            }

            Apply(seat, action);
        }

        private void HandleState(ulong sender, FastBufferReader reader)
        {
            if (IsAuthority)
            {
                return;
            }

            CharacterSelectMessages.ReadState(reader, Board);
            if (!_hasState && Board.CharacterCount != CharacterCount)
            {
                Debug.LogWarning($"[キャラ選択] ホストのキャラの数（{Board.CharacterCount}）とこのPCの数（{CharacterCount}）が違います。同じビルドを使ってください。");
            }

            _hasState = true;
        }
    }
}
