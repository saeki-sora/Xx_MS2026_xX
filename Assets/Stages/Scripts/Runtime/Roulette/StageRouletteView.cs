using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Stages
{
    /// <summary>
    /// ステージ抽選のルーレット画面（全員の PC に出る。通信はせず、ホストから届いた「選ばれたステージ」で止まるように各 PC が自分で動かす）。
    /// ステージのカードが横に流れて、だんだんゆっくりになり、少し行き過ぎて戻って真ん中で止まる → 光って「決定！」。
    /// 画面の部品はすべてコードで作る（シーンにもプレハブにも置かない）。キャラ選択の歯磨き粉の幕（描く順番 950）より下に描く。
    /// </summary>
    public sealed class StageRouletteView : MonoBehaviour
    {
        private const int VisibleSide = 3;
        private const float CardWidth = 380f;
        private const float CardHeight = 270f;
        private const float CardSpacing = 430f;
        private const float FadeSeconds = 0.25f;

        private static StageRouletteView _current;
        private static Font _font;
        private static Sprite _rounded;
        private static Sprite _outline;
        private static Sprite _soft;
        private static Texture2D _stripes;

        private readonly List<int> _slots = new List<int>();
        private readonly Card[] _cards = new Card[VisibleSide * 2 + 1];

        private StageCatalog _catalog;
        private StageRouletteSettings _settings;
        private Func<bool> _keepVisible;
        private int _chosenSlot;
        private float _final;
        private float _time;
        private float _fade;
        private int _lastCenter = -1;
        private bool _stopped;

        private CanvasGroup _group;
        private RectTransform _reel;
        private Image _backdrop;
        private RawImage _stripesImage;
        private Text _title;
        private Text _caption;
        private Image _selector;
        private Image _flash;
        private Image _burst;
        private Text _stamp;
        private Text _arrowTop;
        private Text _arrowBottom;

        private sealed class Card
        {
            public RectTransform Root;
            public Image Frame;
            public Image Picture;
            public Text Number;
            public Text Name;
            public Text Subtitle;
            public int ShownSlot = -1;
        }

        /// <summary>ルーレットを出している最中か。</summary>
        public static bool IsShowing => _current != null;

        /// <summary>
        /// ルーレットを出す。chosenIndex = 一覧（<see cref="StageCatalog.stages"/>）の中の選ばれたステージ。
        /// keepVisible が false になったら（ゲームが読み込まれた・中止された）消える。null ならずっと出す（<see cref="Hide"/> で消す）。
        /// </summary>
        public static StageRouletteView Play(StageCatalog catalog, int chosenIndex, Func<bool> keepVisible)
        {
            Hide(true);
            if (catalog == null)
            {
                return null;
            }

            var go = new GameObject("[Stage] Roulette");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<StageRouletteView>();
            view.Begin(catalog, chosenIndex, keepVisible);
            _current = view;
            return view;
        }

        /// <summary>出しているルーレットを消す（immediate なら一瞬で、そうでなければふわっと）。</summary>
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

        private void Begin(StageCatalog catalog, int chosenIndex, Func<bool> keepVisible)
        {
            _catalog = catalog;
            _settings = catalog.roulette ?? new StageRouletteSettings();
            _keepVisible = keepVisible;

            // ルーレットに出すステージの列（選ばれたステージがルーレットに出ない設定でも、列に入れて止める）。
            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog.IsInRoulette(i) || i == chosenIndex)
                {
                    _slots.Add(i);
                }
            }

            if (_slots.Count == 0)
            {
                _slots.Add(Mathf.Max(0, chosenIndex));
            }

            _chosenSlot = Mathf.Max(0, _slots.IndexOf(chosenIndex));
            _final = StageRouletteMath.FinalPosition(_slots.Count, _chosenSlot, _settings.minLaps);
            BuildUi();
            FortressEffectPlayer.PlaySound(_settings.onStart, Vector3.zero);
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
            TickReel();
            TickDecoration(dt);
        }

        // ───────── 動き ─────────

        private void TickReel()
        {
            var spin = _settings.spinSeconds;
            var position = StageRouletteMath.PositionAt(_time, spin, _final, _settings.overshootCards);
            var count = _slots.Count;

            // 真ん中を通るたびにカチッ。
            var center = Mathf.FloorToInt(position + 0.5f);
            if (center != _lastCenter)
            {
                if (_lastCenter >= 0 && !_stopped)
                {
                    FortressEffectPlayer.PlaySound(_settings.onTick, Vector3.zero);
                }

                _lastCenter = center;
            }

            if (!_stopped && _time >= spin)
            {
                _stopped = true;
                OnStopped();
            }

            var frac = position - Mathf.Floor(position);
            for (var i = 0; i < _cards.Length; i++)
            {
                var offset = i - VisibleSide;
                var card = _cards[i];
                var slot = StageRouletteMath.SlotAt(position, offset, count);
                var x = (offset - frac) * CardSpacing;
                card.Root.anchoredPosition = new Vector2(x, 0f);

                // 真ん中に近いほど大きく明るく。止まった後は選ばれたカードがさらに大きく。
                var closeness = 1f - Mathf.Clamp01(Mathf.Abs(x) / CardSpacing);
                var scale = 0.82f + 0.2f * closeness;
                if (_stopped && Mathf.Abs(x) < CardSpacing * 0.5f)
                {
                    var t = Mathf.Clamp01((_time - spin) / 0.35f);
                    scale *= 1f + 0.18f * EaseOutBack(t);
                }

                card.Root.localScale = new Vector3(scale, scale, 1f);
                var dim = Mathf.Lerp(0.45f, 1f, closeness);
                if (_stopped && Mathf.Abs(x) >= CardSpacing * 0.5f)
                {
                    dim *= Mathf.Lerp(1f, 0.5f, Mathf.Clamp01((_time - spin) / 0.4f));
                }

                SetCard(card, slot, dim);
            }
        }

        private void OnStopped()
        {
            FortressEffectPlayer.PlaySound(_settings.onStop, Vector3.zero);
            var stage = _catalog.Get(_slots[_chosenSlot]);
            _caption.text = stage != null ? $"{stage.DisplayName} に決定！" : _settings.decidedText;
            _stamp.text = _settings.decidedText;
        }

        private void TickDecoration(float dt)
        {
            var spin = _settings.spinSeconds;
            var accent = _settings.accent;

            // 見出しは上から降りてくる。
            var drop = EaseOutBack(Mathf.Clamp01(_time / 0.45f));
            _title.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(160f, -70f, drop));
            if (!_stopped)
            {
                var blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_time * 4f));
                _caption.color = new Color(1f, 1f, 1f, blink);
            }
            else
            {
                _caption.color = Color.white;
            }

            // 後ろのしまは流れ続ける（回っている間は速く）。
            if (_stripesImage != null)
            {
                var speed = _stopped ? 0.05f : Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(_time / spin));
                var rect = _stripesImage.uvRect;
                rect.x = Mathf.Repeat(rect.x + speed * dt, 1f);
                _stripesImage.uvRect = rect;
            }

            // 真ん中の枠は脈打つ（止まったら強く光る）。
            var pulse = 0.75f + 0.25f * Mathf.Sin(_time * (_stopped ? 9f : 5f));
            var selectorColor = _stopped ? Color.Lerp(accent, _settings.flashColor, 0.6f) : accent;
            selectorColor.a = pulse;
            _selector.color = selectorColor;
            var selectorScale = 1f + (_stopped ? 0.12f * EaseOutBack(Mathf.Clamp01((_time - spin) / 0.35f)) : 0.02f * Mathf.Sin(_time * 5f));
            _selector.rectTransform.localScale = new Vector3(selectorScale, selectorScale, 1f);
            var arrowBob = Mathf.Sin(_time * (_stopped ? 2f : 8f)) * 8f;
            _arrowTop.rectTransform.anchoredPosition = new Vector2(0f, CardHeight * 0.5f + 70f + arrowBob);
            _arrowBottom.rectTransform.anchoredPosition = new Vector2(0f, -CardHeight * 0.5f - 70f - arrowBob);

            // 止まった瞬間: 画面がパッと光り、輪が広がり、「決定！」が弾んで出る。
            var since = _time - spin;
            if (since >= 0f)
            {
                var flash = Mathf.Clamp01(1f - since / 0.35f);
                var flashColor = _settings.flashColor;
                flashColor.a = flash * 0.55f;
                _flash.color = flashColor;

                var ring = Mathf.Clamp01(since / 0.6f);
                var ringSize = Mathf.Lerp(300f, 1600f, 1f - (1f - ring) * (1f - ring));
                _burst.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
                var burstColor = _settings.flashColor;
                burstColor.a = (1f - ring) * 0.8f;
                _burst.color = burstColor;

                var stamp = Mathf.Clamp01((since - 0.12f) / 0.3f);
                _stamp.enabled = since >= 0.12f;
                var stampScale = Mathf.Lerp(2.4f, 1f, EaseOutBack(stamp));
                _stamp.rectTransform.localScale = new Vector3(stampScale, stampScale, 1f);
                _stamp.color = new Color(1f, 1f, 1f, stamp);
            }
            else
            {
                _flash.color = Color.clear;
                _burst.color = Color.clear;
                _stamp.enabled = false;
            }
        }

        private void SetCard(Card card, int slot, float brightness)
        {
            var index = _slots[slot];
            var stage = _catalog.Get(index);
            if (card.ShownSlot != slot)
            {
                card.ShownSlot = slot;
                card.Name.text = stage != null ? stage.DisplayName : "?";
                card.Subtitle.text = stage != null ? stage.subtitle : string.Empty;
                card.Number.text = (index + 1).ToString();
                var hasPicture = stage != null && stage.thumbnail != null;
                card.Picture.sprite = hasPicture ? stage.thumbnail : RoundedSprite;
                card.Picture.type = hasPicture ? Image.Type.Simple : Image.Type.Sliced;
                card.Picture.preserveAspect = hasPicture;
                card.Number.enabled = !hasPicture;
            }

            var theme = stage != null ? stage.themeColor : Color.gray;
            var hasThumbnail = stage != null && stage.thumbnail != null;
            card.Picture.color = (hasThumbnail ? Color.white : Color.Lerp(theme, Color.black, 0.25f)) * new Color(brightness, brightness, brightness, 1f);
            var frame = Color.Lerp(theme, Color.white, 0.35f) * new Color(brightness, brightness, brightness, 1f);
            frame.a = 1f;
            card.Frame.color = frame;
            card.Name.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, brightness));
            card.Number.color = new Color(1f, 1f, 1f, 0.9f * brightness);
        }

        // ───────── 作る ─────────

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _settings.sortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var root = (RectTransform)transform;
            // 不透明（後ろのキャラ選択の画面は見せない。2026-10-29、ユーザー依頼）。
            _backdrop = MakeImage("Backdrop", root, null, new Color(0.02f, 0.04f, 0.12f, 1f));
            Stretch(_backdrop.rectTransform);

            var stripes = _settings.stripes != null ? _settings.stripes : StripesTexture;
            _stripesImage = new GameObject("Stripes", typeof(RectTransform)).AddComponent<RawImage>();
            _stripesImage.transform.SetParent(root, false);
            _stripesImage.texture = stripes;
            _stripesImage.color = new Color(_settings.accent.r, _settings.accent.g, _settings.accent.b, 0.08f);
            _stripesImage.uvRect = new Rect(0f, 0f, 6f, 3.4f);
            _stripesImage.raycastTarget = false;
            Stretch(_stripesImage.rectTransform);

            // カードが流れる帯。
            var band = MakeImage("Band", root, null, new Color(0f, 0f, 0f, 0.45f));
            band.rectTransform.sizeDelta = new Vector2(2400f, CardHeight + 110f);
            var lineTop = MakeImage("LineTop", band.rectTransform, null, _settings.accent);
            lineTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            lineTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            lineTop.rectTransform.sizeDelta = new Vector2(0f, 4f);
            var lineBottom = MakeImage("LineBottom", band.rectTransform, null, _settings.accent);
            lineBottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            lineBottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            lineBottom.rectTransform.sizeDelta = new Vector2(0f, 4f);

            _burst = MakeImage("Burst", root, SoftSprite, Color.clear);

            _reel = new GameObject("Reel", typeof(RectTransform)).GetComponent<RectTransform>();
            _reel.SetParent(root, false);
            for (var i = 0; i < _cards.Length; i++)
            {
                _cards[i] = MakeCard(i);
            }

            _selector = MakeImage("Selector", root, _settings.cardFrame != null ? _settings.cardFrame : OutlineSprite, _settings.accent);
            _selector.type = _settings.cardFrame != null ? Image.Type.Simple : Image.Type.Sliced;
            _selector.rectTransform.sizeDelta = new Vector2(CardWidth * 1.1f + 26f, CardHeight * 1.1f + 26f);

            _arrowTop = MakeText("ArrowTop", root, "▼", 64, _settings.accent, TextAnchor.MiddleCenter);
            _arrowTop.rectTransform.sizeDelta = new Vector2(120f, 80f);
            _arrowBottom = MakeText("ArrowBottom", root, "▲", 64, _settings.accent, TextAnchor.MiddleCenter);
            _arrowBottom.rectTransform.sizeDelta = new Vector2(120f, 80f);

            _title = MakeText("Title", root, _settings.title, 96, _settings.accent, TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.BoldAndItalic;
            _title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _title.rectTransform.sizeDelta = new Vector2(1800f, 140f);
            AddOutline(_title, new Color(0.02f, 0.05f, 0.18f, 1f), 4f);

            _caption = MakeText("Caption", root, _settings.caption, 46, Color.white, TextAnchor.MiddleCenter);
            _caption.fontStyle = FontStyle.Bold;
            _caption.rectTransform.anchoredPosition = new Vector2(0f, -(CardHeight * 0.5f + 170f));
            _caption.rectTransform.sizeDelta = new Vector2(1600f, 80f);
            AddOutline(_caption, new Color(0.02f, 0.05f, 0.18f, 1f), 3f);

            _stamp = MakeText("Stamp", root, _settings.decidedText, 120, Color.white, TextAnchor.MiddleCenter);
            _stamp.fontStyle = FontStyle.BoldAndItalic;
            _stamp.rectTransform.anchoredPosition = new Vector2(CardWidth * 0.42f, CardHeight * 0.42f);
            _stamp.rectTransform.sizeDelta = new Vector2(520f, 170f);
            _stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            AddOutline(_stamp, new Color(0.85f, 0.15f, 0.35f, 1f), 6f);
            _stamp.enabled = false;

            _flash = MakeImage("Flash", root, null, Color.clear);
            Stretch(_flash.rectTransform);
        }

        private Card MakeCard(int index)
        {
            var card = new Card();
            card.Root = new GameObject("Card" + index, typeof(RectTransform)).GetComponent<RectTransform>();
            card.Root.SetParent(_reel, false);
            card.Root.sizeDelta = new Vector2(CardWidth, CardHeight);

            var shadow = MakeImage("Shadow", card.Root, RoundedSprite, new Color(0f, 0f, 0f, 0.45f));
            shadow.type = Image.Type.Sliced;
            Stretch(shadow.rectTransform, new Vector2(10f, -14f), new Vector2(10f, -14f));

            card.Frame = MakeImage("Frame", card.Root, _settings.cardFrame != null ? _settings.cardFrame : RoundedSprite, Color.white);
            card.Frame.type = _settings.cardFrame != null ? Image.Type.Simple : Image.Type.Sliced;
            Stretch(card.Frame.rectTransform);

            card.Picture = MakeImage("Picture", card.Root, RoundedSprite, Color.gray);
            card.Picture.type = Image.Type.Sliced;
            Stretch(card.Picture.rectTransform, new Vector2(12f, 64f), new Vector2(-12f, -12f));

            card.Number = MakeText("Number", card.Picture.rectTransform, "1", 150, Color.white, TextAnchor.MiddleCenter);
            card.Number.fontStyle = FontStyle.BoldAndItalic;
            Stretch(card.Number.rectTransform);
            AddOutline(card.Number, new Color(0f, 0f, 0f, 0.5f), 3f);

            var plate = MakeImage("NamePlate", card.Root, RoundedSprite, new Color(0.03f, 0.06f, 0.16f, 0.92f));
            plate.type = Image.Type.Sliced;
            plate.rectTransform.anchorMin = new Vector2(0f, 0f);
            plate.rectTransform.anchorMax = new Vector2(1f, 0f);
            plate.rectTransform.pivot = new Vector2(0.5f, 0f);
            plate.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            plate.rectTransform.sizeDelta = new Vector2(-16f, 52f);

            card.Name = MakeText("Name", plate.rectTransform, "", 34, Color.white, TextAnchor.MiddleCenter);
            card.Name.fontStyle = FontStyle.Bold;
            Stretch(card.Name.rectTransform);

            card.Subtitle = MakeText("Subtitle", card.Root, "", 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter);
            card.Subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            card.Subtitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            card.Subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            card.Subtitle.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            card.Subtitle.rectTransform.sizeDelta = new Vector2(0f, 36f);
            return card;
        }

        private static Image MakeImage(string objectName, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string objectName, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = LegacyFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void AddOutline(Text text, Color color, float distance)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static float EaseOutBack(float t)
        {
            const float S = 1.70158f;
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((S + 1f) * t + S) + 1f;
        }

        // ───────── 絵（コードで描く） ─────────

        private static Font LegacyFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>角丸の四角（9 分割で伸ばす）。</summary>
        private static Sprite RoundedSprite => _rounded != null ? _rounded : _rounded = MakeRounded(64, 18f, false);

        /// <summary>角丸の枠（中が透明。9 分割で伸ばす）。</summary>
        private static Sprite OutlineSprite => _outline != null ? _outline : _outline = MakeRounded(64, 18f, true);

        private static Sprite SoftSprite
        {
            get
            {
                if (_soft != null)
                {
                    return _soft;
                }

                var texture = NewTexture(128, 128, (x, y) =>
                {
                    var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                    var ring = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.07f, 2f));
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(ring) * Mathf.Clamp01((1f - r) * 20f));
                });
                _soft = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
                _soft.hideFlags = HideFlags.HideAndDontSave;
                return _soft;
            }
        }

        private static Texture2D StripesTexture
        {
            get
            {
                if (_stripes != null)
                {
                    return _stripes;
                }

                _stripes = NewTexture(64, 64, (x, y) =>
                {
                    var v = Mathf.Repeat(x + y, 0.5f) / 0.5f;
                    return new Color(1f, 1f, 1f, v < 0.45f ? 1f : 0f);
                });
                _stripes.wrapMode = TextureWrapMode.Repeat;
                return _stripes;
            }
        }

        private static Sprite MakeRounded(int size, float radius, bool outlineOnly)
        {
            const float Border = 5f;
            var texture = NewTexture(size, size, (u, v) =>
            {
                var p = new Vector2(u * size, v * size);
                var q = new Vector2(Mathf.Abs(p.x - size * 0.5f), Mathf.Abs(p.y - size * 0.5f)) - new Vector2(size * 0.5f - radius, size * 0.5f - radius);
                var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                var fill = Mathf.Clamp01(0.5f - outside);
                if (outlineOnly)
                {
                    fill *= Mathf.Clamp01(outside + Border + 0.5f);
                }

                return new Color(1f, 1f, 1f, fill);
            });
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height, Func<float, float, Color> paint)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "StageRoulette",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = paint((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
