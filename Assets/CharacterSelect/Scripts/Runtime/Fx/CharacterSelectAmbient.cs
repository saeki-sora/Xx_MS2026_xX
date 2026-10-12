using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面全体にずっと流れている飾り（このゲームの世界＝電脳世界と歯磨き粉）:
    /// ・歯磨き粉の泡が、下からゆらゆら昇る
    /// ・2進数（0 と 1）が、うっすら上から降る（ときどき 0 と 1 が入れ替わる）
    /// ・キラキラが、あちこちでまたたく
    /// 最初に決まった数だけ作って使い回す（再生中に物を作らない）。この物の大きさ（RectTransform）の中に出る。
    /// </summary>
    [AddComponentMenu("キャラ選択/演出/漂う泡と2進数 (CharacterSelectAmbient)")]
    public sealed class CharacterSelectAmbient : MonoBehaviour
    {
        [Header("泡")]
        [Tooltip("泡の絵。")]
        public Sprite bubble;

        [Tooltip("泡の数。")]
        [Range(0, 80)]
        public int bubbleCount = 26;

        [Tooltip("泡の大きさ（ピクセル。この範囲でばらつく）。")]
        public Vector2 bubbleSize = new Vector2(14f, 46f);

        [Tooltip("泡の昇る速さ（ピクセル/秒）。")]
        public Vector2 bubbleSpeed = new Vector2(30f, 90f);

        [Tooltip("泡の色（濃さ＝透けぐあい）。")]
        public Color bubbleColor = new Color(1f, 1f, 1f, 0.55f);

        [Header("2進数")]
        [Tooltip("0・1 の文字に使うフォント（空なら出さない）。")]
        public Font font;

        [Tooltip("文字の数。")]
        [Range(0, 120)]
        public int digitCount = 40;

        [Tooltip("文字の大きさ（ピクセル）。")]
        public Vector2 digitSize = new Vector2(18f, 34f);

        [Tooltip("文字の降る速さ（ピクセル/秒）。")]
        public Vector2 digitSpeed = new Vector2(20f, 60f);

        [Tooltip("文字の色（濃さ＝うっすらぐあい）。")]
        public Color digitColor = new Color(0.25f, 0.75f, 1f, 0.22f);

        [Header("キラキラ")]
        [Tooltip("キラキラの絵（空なら出さない）。")]
        public Sprite sparkle;

        [Tooltip("キラキラの数。")]
        [Range(0, 40)]
        public int sparkleCount = 12;

        [Tooltip("キラキラの大きさ（ピクセル）。")]
        public Vector2 sparkleSize = new Vector2(16f, 40f);

        private struct Item
        {
            public RectTransform rect;
            public Graphic graphic;
            public Text text;
            public Vector2 position;
            public float speed;
            public float size;
            public float phase;
            public float timer;
        }

        private Item[] _bubbles = System.Array.Empty<Item>();
        private Item[] _digits = System.Array.Empty<Item>();
        private Item[] _sparkles = System.Array.Empty<Item>();
        private RectTransform _area;

        private void Awake()
        {
            _area = (RectTransform)transform;
            if (bubble != null)
            {
                _bubbles = MakeImages("Bubble", bubble, bubbleCount);
            }

            if (font != null)
            {
                _digits = new Item[digitCount];
                for (var i = 0; i < digitCount; i++)
                {
                    var go = new GameObject("Digit", typeof(RectTransform), typeof(Text));
                    go.transform.SetParent(transform, false);
                    var text = go.GetComponent<Text>();
                    text.font = font;
                    text.raycastTarget = false;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.fontStyle = FontStyle.Bold;
                    text.text = Random.value < 0.5f ? "0" : "1";
                    _digits[i] = new Item { rect = (RectTransform)go.transform, graphic = text, text = text };
                }
            }

            if (sparkle != null)
            {
                _sparkles = MakeImages("Sparkle", sparkle, sparkleCount);
            }
        }

        private void Start()
        {
            // 最初から画面全体に散らばった状態で始める。
            var r = _area.rect;
            for (var i = 0; i < _bubbles.Length; i++)
            {
                ResetBubble(ref _bubbles[i], Random.Range(r.yMin, r.yMax));
            }

            for (var i = 0; i < _digits.Length; i++)
            {
                ResetDigit(ref _digits[i], Random.Range(r.yMin, r.yMax));
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                ResetSparkle(ref _sparkles[i]);
                _sparkles[i].timer = Random.Range(0f, 2f);
            }
        }

        private Item[] MakeImages(string name, Sprite sprite, int count)
        {
            var items = new Item[count];
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                items[i] = new Item { rect = (RectTransform)go.transform, graphic = image };
            }

            return items;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var time = Time.unscaledTime;
            var r = _area.rect;

            for (var i = 0; i < _bubbles.Length; i++)
            {
                ref var b = ref _bubbles[i];
                b.position.y += b.speed * dt;
                if (b.position.y > r.yMax + b.size)
                {
                    ResetBubble(ref b, r.yMin - b.size);
                }

                // ゆらゆら左右に揺れながら昇る。
                var wobble = Mathf.Sin(time * 1.3f + b.phase) * 12f;
                b.rect.anchoredPosition = new Vector2(b.position.x + wobble, b.position.y);
            }

            for (var i = 0; i < _digits.Length; i++)
            {
                ref var d = ref _digits[i];
                d.position.y -= d.speed * dt;
                d.timer -= dt;
                if (d.timer <= 0f)
                {
                    d.timer = Random.Range(0.3f, 2f);
                    d.text.text = d.text.text == "0" ? "1" : "0";
                }

                if (d.position.y < r.yMin - d.size)
                {
                    ResetDigit(ref d, r.yMax + d.size);
                }

                d.rect.anchoredPosition = d.position;
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                ref var s = ref _sparkles[i];
                s.timer += dt * s.speed;
                if (s.timer >= 1f)
                {
                    ResetSparkle(ref s);
                }

                // ぱっと大きくなって、くるっと回りながら消える。
                var k = Mathf.Sin(Mathf.Clamp01(s.timer) * Mathf.PI);
                s.rect.sizeDelta = Vector2.one * (s.size * k);
                s.rect.localRotation = Quaternion.Euler(0f, 0f, s.timer * 90f + s.phase);
                var c = Color.white;
                c.a = k * 0.9f;
                s.graphic.color = c;
            }
        }

        private void ResetBubble(ref Item b, float y)
        {
            var r = _area.rect;
            b.size = Random.Range(bubbleSize.x, bubbleSize.y);
            b.speed = Random.Range(bubbleSpeed.x, bubbleSpeed.y) * Mathf.Lerp(1.2f, 0.7f, (b.size - bubbleSize.x) / Mathf.Max(1f, bubbleSize.y - bubbleSize.x));
            b.position = new Vector2(Random.Range(r.xMin, r.xMax), y);
            b.phase = Random.Range(0f, 10f);
            b.rect.sizeDelta = Vector2.one * b.size;
            b.graphic.color = bubbleColor;
        }

        private void ResetDigit(ref Item d, float y)
        {
            var r = _area.rect;
            d.size = Random.Range(digitSize.x, digitSize.y);
            d.speed = Random.Range(digitSpeed.x, digitSpeed.y);
            d.position = new Vector2(Random.Range(r.xMin, r.xMax), y);
            d.timer = Random.Range(0.3f, 2f);
            d.text.fontSize = Mathf.RoundToInt(d.size);
            d.rect.sizeDelta = Vector2.one * d.size;
            var c = digitColor;
            c.a *= Random.Range(0.5f, 1f);
            d.graphic.color = c;
        }

        private void ResetSparkle(ref Item s)
        {
            var r = _area.rect;
            s.position = new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax));
            s.size = Random.Range(sparkleSize.x, sparkleSize.y);
            s.speed = Random.Range(0.6f, 1.2f);
            s.phase = Random.Range(0f, 90f);
            s.timer = 0f;
            s.rect.anchoredPosition = s.position;
            s.rect.sizeDelta = Vector2.zero;
        }
    }
}
