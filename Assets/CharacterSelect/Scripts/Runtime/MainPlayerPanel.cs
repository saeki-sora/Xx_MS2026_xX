using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の左に大きく出る「自分」の区画（このPCのプレイヤー。1台で試すときは 1P）。
    /// 土台の絵（MAIN_base）の上に、席の番号（「1P」）・今選んでいるキャラの立ち絵・能力の棒・スキルの名前と説明を重ねる。
    /// ・キャラを変える: 立ち絵を歯磨き粉で塗り替えて切り替え、棒がするっと伸び縮みする
    /// ・決定: 「READY!」のはんこが叩きつけられ、白く光って泡とキラキラが弾け、全体がぽんと跳ねる
    /// ・決定できない（ほかの人が決めたキャラ）: 左右に揺れる
    /// 土台の絵は今は赤だけ。席の色・キャラの色ごとの絵が届いたら「土台の色の決め方」と絵の欄に入れる。
    /// </summary>
    [AddComponentMenu("キャラ選択/自分の区画 (MainPlayerPanel)")]
    public sealed class MainPlayerPanel : MonoBehaviour
    {
        private static readonly string[] SeatLabels = { "1P", "2P", "3P", "4P" };

        /// <summary>土台の絵をどれにするか。</summary>
        public enum BaseColorMode
        {
            [InspectorName("席の色（1P 赤・2P 青…）")] Seat,
            [InspectorName("選んでいるキャラの色")] Character
        }

        [Header("全体")]
        [Tooltip("跳ねる・揺れる部分（空ならこの物）。")]
        public RectTransform body;

        [Tooltip("土台の絵（MAIN_base）。")]
        public Image baseImage;

        [Tooltip("土台の色の決め方。絵が無い色のときは、土台に入れてある絵のまま。")]
        public BaseColorMode baseColorMode = BaseColorMode.Seat;

        [Tooltip("席ごとの土台の絵（1P・2P・3P・4P の順）。空の席は土台に入れてある絵（赤）のまま。")]
        public Sprite[] baseBySeat = new Sprite[CharacterSelectBoard.SeatCount];

        [Tooltip("左上の「1P」などの文字。")]
        public Text seatLabel;

        [Header("立ち絵")]
        [Tooltip("立ち絵を動かす枠（ふわふわ・跳ねる）。")]
        public RectTransform portraitRoot;

        [Tooltip("立ち絵（キャラのファイルの「立ち絵」）。")]
        public Image portrait;

        [Tooltip("キャラを切り替えるときの「歯磨き粉で塗り替えて切り替え」（立ち絵の枠に付けた物。空ならすぐ切り替わる）。")]
        public CharacterSwapFx swap;

        [Header("説明")]
        [Tooltip("能力の棒（上から パワー・スピード・ため。名前は土台の絵に描いてあるので棒だけ）。")]
        public StatBar[] stats = System.Array.Empty<StatBar>();

        [Tooltip("スキルの名前。")]
        public Text skillNameText;

        [Tooltip("スキルの説明。")]
        public Text skillText;

        [Header("決定")]
        [Tooltip("決定（READY）したときだけ出す「READY!」のはんこ。")]
        public GameObject decidedStamp;

        [Tooltip("はんこの文字（席の色になる。空でもよい）。")]
        public Text decidedStampText;

        [Tooltip("決定したときなどに白く光る板（ふだんは透明）。")]
        public Graphic flash;

        [Tooltip("決定したときに泡とキラキラを弾けさせる部品（立ち絵のあたりに置く）。")]
        public CharacterSelectBurst burst;

        [Tooltip("はんこを叩きつける時間（秒）。")]
        [Min(0.01f)]
        public float stampSlamSeconds = 0.3f;

        [Tooltip("叩きつける前のはんこの大きさ（倍率）。")]
        public float stampSlamScale = 2.6f;

        [Header("動きの数値")]
        [Tooltip("立ち絵がふわふわ上下する幅（ピクセル）。")]
        public float bobPixels = 8f;

        [Tooltip("ふわふわの速さ（1秒に何往復）。")]
        public float bobSpeed = 0.5f;

        [Tooltip("ほかの人が決めたキャラ（自分は選べない）のときの立ち絵の濃さ。")]
        [Range(0f, 1f)]
        public float blockedAlpha = 0.5f;

        private CharacterDefinition _shown;
        private bool _hasShown;
        private int _shownSeat = -1;
        private Sprite _defaultBase;
        private Vector2 _portraitBase;
        private bool _hasBase;
        private float _pop;
        private float _shake;
        private float _flash;
        private float _stamp = 1f;
        private bool _stampShown;
        private Vector3 _stampScale = Vector3.one;
        private Quaternion _stampRotation = Quaternion.identity;

        private void Awake()
        {
            if (baseImage != null)
            {
                _defaultBase = baseImage.sprite;
            }

            if (portraitRoot != null)
            {
                _portraitBase = portraitRoot.anchoredPosition;
                _hasBase = true;
            }

            if (decidedStamp != null)
            {
                _stampScale = decidedStamp.transform.localScale;
                _stampRotation = decidedStamp.transform.localRotation;
                decidedStamp.SetActive(false);
            }
        }

        /// <summary>状態を表示する（毎フレーム呼ばれる）。</summary>
        public void Show(int seat, CharacterDefinition character, bool confirmed, bool blocked)
        {
            if (seat != _shownSeat)
            {
                _shownSeat = seat;
                SetText(seatLabel, seat >= 0 && seat < SeatLabels.Length ? SeatLabels[seat] : string.Empty);
                if (decidedStampText != null)
                {
                    decidedStampText.color = FortressColors.SeatColor(seat);
                }
            }

            if (!_hasShown || character != _shown)
            {
                ShowCharacter(character);
                _shown = character;
                _hasShown = true;
            }

            ApplyBase(seat, character);

            if (confirmed && !_stampShown)
            {
                // はんこを叩きつける（大きいところから一気に縮んで、少し跳ね返る）。
                _stamp = 0f;
            }

            _stampShown = confirmed;
            SetActive(decidedStamp, confirmed);

            if (portrait != null && character != null)
            {
                var c = character.spriteTint;
                c.a *= blocked && !confirmed ? blockedAlpha : 1f;
                portrait.color = c;
            }
        }

        /// <summary>キャラを変えたとき（少し光る）。</summary>
        public void SlideIn() => _flash = Mathf.Max(_flash, 0.2f);

        /// <summary>決定したとき（ぽんと跳ね、白く光り、泡とキラキラが弾ける）。</summary>
        public void Pop(int seat)
        {
            _pop = 1f;
            _flash = 0.6f;
            if (burst != null)
            {
                burst.Play(FortressColors.SeatColor(seat), 28);
            }
        }

        /// <summary>決定できなかったとき（左右に揺れる）。</summary>
        public void Shake() => _shake = 1f;

        private void ApplyBase(int seat, CharacterDefinition character)
        {
            if (baseImage == null)
            {
                return;
            }

            Sprite sprite = null;
            if (baseColorMode == BaseColorMode.Character)
            {
                sprite = character != null ? character.mainBase : null;
            }
            else if (baseBySeat != null && seat >= 0 && seat < baseBySeat.Length)
            {
                sprite = baseBySeat[seat];
            }

            sprite = sprite != null ? sprite : _defaultBase;
            if (baseImage.sprite != sprite)
            {
                baseImage.sprite = sprite;
            }
        }

        private void ShowCharacter(CharacterDefinition character)
        {
            SetText(skillNameText, character != null ? character.skillName : string.Empty);
            SetText(skillText, character != null ? character.skillDescription : string.Empty);

            var sprite = character != null ? character.sprite : null;
            // キャラが替わるときは、前の立ち絵を残して歯磨き粉で塗り替えて切り替える。
            var scrub = swap != null && portrait != null && _shown != null && character != null && portrait.enabled && sprite != null;
            var oldSprite = portrait != null ? portrait.sprite : null;
            var oldColor = portrait != null ? portrait.color : Color.white;
            var oldScale = portrait != null ? portrait.rectTransform.localScale : Vector3.one;
            var oldPosition = portrait != null ? portrait.rectTransform.anchoredPosition : Vector2.zero;
            if (portrait != null)
            {
                portrait.enabled = sprite != null;
                portrait.sprite = sprite;
                portrait.rectTransform.localScale = Vector3.one * (character != null ? character.spriteScale : 1f);
                portrait.rectTransform.anchoredPosition = character != null ? character.spriteOffset : Vector2.zero;
                if (character != null)
                {
                    portrait.color = character.spriteTint;
                }
            }

            if (scrub)
            {
                swap.Play(oldSprite, oldColor, oldScale, oldPosition);
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
            var time = Time.unscaledTime;
            _pop = Mathf.Max(0f, _pop - dt * 2.2f);
            _shake = Mathf.Max(0f, _shake - dt * 2.5f);
            _flash = Mathf.Max(0f, _flash - dt * 2.5f);
            _stamp = Mathf.Min(1f, _stamp + dt / stampSlamSeconds);
            if (flash != null)
            {
                flash.color = new Color(1f, 1f, 1f, _flash);
            }

            if (decidedStamp != null && decidedStamp.activeSelf)
            {
                var k = CharacterSelectEase.BackOut(_stamp);
                decidedStamp.transform.localScale = _stampScale * Mathf.LerpUnclamped(stampSlamScale, 1f, k);
                decidedStamp.transform.localRotation = _stampRotation * Quaternion.Euler(0f, 0f, (1f - k) * -25f);
            }

            var target = body != null ? body : (RectTransform)transform;
            target.localScale = Vector3.one * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.04f);
            target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 40f) * 1.2f * _shake);

            if (portraitRoot != null && _hasBase)
            {
                var bob = Mathf.Sin(time * bobSpeed * Mathf.PI * 2f) * bobPixels;
                portraitRoot.anchoredPosition = _portraitBase + new Vector2(0f, bob + Mathf.Sin(_pop * Mathf.PI) * 30f);
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
