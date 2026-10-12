using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の下の丸（1P〜4P のどれか1つ）。上の区画（<see cref="PlayerPanel"/>）と同じく、そのプレイヤーが今選んでいるキャラを映す。
    /// ・えらび中: 丸の中にキャラの顔（仮はキャラの色のキューブ）。縁は紺で、ゆっくり脈打つ。長く握っている間は縁に沿ってゲージが伸びる
    /// ・決定: 縁がプレイヤーの色になって太くなり、「1P」の札にチェックが付き、ぽんと跳ねる
    /// ・参加していない: 全体が薄くなる
    /// 絵・文字・配置はシーンの物を直接直してよい（ここに入れた部品の色・絵・大きさだけを動かす）。
    /// </summary>
    [AddComponentMenu("キャラ選択/下の丸 (PlayerCircle)")]
    public sealed class PlayerCircle : MonoBehaviour
    {
        [Tooltip("全体の濃さ（参加していないとき薄くする）。")]
        public CanvasGroup group;

        [Tooltip("跳ねる・揺れる部分（空ならこの物）。")]
        public RectTransform body;

        [Tooltip("丸の縁（えらび中は「縁の色」、決定するとプレイヤーの色）。")]
        public Image ring;

        [Tooltip("長く握っている間に縁に沿って伸びるゲージ（Image の種類を Filled・Radial 360 にしておく）。")]
        public Image holdGauge;

        [Tooltip("顔の後ろの丸（絵があれば白、無ければキャラの色）。")]
        public Image background;

        [Tooltip("キャラの顔（キャラのファイルの「アイコン」、無ければ「立ち絵」）。")]
        public Image icon;

        [Tooltip("絵が無いキャラのときに出す頭文字。")]
        public Text letter;

        [Tooltip("「1P」などの札（決定するとチェックが付く）。")]
        public PlayerTag label;

        [Tooltip("えらび中の縁の色。")]
        public Color ringColor = new Color(0.1f, 0.3f, 0.45f);

        [Tooltip("決定したときの縁の太さ（大きさの倍率）。")]
        public float decidedRingScale = 1.1f;

        [Tooltip("参加していないときの濃さ。")]
        [Range(0f, 1f)]
        public float inactiveAlpha = 0.35f;

        [Tooltip("決定したときに泡とキラキラを弾けさせる部品（空でもよい）。")]
        public CharacterSelectBurst burst;

        private CharacterDefinition _shown;
        private bool _shownActive = true;
        private bool _confirmed;
        private float _pop;
        private float _punch;
        private float _shake;

        /// <summary>状態を表示する（毎フレーム呼ばれる）。</summary>
        public void Show(int seat, bool active, CharacterDefinition character, bool confirmed, bool blocked, float hold)
        {
            var color = FortressColors.PlayerColor(seat);
            _confirmed = active && confirmed;
            if (group != null)
            {
                group.alpha = active ? 1f : inactiveAlpha;
            }

            var shown = active ? character : null;
            if (shown != _shown || active != _shownActive)
            {
                ShowCharacter(shown);
                _shown = shown;
                _shownActive = active;
            }

            if (ring != null)
            {
                ring.color = _confirmed ? color : ringColor;
            }

            if (holdGauge != null)
            {
                holdGauge.fillAmount = active ? hold : 0f;
                holdGauge.color = confirmed ? Color.white : color;
            }

            if (icon != null && icon.enabled)
            {
                var c = icon.color;
                c.a = blocked && !confirmed ? 0.5f : 1f;
                icon.color = c;
            }

            if (label != null)
            {
                label.Set(seat, color, _confirmed, active && blocked);
            }
        }

        /// <summary>キャラを変えたとき（小さく跳ねる）。</summary>
        public void Punch(int seat)
        {
            _punch = 1f;
            if (burst != null)
            {
                // 小さく泡がはじける。
                burst.Play(FortressColors.PlayerColor(seat), 5);
            }
        }

        /// <summary>決定したとき（大きく跳ね、泡とキラキラが弾ける）。</summary>
        public void Pop(int seat)
        {
            _pop = 1f;
            if (burst != null)
            {
                burst.Play(FortressColors.PlayerColor(seat), 14);
            }
        }

        /// <summary>決定できなかったとき（左右に揺れる）。</summary>
        public void Shake() => _shake = 1f;

        private void ShowCharacter(CharacterDefinition character)
        {
            // 顔は「アイコン」（絵のまま）、無ければ「立ち絵」（立ち絵と同じ色をかける。仮のキューブはこれでキャラの色になる）。
            var face = character != null ? character.IconOrSprite : null;
            var fromSprite = character != null && character.icon == null;
            if (icon != null)
            {
                icon.enabled = face != null;
                icon.sprite = face;
                icon.color = fromSprite ? character.spriteTint : Color.white;
            }

            if (background != null)
            {
                background.color = face != null || character == null ? Color.white : character.themeColor;
            }

            if (letter != null)
            {
                letter.enabled = character != null && face == null;
                var name = character != null ? character.DisplayName : string.Empty;
                letter.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _pop = Mathf.Max(0f, _pop - dt * 2.2f);
            _punch = Mathf.Max(0f, _punch - dt * 5f);
            _shake = Mathf.Max(0f, _shake - dt * 2.5f);

            var target = body != null ? body : (RectTransform)transform;
            var scale = 1f + Mathf.Sin(_pop * Mathf.PI) * 0.22f + Mathf.Sin(_punch * Mathf.PI) * 0.08f;
            target.localScale = Vector3.one * scale;
            target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 40f) * 8f * _shake);

            if (ring != null)
            {
                // えらび中は縁がゆっくり脈打ち、決定すると太くなる。
                var pulse = _confirmed ? decidedRingScale : 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.025f;
                ring.rectTransform.localScale = Vector3.one * pulse;
            }
        }
    }
}
