using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の右に縦に3つ並ぶ「ほかの人の欄」の1つ。自分が決定するまでは隠れていて（代わりにスキルの動画が出る）、
    /// 決定すると右から滑り込む。その人が今選んでいるキャラの土台（キャラの色・ミニキャラ入り）・能力の棒・
    /// 「えらび中…」／「READY!」・スキル名を出す。まだ選んでいる人の欄は、カーソルの動きに合わせて中身が変わる。
    /// </summary>
    [AddComponentMenu("キャラ選択/ほかの人の欄 (OtherPlayerRow)")]
    public sealed class OtherPlayerRow : MonoBehaviour
    {
        private static readonly string[] SeatLabels = { "1\nP", "2\nP", "3\nP", "4\nP" };

        [Tooltip("全体の濃さ（滑り込むときと、参加していないときに使う）。")]
        public CanvasGroup group;

        [Tooltip("動く部分（滑り込み・跳ねる。空ならこの物）。")]
        public RectTransform body;

        [Tooltip("土台の絵（その人が選んでいるキャラの「ほかの人の欄の土台」に替わる）。")]
        public Image baseImage;

        [Tooltip("左端の「2P」などの文字（縦書き）。")]
        public Text seatLabel;

        [Tooltip("能力の棒（上から パワー・スピード・ため。名前は土台の絵に描いてあるので棒だけ）。")]
        public StatBar[] stats = System.Array.Empty<StatBar>();

        [Tooltip("「えらび中…」「READY!」の文字。")]
        public Text stateText;

        [Tooltip("スキル名。")]
        public Text skillNameText;

        [Tooltip("決定したときに泡とキラキラを弾けさせる部品。")]
        public CharacterSelectBurst burst;

        [Tooltip("えらび中の文字の色。")]
        public Color selectingColor = new Color(0.36f, 0.35f, 0.5f);

        [Tooltip("参加していない人の欄の濃さ。")]
        [Range(0f, 1f)]
        public float inactiveAlpha = 0.4f;

        /// <summary>この欄に映している席（-1 = まだ無し）。</summary>
        public int Seat { get; private set; } = -1;

        private CharacterDefinition _shown;
        private bool _hasShown;
        private float _pop;
        private float _punch;
        private float _reveal;
        private float _slideDistance;

        /// <summary>状態を表示する（毎フレーム呼ばれる）。reveal = 滑り込みの進み具合（0 = 画面の外、1 = 所定の位置）。</summary>
        public void Show(int seat, bool active, CharacterDefinition character, bool confirmed, float reveal, float slideDistance)
        {
            if (seat != Seat)
            {
                Seat = seat;
                if (seatLabel != null)
                {
                    seatLabel.text = seat >= 0 && seat < SeatLabels.Length ? SeatLabels[seat] : string.Empty;
                }
            }

            var shown = active ? character : null;
            if (!_hasShown || shown != _shown)
            {
                ShowCharacter(shown);
                _shown = shown;
                _hasShown = true;
            }

            _reveal = reveal;
            _slideDistance = slideDistance;
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(reveal * 1.5f) * (active ? 1f : inactiveAlpha);
            }

            if (stateText != null)
            {
                var text = !active ? "参加していません" : confirmed ? "READY!" : "えらび中…";
                if (stateText.text != text)
                {
                    stateText.text = text;
                }

                stateText.color = confirmed ? FortressColors.SeatColor(seat) : selectingColor;
            }
        }

        /// <summary>カーソルを動かしたとき（小さく跳ねる）。</summary>
        public void Punch() => _punch = 1f;

        /// <summary>決定したとき（ぽんと跳ね、泡とキラキラが弾ける）。</summary>
        public void Pop(int seat)
        {
            _pop = 1f;
            if (burst != null && _reveal > 0.5f)
            {
                burst.Play(FortressColors.SeatColor(seat), 16);
            }
        }

        private void ShowCharacter(CharacterDefinition character)
        {
            if (baseImage != null && character != null && character.rowBase != null)
            {
                baseImage.sprite = character.rowBase;
            }

            if (skillNameText != null)
            {
                skillNameText.text = character != null ? character.skillName : string.Empty;
            }

            for (var i = 0; i < stats.Length; i++)
            {
                if (stats[i] == null)
                {
                    continue;
                }

                var has = character != null && character.stats != null && i < character.stats.Length;
                stats[i].Set(string.Empty, has ? character.stats[i].value : 0f);
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _pop = Mathf.Max(0f, _pop - dt * 2.4f);
            _punch = Mathf.Max(0f, _punch - dt * 4f);
            var target = body != null ? body : (RectTransform)transform;
            var scale = 1f + Mathf.Sin(_pop * Mathf.PI) * 0.06f + Mathf.Sin(_punch * Mathf.PI) * 0.02f;
            target.localScale = Vector3.one * scale;
            target.anchoredPosition = new Vector2((1f - CharacterSelectEase.BackOut(Mathf.Clamp01(_reveal))) * _slideDistance, 0f);
        }
    }
}
