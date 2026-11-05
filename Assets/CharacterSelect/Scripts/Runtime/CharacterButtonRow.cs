using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の下に並ぶキャラのボタン（キャラの一覧の順。1キャラ = 1ボタン）と、その上に付く札・矢印。
    /// ・自分のカーソルが乗っているボタンは「on」の絵（大きく光る）、ほかは「off」の絵
    /// ・1P〜4P の札: その人のカーソルが今いるボタンの左上に付く（同じボタンに何人もいるときは横に並ぶ）。決定した人の札は光る
    /// ・矢印: 自分のボタンの右に付き、「短く握るとこっちへ進む」を示す（自分が決定したら消える）
    /// ・ほかの人が決定したキャラ（自分は選べない）のボタンは暗くなる
    /// </summary>
    [AddComponentMenu("キャラ選択/キャラのボタン (CharacterButtonRow)")]
    public sealed class CharacterButtonRow : MonoBehaviour
    {
        [Tooltip("ボタン（キャラの一覧の順）。")]
        public CharacterButton[] buttons = System.Array.Empty<CharacterButton>();

        [Tooltip("1P・2P・3P・4P の札（この順）。")]
        public RectTransform[] badges = new RectTransform[CharacterSelectBoard.SeatCount];

        [Tooltip("決定した人の札の後ろに光る物（札と同じ順。空でもよい）。")]
        public Graphic[] badgeGlows = new Graphic[CharacterSelectBoard.SeatCount];

        [Tooltip("自分のボタンの右に付く矢印。")]
        public RectTransform arrow;

        [Tooltip("札の位置（ボタンの左上の角からのずれ。ピクセル）。")]
        public Vector2 badgeOffset = new Vector2(34f, -58f);

        [Tooltip("同じボタンに何人もいるときの札の間隔（ピクセル）。")]
        public float badgeSpacing = 52f;

        [Tooltip("矢印の位置（自分のボタンの右の端からのずれ。ピクセル）。")]
        public Vector2 arrowOffset = new Vector2(-32f, 0f);

        [Tooltip("矢印が右に揺れる幅（ピクセル。0 で揺れない）。")]
        public float arrowBobPixels = 8f;

        [Tooltip("札と矢印が次の場所へ移る速さ（大きいほど速い）。")]
        public float followSpeed = 18f;

        [Tooltip("ほかの人が決定したキャラのボタンにかける色。")]
        public Color lockedTint = new Color(0.55f, 0.55f, 0.6f, 1f);

        private readonly bool[] _placed = new bool[CharacterSelectBoard.SeatCount];
        private bool _arrowPlaced;
        private int[] _stack = System.Array.Empty<int>();
        private readonly float[] _badgePop = new float[CharacterSelectBoard.SeatCount];

        /// <summary>状態を表示する（毎フレーム呼ばれる）。</summary>
        public void Show(CharacterSelectController controller, int mainSeat)
        {
            var board = controller.Board;
            var ready = controller.HasState;
            var dt = Time.unscaledDeltaTime;
            var mainCursor = ready && CharacterSelectBoard.IsValidSeat(mainSeat) && board.seats[mainSeat].active ? board.seats[mainSeat].cursor : -1;

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var locked = ready && i != mainCursor && board.ConfirmedOwner(i) >= 0;
                button.Show(controller.GetCharacter(i), i == mainCursor, locked ? lockedTint : Color.white);
            }

            // 札: 同じボタンにいる人を、席の順に横に並べる。
            if (_stack.Length != buttons.Length)
            {
                _stack = new int[buttons.Length];
            }

            System.Array.Clear(_stack, 0, _stack.Length);
            var stack = _stack;
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount && seat < badges.Length; seat++)
            {
                var badge = badges[seat];
                if (badge == null)
                {
                    continue;
                }

                var s = board.seats[seat];
                var visible = ready && s.active && s.cursor >= 0 && s.cursor < buttons.Length && buttons[s.cursor] != null;
                if (badge.gameObject.activeSelf != visible)
                {
                    badge.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    _placed[seat] = false;
                    continue;
                }

                var target = buttons[s.cursor].TopLeft + badgeOffset + new Vector2(stack[s.cursor] * badgeSpacing, 0f);
                stack[s.cursor]++;
                badge.anchoredPosition = _placed[seat] ? Vector2.Lerp(badge.anchoredPosition, target, 1f - Mathf.Exp(-followSpeed * dt)) : target;
                _placed[seat] = true;

                _badgePop[seat] = Mathf.Max(0f, _badgePop[seat] - dt * 3f);
                badge.localScale = Vector3.one * ((s.confirmed ? 1.12f : 1f) + Mathf.Sin(_badgePop[seat] * Mathf.PI) * 0.35f);
                var glow = seat < badgeGlows.Length ? badgeGlows[seat] : null;
                if (glow != null)
                {
                    var c = glow.color;
                    c.a = s.confirmed ? 0.75f + Mathf.Sin(Time.unscaledTime * 6f) * 0.2f : 0f;
                    glow.color = c;
                }
            }

            if (arrow != null)
            {
                var visible = mainCursor >= 0 && mainCursor < buttons.Length && buttons[mainCursor] != null && !board.seats[mainSeat].confirmed && !board.Finished;
                if (arrow.gameObject.activeSelf != visible)
                {
                    arrow.gameObject.SetActive(visible);
                }

                if (visible)
                {
                    // 右へ進むのを促すように、小さく右に揺れる。
                    var bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.2f)) * arrowBobPixels;
                    var target = buttons[mainCursor].RightMiddle + arrowOffset;
                    _arrowPosition = _arrowPlaced ? Vector2.Lerp(_arrowPosition, target, 1f - Mathf.Exp(-followSpeed * dt)) : target;
                    arrow.anchoredPosition = _arrowPosition + new Vector2(bob, 0f);
                    _arrowPlaced = true;
                }
                else
                {
                    _arrowPlaced = false;
                }
            }
        }

        private Vector2 _arrowPosition;

        /// <summary>そのキャラのボタンを跳ねさせる（カーソルが来たとき）。</summary>
        public void Punch(int character)
        {
            if (character >= 0 && character < buttons.Length && buttons[character] != null)
            {
                buttons[character].Punch();
            }
        }

        /// <summary>決定したとき（そのキャラのボタンが跳ね、その人の札が弾む）。</summary>
        public void Pop(int seat, int character)
        {
            if (character >= 0 && character < buttons.Length && buttons[character] != null)
            {
                buttons[character].Pop();
            }

            if (seat >= 0 && seat < _badgePop.Length)
            {
                _badgePop[seat] = 1f;
            }
        }
    }
}
