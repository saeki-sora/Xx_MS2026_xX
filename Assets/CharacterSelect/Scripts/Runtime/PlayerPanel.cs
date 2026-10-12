using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面を4つに分けた区画の1つ（左上 1P・左下 2P・右上 3P・右下 4P）。そのプレイヤーが今選んでいるキャラの
    /// 立ち絵・名前の札（プレイヤーの色）・能力の棒グラフ・スキルを出し、選択・決定に合わせて変わる。
    /// ・えらび中: 立ち絵がふわふわ。キャラを変えると横からすべり込む。長く握っている間は下のゲージが伸びる（このPCのプレイヤーだけ）
    /// ・決定: 「決定！」のはんこが叩きつけられ、区画が白く光って泡とキラキラが弾け、全体がぽんと跳ねる
    /// ・キャラを変える／決定する: キャラ名に電脳ノイズ（色ずれ）が走る
    /// ・参加していない: 全体が薄くなる
    /// 絵・文字・配置はシーンの物を直接直してよい（ここに入れた部品の色・文字・大きさだけを動かす）。
    /// </summary>
    [AddComponentMenu("キャラ選択/プレイヤーの区画 (PlayerPanel)")]
    public sealed class PlayerPanel : MonoBehaviour
    {
        private static readonly string[] SeatLabels = { "1P", "2P", "3P", "4P" };

        [Header("全体")]
        [Tooltip("全体の濃さ（参加していないとき薄くする）。")]
        public CanvasGroup group;

        [Tooltip("区画全体にうっすらかける色（プレイヤーの色）。")]
        public Image areaTint;

        [Tooltip("区画にかける色の濃さ。")]
        [Range(0f, 1f)]
        public float areaTintAlpha = 0.3f;

        [Tooltip("区画のドット柄（プレイヤーの色を明るくしてかける）。")]
        public Graphic pattern;

        [Tooltip("ドット柄の濃さ。")]
        [Range(0f, 1f)]
        public float patternAlpha = 0.45f;

        [Tooltip("跳ねる・揺れる部分（空ならこの物）。")]
        public RectTransform body;

        [Header("プレイヤー")]
        [Tooltip("「1P」などの文字。")]
        public Text playerLabel;

        [Tooltip("「1P」の後ろの札（プレイヤーの色）。")]
        public Image playerLabelBackground;

        [Tooltip("プレイヤーの名前（ロビーで決めた名前）。")]
        public Text playerNameText;

        [Tooltip("プレイヤーの名前の後ろの帯（名前が無いとき＝1台で試すときは隠す）。")]
        public GameObject playerNameBar;

        [Tooltip("このPCのプレイヤーにだけ出す印（「あなた」。ネット対戦のときだけ）。")]
        public GameObject localMark;

        [Header("立ち絵")]
        [Tooltip("立ち絵を動かす枠（ふわふわ・すべり込み）。")]
        public RectTransform portraitRoot;

        [Tooltip("立ち絵（キャラのファイルの「立ち絵」）。")]
        public Image portrait;

        [Tooltip("立ち絵が無いキャラのときに出す丸（キャラの色）。")]
        public Image portraitFallback;

        [Tooltip("立ち絵が無いキャラのときに出す頭文字。")]
        public Text portraitLetter;

        [Header("説明")]
        [Tooltip("キャラ名の札の背景（プレイヤーの色）。")]
        public Image nameTagBackground;

        [Tooltip("キャラ名。")]
        public Text nameText;

        [Tooltip("説明の板（プレイヤーの色をうすくかける）。")]
        public Image infoPanel;

        [Tooltip("説明の板の濃さ。")]
        [Range(0f, 1f)]
        public float infoPanelAlpha = 0.55f;

        [Tooltip("能力の棒グラフの見本（非表示。キャラの能力の数だけ複製して、同じ親の中に並べる）。")]
        public StatBar statTemplate;

        [Tooltip("スキルの名前。")]
        public Text skillNameText;

        [Tooltip("スキルの説明。")]
        public Text skillText;

        [Header("状態")]
        [Tooltip("長く握っている間に伸びるゲージ（Image の種類を Filled・Horizontal にしておく）。")]
        public Image holdGauge;

        [Tooltip("「えらび中」「READY!」などの文字。")]
        public Text stateText;

        [Tooltip("決定（READY）したときだけ出す「READY!」のはんこ。")]
        public GameObject decidedStamp;

        [Tooltip("はんこの文字（プレイヤーの色になる。空でもよい）。")]
        public Text decidedStampText;

        [Header("演出")]
        [Tooltip("決定したときなどに白く光る板（区画いっぱい。ふだんは透明）。")]
        public Graphic flash;

        [Tooltip("決定したときに泡とキラキラを弾けさせる部品（立ち絵のあたりに置く）。")]
        public CharacterSelectBurst burst;

        [Tooltip("キャラ名に電脳ノイズ（色ずれ）を走らせる部品。")]
        public CharacterSelectGlitch nameGlitch;

        [Tooltip("キャラを切り替えるときの「歯磨き粉で塗り替えて切り替え」（立ち絵の枠に付けた物。空ならすぐ切り替わる）。")]
        public CharacterSwapFx swap;

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

        [Tooltip("キャラを変えたとき、横からすべり込む距離（ピクセル）。")]
        public float slideInPixels = 60f;

        [Tooltip("参加していないときの濃さ。")]
        [Range(0f, 1f)]
        public float inactiveAlpha = 0.35f;

        private readonly List<StatBar> _stats = new List<StatBar>();
        private CharacterDefinition _shown;
        private bool _shownActive = true;
        private Vector2 _portraitBase;
        private bool _hasBase;
        private float _slide;
        private float _pop;
        private float _shake;
        private float _phase;
        private float _flash;
        private float _stamp = 1f;
        private bool _stampShown;
        private Vector3 _stampScale = Vector3.one;
        private Quaternion _stampRotation = Quaternion.identity;

        private void Awake()
        {
            if (statTemplate != null)
            {
                statTemplate.gameObject.SetActive(false);
            }

            if (portraitRoot != null)
            {
                _portraitBase = portraitRoot.anchoredPosition;
                _hasBase = true;
            }

            _phase = transform.GetSiblingIndex() * 0.37f;
            if (decidedStamp != null)
            {
                _stampScale = decidedStamp.transform.localScale;
                _stampRotation = decidedStamp.transform.localRotation;
            }
        }

        /// <summary>状態を表示する（毎フレーム呼ばれる）。</summary>
        public void Show(int seat, bool active, bool isLocal, string playerName, CharacterDefinition character, bool confirmed, bool blocked, float hold)
        {
            var color = FortressColors.PlayerColor(seat);
            if (group != null)
            {
                group.alpha = active ? 1f : inactiveAlpha;
            }

            SetColor(areaTint, WithAlpha(color, areaTintAlpha));
            SetColor(pattern, WithAlpha(Color.Lerp(color, Color.white, 0.45f), patternAlpha));
            SetColor(playerLabelBackground, color);
            SetColor(nameTagBackground, color);
            SetColor(infoPanel, WithAlpha(color, infoPanelAlpha));
            SetText(playerLabel, seat >= 0 && seat < SeatLabels.Length ? SeatLabels[seat] : string.Empty);
            SetText(playerNameText, active ? playerName : string.Empty);
            SetActive(playerNameBar, active && !string.IsNullOrEmpty(playerName));
            SetActive(localMark, active && isLocal);

            var shown = active ? character : null;
            if (shown != _shown || active != _shownActive)
            {
                ShowCharacter(shown);
                _shown = shown;
                _shownActive = active;
            }

            var stamped = active && confirmed;
            if (stamped && !_stampShown)
            {
                // はんこを叩きつける（大きいところから一気に縮んで、少し跳ね返る）。
                _stamp = 0f;
            }

            _stampShown = stamped;
            SetActive(decidedStamp, stamped);
            if (decidedStampText != null)
            {
                decidedStampText.color = color;
            }

            if (holdGauge != null)
            {
                holdGauge.fillAmount = active ? hold : 0f;
                holdGauge.color = confirmed ? Color.white : color;
            }

            if (portrait != null)
            {
                var c = shown != null ? shown.spriteTint : Color.white;
                c.a *= blocked && !confirmed ? 0.5f : 1f;
                portrait.color = c;
            }

            string state;
            if (!active)
            {
                state = "参加していません";
            }
            else if (confirmed)
            {
                state = hold > 0.15f ? "そのまま握ると取り消し…" : "READY!";
            }
            else if (blocked)
            {
                state = "ほかの人が決定済み（短く握って次へ）";
            }
            else
            {
                state = hold > 0.15f ? "そのまま握ると READY…" : "えらび中";
            }

            SetText(stateText, state);
        }

        /// <summary>キャラを変えたとき（横からすべり込み、名前に色ずれが走る）。</summary>
        public void SlideIn()
        {
            _slide = 1f;
            _flash = Mathf.Max(_flash, 0.25f);
            if (nameGlitch != null)
            {
                nameGlitch.Play(0.6f);
            }
        }

        /// <summary>決定したとき（ぽんと跳ね、白く光り、泡とキラキラが弾ける）。</summary>
        public void Pop(int seat)
        {
            _pop = 1f;
            _flash = 0.75f;
            if (nameGlitch != null)
            {
                nameGlitch.Play(1f);
            }

            if (burst != null)
            {
                burst.Play(FortressColors.PlayerColor(seat), 28);
            }
        }

        /// <summary>決定できなかったとき（左右に揺れる）。</summary>
        public void Shake() => _shake = 1f;

        private void ShowCharacter(CharacterDefinition character)
        {
            SetText(nameText, character != null ? character.DisplayName : "―");
            SetText(skillNameText, character != null ? character.skillName : string.Empty);
            SetText(skillText, character != null ? character.skillDescription : string.Empty);

            var sprite = character != null ? character.sprite : null;
            // キャラが替わるときは、前の立ち絵を残して歯磨き粉で塗り替えて切り替える（参加していない⇔参加のときは、すぐ切り替える）。
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
                    // 磨き終わる前の一瞬も、新しいキャラの色で見えるように先に入れておく（毎フレームの色はこの後 Show が入れる）。
                    portrait.color = character.spriteTint;
                }
            }

            if (scrub)
            {
                swap.Play(oldSprite, oldColor, oldScale, oldPosition);
            }

            var showFallback = character != null && sprite == null;
            if (portraitFallback != null)
            {
                portraitFallback.enabled = showFallback;
                if (showFallback)
                {
                    portraitFallback.color = character.themeColor;
                }
            }

            if (portraitLetter != null)
            {
                portraitLetter.enabled = showFallback;
                if (showFallback)
                {
                    var name = character.DisplayName;
                    portraitLetter.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
                }
            }

            ShowStats(character);
        }

        private void ShowStats(CharacterDefinition character)
        {
            if (statTemplate == null)
            {
                return;
            }

            var count = character != null && character.stats != null ? character.stats.Length : 0;
            while (_stats.Count < count)
            {
                var bar = Instantiate(statTemplate, statTemplate.transform.parent, false);
                bar.name = $"Stat {_stats.Count + 1}";
                _stats.Add(bar);
            }

            for (var i = 0; i < _stats.Count; i++)
            {
                var on = i < count;
                SetActive(_stats[i].gameObject, on);
                if (on)
                {
                    _stats[i].Set(character.stats[i].label, character.stats[i].value);
                }
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var time = Time.unscaledTime;
            _slide = Mathf.Max(0f, _slide - dt * 4f);
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
                var k = BackOut(_stamp);
                decidedStamp.transform.localScale = _stampScale * Mathf.LerpUnclamped(stampSlamScale, 1f, k);
                decidedStamp.transform.localRotation = _stampRotation * Quaternion.Euler(0f, 0f, (1f - k) * -25f);
            }

            var target = body != null ? body : (RectTransform)transform;
            target.localScale = Vector3.one * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.06f);
            target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 40f) * 1.5f * _shake);

            if (portraitRoot != null && _hasBase)
            {
                // ふわふわ上下＋キャラを変えたときに横からすべり込む（すべり込みは速く始まって、ゆっくり止まる）。
                var bob = Mathf.Sin((time * bobSpeed + _phase) * Mathf.PI * 2f) * bobPixels;
                var slide = _slide * _slide * slideInPixels;
                portraitRoot.anchoredPosition = _portraitBase + new Vector2(-slide, bob + Mathf.Sin(_pop * Mathf.PI) * 30f);
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

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static void SetText(Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetColor(Graphic graphic, Color color)
        {
            if (graphic != null)
            {
                graphic.color = color;
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
