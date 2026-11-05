using System;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 読み込み中のヒント（ゲームへ進む歯磨き粉の幕が覆っている間に、上に重ねて出すカード）。
    /// 中身は <see cref="HowToPlaySettings"/> の tip〜（歯みがきタイムの説明）。絵がリズムよく「ギュッ」と弾み、にぎにぎする感じを見せる。
    /// 通信はしない（各 PC が自分の幕の上に出す）。
    /// </summary>
    public sealed class LoadingTipView : MonoBehaviour
    {
        private const float FadeSeconds = 0.25f;
        private const float GripBeat = 0.4f;
        private static readonly Vector2 CardSize = new Vector2(1240f, 460f);

        private static LoadingTipView _current;

        private Func<bool> _keepVisible;
        private float _time;
        private float _fade;

        private CanvasGroup _group;
        private RectTransform _card;
        private RectTransform _picture;
        private Text _gripLeft;
        private Text _gripRight;

        /// <summary>ヒントを出しているか。</summary>
        public static bool IsShowing => _current != null;

        /// <summary>ヒントを出す。keepVisible が false になったら消える（null ならずっと。<see cref="Hide"/> で消す）。</summary>
        public static LoadingTipView Show(HowToPlaySettings settings, Func<bool> keepVisible)
        {
            Hide(true);
            if (settings == null)
            {
                return null;
            }

            var go = new GameObject("[HowToPlay] Loading Tip");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<LoadingTipView>();
            view._keepVisible = keepVisible;
            view.BuildUi(settings);
            _current = view;
            FortressEffectPlayer.PlaySound(settings.onTipShow, Vector3.zero);
            return view;
        }

        /// <summary>出しているヒントを消す（immediate なら一瞬で、そうでなければふわっと）。</summary>
        public static void Hide(bool immediate = false)
        {
            if (_current == null)
            {
                return;
            }

            if (immediate)
            {
                Destroy(_current.gameObject);
                _current = null;
                return;
            }

            _current._keepVisible = () => false;
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _time += dt;

            var keep = _keepVisible == null || _keepVisible();
            _fade = keep ? Mathf.Min(1f, _fade + dt / FadeSeconds) : _fade - dt / FadeSeconds;
            if (!keep && _fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _group.alpha = Mathf.Clamp01(_fade);
            var enter = HowToPlayArt.EaseOutBack(Mathf.Clamp01(_time / 0.35f));
            _card.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, enter);

            // にぎにぎ: 一定のリズムで絵が縮んで弾み、「ギュッ」が左右交互に出る。
            var beat = Mathf.FloorToInt(_time / GripBeat);
            var phase = Mathf.Repeat(_time, GripBeat) / GripBeat;
            var squash = Mathf.Exp(-phase * 7f);
            _picture.localScale = new Vector3(1f + 0.08f * squash, 1f - 0.1f * squash, 1f);
            var left = beat % 2 == 0;
            var alpha = Mathf.Clamp01(1f - phase * 1.3f);
            var rise = phase * 24f;
            _gripLeft.color = new Color(1f, 0.9f, 0.45f, left ? alpha : 0f);
            _gripRight.color = new Color(1f, 0.9f, 0.45f, left ? 0f : alpha);
            _gripLeft.rectTransform.anchoredPosition = new Vector2(-540f, 80f + rise);
            _gripRight.rectTransform.anchoredPosition = new Vector2(-280f, 80f + rise);
        }

        private void BuildUi(HowToPlaySettings settings)
        {
            _group = HowToPlayArt.MakeCanvas(gameObject, settings.tipSortingOrder);
            var root = (RectTransform)transform;

            _card = HowToPlayArt.MakeRect("Card", root, Vector2.zero, CardSize);
            HowToPlayArt.MakeImage("Shadow", _card, HowToPlayArt.Rounded, new Color(0f, 0f, 0f, 0.35f), new Vector2(12f, -16f), CardSize);
            HowToPlayArt.MakeImage("Back", _card, HowToPlayArt.Rounded, new Color(0.07f, 0.13f, 0.22f, 1f), Vector2.zero, CardSize);
            HowToPlayArt.MakeImage("Frame", _card, HowToPlayArt.Outline, new Color(0.55f, 0.85f, 1f, 0.9f), Vector2.zero, CardSize);

            var badge = HowToPlayArt.MakeImage("Badge", _card, HowToPlayArt.Rounded, new Color(0.88f, 0.22f, 0.3f), new Vector2(-520f, 175f), new Vector2(150f, 60f));
            HowToPlayArt.MakeText("BadgeText", badge.rectTransform, settings.tipBadge, 34, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(150f, 60f));

            _picture = HowToPlayArt.MakeRect("Picture", _card, new Vector2(-410f, -30f), new Vector2(270f, 270f));
            _picture.pivot = new Vector2(0.5f, 0f);
            _picture.anchoredPosition = new Vector2(-410f, -165f);
            if (settings.tipPicture != null)
            {
                var picture = HowToPlayArt.MakeImage("Image", _picture, settings.tipPicture, Color.white, Vector2.zero, new Vector2(270f, 270f));
                picture.preserveAspect = true;
            }
            else
            {
                HowToPlayArt.MakeImage("Image", _picture, HowToPlayArt.Circle, Color.white, Vector2.zero, new Vector2(230f, 230f));
            }

            _gripLeft = HowToPlayArt.MakeText("GripLeft", _card, settings.tipGripText, 46, Color.clear, TextAnchor.MiddleCenter, new Vector2(-540f, 80f), new Vector2(200f, 70f));
            HowToPlayArt.AddOutline(_gripLeft, HowToPlayArt.Ink, 3f);
            _gripRight = HowToPlayArt.MakeText("GripRight", _card, settings.tipGripText, 46, Color.clear, TextAnchor.MiddleCenter, new Vector2(-280f, 80f), new Vector2(200f, 70f));
            HowToPlayArt.AddOutline(_gripRight, HowToPlayArt.Ink, 3f);

            var title = HowToPlayArt.MakeText("Title", _card, settings.tipTitle, 60, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft, new Vector2(170f, 85f), new Vector2(820f, 90f));
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 36;
            title.resizeTextMaxSize = 60;
            HowToPlayArt.AddOutline(title, HowToPlayArt.Ink, 3f);

            var body = HowToPlayArt.MakeText("Body", _card, settings.tipBody, 40, Color.white, TextAnchor.UpperLeft, new Vector2(170f, -70f), new Vector2(820f, 190f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;
        }
    }
}
