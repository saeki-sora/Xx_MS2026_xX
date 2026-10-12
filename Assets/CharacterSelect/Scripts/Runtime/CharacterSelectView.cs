using MS2026.Fortress;
using MS2026.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラ選択の見た目（2Dの画面）。
    /// ・画面を4つに分けた区画（左上 1P・左下 2P・右上 3P・右下 4P）に、各プレイヤーが選んでいるキャラの立ち絵・名前・能力・スキル（<see cref="PlayerPanel"/>）
    /// ・画面の下に 1P〜4P の丸（<see cref="PlayerCircle"/>）。上の区画と同じく、各プレイヤーが選んでいるキャラ・長押しのゲージ・決定を映す
    /// ・全員が決まったら、画面が光って「全員決定！」が叩きつけられ、4人の色の泡とキラキラが弾ける
    /// ・登場: 区画が左右から滑り込み、下の丸がせり上がる（ロビーから来たときは、ロビーの幕が開いてから）
    /// ・1台で試すとき: ゲームのシーンへ進む前に白くフェードアウト（ロビーから来たときはロビーの幕を使う）
    /// 仮の物（立ち絵・アイコン・背景・札の絵）は全部差し替えられる: キャラはキャラのファイル、画面の部品はシーンの [CharacterSelect UI] の中を直接直す。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("キャラ選択/キャラクター選択の見た目 (CharacterSelectView)")]
    public sealed class CharacterSelectView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("進行役（空なら同じ物・シーンから探す）。")]
        public CharacterSelectController controller;

        [Header("プレイヤーの区画")]
        [Tooltip("P1・P2・P3・P4 の区画（この順。置き場所はシーンで自由に動かしてよい。今は 左上 1P・左下 2P・右上 3P・右下 4P）。")]
        public PlayerPanel[] panels = new PlayerPanel[CharacterSelectBoard.SeatCount];

        [Header("下の丸")]
        [Tooltip("P1・P2・P3・P4 の丸（この順。上の区画と同じ内容を映す）。")]
        public PlayerCircle[] circles = new PlayerCircle[CharacterSelectBoard.SeatCount];

        [Header("文字")]
        [Tooltip("操作の説明（再生すると今のキーの説明に書き換わる。空なら出さない）。")]
        public Text helpText;

        [Tooltip("ルール（同じキャラを選べるか）の文字（空なら出さない）。")]
        public Text ruleText;

        [Tooltip("残り時間の文字（制限時間があるときだけ出る）。")]
        public Text timerText;

        [Tooltip("全員が決まったときに出す見出し（最初は透明）。")]
        public CanvasGroup decidedBanner;

        [Tooltip("見出しの文字の色ずれノイズ（空でもよい）。")]
        public CharacterSelectGlitch bannerGlitch;

        [Header("登場の動き")]
        [Tooltip("下の丸と帯（せり上がる部分）。空なら動かさない。")]
        public RectTransform bottomArea;

        [Tooltip("画面が出てから動き始めるまでの秒数。")]
        [Min(0f)]
        public float entranceDelay = 0.1f;

        [Tooltip("1つの区画が滑り込む秒数。")]
        [Min(0.01f)]
        public float entranceSeconds = 0.5f;

        [Tooltip("区画どうしの出る時間のずれ（秒）。")]
        [Min(0f)]
        public float entranceStagger = 0.08f;

        [Tooltip("区画が滑り込んでくる距離（ピクセル。1P・2P は左から、3P・4P は右から）。")]
        public float entranceDistance = 1000f;

        [Header("全員決定・フェード")]
        [Tooltip("全員決定のときに画面いっぱいに光る板（ふだんは透明）。")]
        public Graphic screenFlash;

        [Tooltip("全員決定のときに真ん中から泡とキラキラを弾けさせる部品。")]
        public CharacterSelectBurst centerBurst;

        [Tooltip("1台で試すとき、ゲームへ進む前に画面を覆う板（最初は透明）。")]
        public CanvasGroup fadeOut;

        [Tooltip("フェードアウトにかける秒数（「GO!」を見せる時間の終わりの方で覆う）。")]
        [Min(0.01f)]
        public float fadeOutSeconds = 0.4f;

        private float _banner;
        private float _bannerSlam = 1f;
        private bool _bannerShown;
        private float _screenFlash;
        private int _shownSeconds = -1;
        private Vector2[] _panelBase = System.Array.Empty<Vector2>();
        private Vector2 _bottomBase;
        private float _entranceStart = -1f;

        private void Awake()
        {
            controller = controller != null ? controller : GetComponent<CharacterSelectController>();
            controller = controller != null ? controller : FindFirstObjectByType<CharacterSelectController>();
            if (decidedBanner != null)
            {
                decidedBanner.alpha = 0f;
            }

            if (fadeOut != null)
            {
                fadeOut.alpha = 0f;
            }

            // 登場の前は、区画を画面の外、下の丸を下に隠しておく。
            _panelBase = new Vector2[panels.Length];
            for (var i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null)
                {
                    _panelBase[i] = ((RectTransform)panels[i].transform).anchoredPosition;
                }
            }

            if (bottomArea != null)
            {
                _bottomBase = bottomArea.anchoredPosition;
            }

            ApplyEntrance(0f);
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                return;
            }

            controller.SeatMoved += HandleMoved;
            controller.SeatConfirmed += HandleConfirmed;
            controller.SeatCanceled += HandleMoved;
            controller.SeatRejected += HandleRejected;
            controller.AllDecided += HandleAllDecided;
        }

        private void OnDisable()
        {
            if (controller == null)
            {
                return;
            }

            controller.SeatMoved -= HandleMoved;
            controller.SeatConfirmed -= HandleConfirmed;
            controller.SeatCanceled -= HandleMoved;
            controller.SeatRejected -= HandleRejected;
            controller.AllDecided -= HandleAllDecided;
        }

        private void Start()
        {
            if (helpText != null && controller != null)
            {
                helpText.text = controller.DescribeControls();
            }

            // このシーンだけで再生したときも、歯磨き粉の幕が流れ落ちるところから始める（幕の見た目を試せる）。
            if (controller != null && controller.IsStandalone)
            {
                PasteCurtain.RevealForStandalone();
            }
        }

        /// <summary>演出（VFX）を出す位置。画面（UI）の上なので、今は原点を返す（効果音は位置に関係なく鳴る）。</summary>
        public Vector3 CharacterPosition(int character) => Vector3.zero;

        private void LateUpdate()
        {
            if (controller == null)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var board = controller.Board;
            var ready = controller.HasState;
            UpdateEntrance();
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                var s = board.seats[seat];
                var active = ready && s.active;
                var character = active ? controller.GetCharacter(s.cursor) : null;
                var blocked = active && board.IsLockedFor(s.cursor, seat);
                var hold = controller.HoldProgress(seat);

                var panel = Panel(seat);
                if (panel != null)
                {
                    // 「あなた」の印はネット対戦のときだけ（1台で試すときは4人とも自分なので出さない）。
                    var isLocal = controller.IsOnline && controller.IsLocalSeat(seat);
                    panel.Show(seat, active, isLocal, controller.PlayerName(seat), character, s.confirmed, blocked, hold);
                }

                // 下の丸も、上の区画と同じ状態を映す。
                Circle(seat)?.Show(seat, active, character, s.confirmed, blocked, hold);
            }

            UpdateTexts(board, dt);
        }

        private void UpdateTexts(CharacterSelectBoard board, float dt)
        {
            if (ruleText != null)
            {
                var rule = board.AllowDuplicates ? "同じキャラ：えらべる" : "同じキャラ：えらべない（早い者勝ち）";
                if (ruleText.text != rule)
                {
                    ruleText.text = rule;
                }
            }

            if (timerText != null)
            {
                timerText.enabled = board.HasTimeLimit;
                var seconds = board.HasTimeLimit ? Mathf.CeilToInt(board.remaining) : -1;
                if (seconds != _shownSeconds)
                {
                    _shownSeconds = seconds;
                    timerText.text = seconds >= 0 ? $"のこり {seconds}" : string.Empty;
                }
            }

            if (decidedBanner != null)
            {
                if (board.Finished && !_bannerShown)
                {
                    _bannerSlam = 0f;
                }

                _bannerShown = board.Finished;
                _banner = Mathf.MoveTowards(_banner, board.Finished ? 1f : 0f, dt * 8f);
                _bannerSlam = Mathf.Min(1f, _bannerSlam + dt / 0.35f);
                decidedBanner.alpha = _banner;
                // 大きいところから叩きつけて、少し跳ね返る。
                decidedBanner.transform.localScale = Vector3.one * Mathf.LerpUnclamped(2.4f, 1f, BackOut(_bannerSlam));
            }

            _screenFlash = Mathf.Max(0f, _screenFlash - dt * 2f);
            if (screenFlash != null)
            {
                screenFlash.color = new Color(1f, 1f, 1f, _screenFlash);
            }

            if (fadeOut != null)
            {
                // 1台で試すときだけ、ゲームのシーンへ進む直前に覆う（ロビーから来たときはロビーの幕が覆う）。
                // 進むシーンが空（どこへも進まない）とき・歯磨き粉の幕で切り替えるときは覆わない。
                var leaving = controller.IsStandalone && !string.IsNullOrEmpty(controller.offlineNextScene) && !PasteCurtain.UseForGameStart;
                var left = leaving ? controller.SecondsUntilProceed : -1f;
                fadeOut.alpha = left < 0f ? 0f : 1f - Mathf.Clamp01(left / fadeOutSeconds);
            }
        }

        // ───────── 登場 ─────────

        private void UpdateEntrance()
        {
            if (_entranceStart < 0f)
            {
                // ロビーから来たときは、ロビーの幕（画面切り替え）が開ききってから始める。
                var root = UiRoot.Active;
                // 歯磨き粉の幕（キャラ選択に入るときの幕）は、流れ落ち始めたら登場を始める。
                var covered = (root != null && root.Overlay != null && root.Overlay.IsPlaying) || PasteCurtain.IsCovering;
                if (covered || !controller.HasState)
                {
                    return;
                }

                _entranceStart = Time.unscaledTime + entranceDelay;
            }

            ApplyEntrance(Time.unscaledTime - _entranceStart);
        }

        private void ApplyEntrance(float elapsed)
        {
            for (var i = 0; i < panels.Length && i < _panelBase.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                var t = Mathf.Clamp01((elapsed - i * entranceStagger) / entranceSeconds);
                var side = i >= 2 ? 1f : -1f;
                ((RectTransform)panels[i].transform).anchoredPosition = _panelBase[i] + new Vector2(side * entranceDistance * (1f - BackOut(t)), 0f);
            }

            if (bottomArea != null)
            {
                var t = Mathf.Clamp01((elapsed - panels.Length * entranceStagger) / entranceSeconds);
                var k = 1f - (1f - t) * (1f - t) * (1f - t);
                bottomArea.anchoredPosition = _bottomBase + new Vector2(0f, -300f * (1f - k));
            }
        }

        /// <summary>少し行き過ぎて戻る動き（0〜1）。</summary>
        private static float BackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ───────── 出来事 ─────────

        private void HandleMoved(int seat)
        {
            Panel(seat)?.SlideIn();
            Circle(seat)?.Punch(seat);
        }

        private void HandleConfirmed(int seat)
        {
            Panel(seat)?.Pop(seat);
            Circle(seat)?.Pop(seat);
        }

        private void HandleAllDecided()
        {
            _screenFlash = 0.8f;
            if (bannerGlitch != null)
            {
                bannerGlitch.Play(1f);
            }

            if (centerBurst != null)
            {
                for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
                {
                    centerBurst.Play(FortressColors.PlayerColor(seat), 12);
                }
            }
        }

        private void HandleRejected(int seat)
        {
            Panel(seat)?.Shake();
            Circle(seat)?.Shake();
        }

        private PlayerPanel Panel(int seat) => panels != null && seat >= 0 && seat < panels.Length ? panels[seat] : null;

        private PlayerCircle Circle(int seat) => circles != null && seat >= 0 && seat < circles.Length ? circles[seat] : null;
    }
}
