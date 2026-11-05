using MS2026.Fortress;
using MS2026.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラ選択の見た目（2Dの画面。デザイナーの完成版 2026-11-04）。各PCで「自分」が主役:
    /// ・左に自分の大きい区画（<see cref="MainPlayerPanel"/>: 立ち絵・能力・スキル）
    /// ・右: 自分が決定するまではスキル動画のモニター（<see cref="SkillMonitor"/>）。決定すると扉のように開いて、ほかの3人の欄（<see cref="OtherPlayerRow"/>）が滑り込む
    /// ・下: キャラごとのボタン（<see cref="CharacterButtonRow"/>。誰のカーソルがどこにいるかの札・自分の矢印）と、長く握ると伸びるゲージ（<see cref="PushGauge"/>）
    /// ・上の帯に残り秒数
    /// ・全員が決まったら、画面が光って「GO!」が叩きつけられ、4人の色の泡とキラキラが弾ける
    /// ・登場: 上の帯・左の区画・右・下がそれぞれ画面の外から滑り込む（ロビーから来たときは幕が開いてから）
    /// 絵はシーンの [CharacterSelect UI] の中、キャラごとの絵（ボタン・右の欄の土台・動画）はキャラのファイルで差し替える。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("キャラ選択/キャラクター選択の見た目 (CharacterSelectView)")]
    public sealed class CharacterSelectView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("進行役（空なら同じ物・シーンから探す）。")]
        public CharacterSelectController controller;

        [Tooltip("自分の大きい区画。")]
        public MainPlayerPanel mainPanel;

        [Tooltip("右のほかの人の欄（上から順。自分以外の席を 1P→4P の順に入れる）。")]
        public OtherPlayerRow[] rows = new OtherPlayerRow[CharacterSelectBoard.SeatCount - 1];

        [Tooltip("下のキャラのボタン。")]
        public CharacterButtonRow buttons;

        [Tooltip("右のスキル動画のモニター。")]
        public SkillMonitor monitor;

        [Tooltip("右下の決定のゲージ。")]
        public PushGauge gauge;

        [Header("文字")]
        [Tooltip("上の帯の残り秒数（数字だけ。「残り」「秒」は帯の絵に描いてある）。")]
        public Text timerText;

        [Tooltip("1台で試すときだけ出す、仮のキーの説明（会場では出ない）。")]
        public Text devKeysText;

        [Tooltip("全員が決まったときに出す見出し（最初は透明）。")]
        public CanvasGroup decidedBanner;

        [Tooltip("見出しの文字の色ずれノイズ（空でもよい）。")]
        public CharacterSelectGlitch bannerGlitch;

        [Header("ほかの人の欄の出方")]
        [Tooltip("モニターがどれだけ開いたら欄が出始めるか（0〜1）。")]
        [Range(0f, 1f)]
        public float rowsStartAt = 0.45f;

        [Tooltip("欄が1つ滑り込む秒数。")]
        [Min(0.01f)]
        public float rowSeconds = 0.35f;

        [Tooltip("欄どうしの出る時間のずれ（秒）。")]
        [Min(0f)]
        public float rowStagger = 0.07f;

        [Tooltip("欄が滑り込んでくる距離（ピクセル。右から）。")]
        public float rowSlideDistance = 420f;

        [Header("登場の動き")]
        [Tooltip("上の帯（上から下りてくる）。空なら動かさない。")]
        public RectTransform headerArea;

        [Tooltip("自分の大きい区画（左から）。空なら動かさない。")]
        public RectTransform leftArea;

        [Tooltip("モニターとほかの人の欄（右から）。空なら動かさない。")]
        public RectTransform rightArea;

        [Tooltip("キャラのボタン・ゲージ・下の帯（下から）。空なら動かさない。")]
        public RectTransform bottomArea;

        [Tooltip("画面が出てから動き始めるまでの秒数。")]
        [Min(0f)]
        public float entranceDelay = 0.1f;

        [Tooltip("1つの部分が滑り込む秒数。")]
        [Min(0.01f)]
        public float entranceSeconds = 0.5f;

        [Tooltip("部分どうしの出る時間のずれ（秒）。")]
        [Min(0f)]
        public float entranceStagger = 0.08f;

        [Tooltip("左右から滑り込んでくる距離（ピクセル。上下は帯・下の部分の高さに合わせて自動）。")]
        public float entranceDistance = 1100f;

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
        private int _shownSeconds = int.MinValue;
        private float _rowsTime;
        private float _entranceStart = -1f;
        private readonly int[] _rowSeats = new int[CharacterSelectBoard.SeatCount - 1];
        private Vector2 _headerBase, _leftBase, _rightBase, _bottomBase;

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

            _headerBase = BaseOf(headerArea);
            _leftBase = BaseOf(leftArea);
            _rightBase = BaseOf(rightArea);
            _bottomBase = BaseOf(bottomArea);
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
            if (devKeysText != null && controller != null)
            {
                // 会場（ロビーから来たとき）は握るだけなので出さない。1台で試すときだけ仮のキーを出す。
                devKeysText.gameObject.SetActive(controller.IsStandalone);
                devKeysText.text = controller.DescribeControls();
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
            var main = controller.MainSeat;
            UpdateEntrance();

            var me = board.seats[main];
            var meActive = ready && me.active;
            var myCharacter = meActive ? controller.GetCharacter(me.cursor) : null;
            var myConfirmed = meActive && me.confirmed;
            var blocked = meActive && board.IsLockedFor(me.cursor, main);
            mainPanel?.Show(main, myCharacter, myConfirmed, blocked);

            // 自分が決定したら（全員決まったときも）モニターを開いて、ほかの人の欄を出す。
            var showOthers = myConfirmed || (ready && board.Finished);
            if (monitor != null)
            {
                monitor.Show(myCharacter, showOthers);
            }

            var openness = monitor != null ? monitor.Openness : (showOthers ? 1f : 0f);
            var rowsWanted = showOthers && openness >= rowsStartAt;
            var rowsEnd = rowSeconds + rowStagger * Mathf.Max(0, rows.Length - 1);
            _rowsTime = Mathf.Clamp(_rowsTime + (rowsWanted ? dt : -dt * 3f), 0f, rowsEnd);
            UpdateRows(board, ready, main);

            buttons?.Show(controller, main);
            gauge?.Show(meActive ? controller.HoldProgress(main) : 0f, myConfirmed);

            UpdateTexts(board, dt);
        }

        private void UpdateRows(CharacterSelectBoard board, bool ready, int main)
        {
            var n = 0;
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount && n < _rowSeats.Length; seat++)
            {
                if (seat != main)
                {
                    _rowSeats[n++] = seat;
                }
            }

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || i >= n)
                {
                    continue;
                }

                var seat = _rowSeats[i];
                var s = board.seats[seat];
                var active = ready && s.active;
                var character = active ? controller.GetCharacter(s.cursor) : null;
                var reveal = Mathf.Clamp01((_rowsTime - i * rowStagger) / rowSeconds);
                row.Show(seat, active, character, active && s.confirmed, reveal, rowSlideDistance);
            }
        }

        private void UpdateTexts(CharacterSelectBoard board, float dt)
        {
            if (timerText != null)
            {
                var seconds = board.HasTimeLimit ? Mathf.Max(0, Mathf.CeilToInt(board.remaining)) : -1;
                if (seconds != _shownSeconds)
                {
                    _shownSeconds = seconds;
                    timerText.text = seconds >= 0 ? seconds.ToString("00") : "--";
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
                decidedBanner.transform.localScale = Vector3.one * Mathf.LerpUnclamped(2.4f, 1f, CharacterSelectEase.BackOut(_bannerSlam));
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
            Slide(headerArea, _headerBase, elapsed, 0, new Vector2(0f, HeightOf(headerArea)));
            Slide(leftArea, _leftBase, elapsed, 1, new Vector2(-entranceDistance, 0f));
            Slide(rightArea, _rightBase, elapsed, 2, new Vector2(entranceDistance, 0f));
            Slide(bottomArea, _bottomBase, elapsed, 3, new Vector2(0f, -HeightOf(bottomArea)));
        }

        private void Slide(RectTransform rect, Vector2 basePosition, float elapsed, int order, Vector2 from)
        {
            if (rect == null)
            {
                return;
            }

            var t = Mathf.Clamp01((elapsed - order * entranceStagger) / entranceSeconds);
            var k = from.y != 0f ? CharacterSelectEase.CubicOut(t) : CharacterSelectEase.BackOut(t);
            rect.anchoredPosition = basePosition + from * (1f - k);
        }

        private static Vector2 BaseOf(RectTransform rect) => rect != null ? rect.anchoredPosition : Vector2.zero;

        // 上下から出る部分は、中身の高さ（無ければ 400px）だけ外から。
        private static float HeightOf(RectTransform rect) => rect != null && rect.rect.height > 1f && rect.rect.height < 1000f ? rect.rect.height + 40f : 400f;

        // ───────── 出来事 ─────────

        private void HandleMoved(int seat)
        {
            if (controller == null)
            {
                return;
            }

            if (seat == controller.MainSeat)
            {
                mainPanel?.SlideIn();
                buttons?.Punch(controller.Board.seats[seat].cursor);
            }
            else
            {
                Row(seat)?.Punch();
            }
        }

        private void HandleConfirmed(int seat)
        {
            if (controller == null)
            {
                return;
            }

            if (seat == controller.MainSeat)
            {
                mainPanel?.Pop(seat);
            }
            else
            {
                Row(seat)?.Pop(seat);
            }

            buttons?.Pop(seat, controller.Board.seats[seat].cursor);
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
                    centerBurst.Play(FortressColors.SeatColor(seat), 12);
                }
            }
        }

        private void HandleRejected(int seat)
        {
            if (controller != null && seat == controller.MainSeat)
            {
                mainPanel?.Shake();
            }
        }

        private OtherPlayerRow Row(int seat)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].Seat == seat)
                {
                    return rows[i];
                }
            }

            return null;
        }
    }
}
