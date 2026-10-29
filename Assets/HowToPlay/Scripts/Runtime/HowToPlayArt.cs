using System;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// あそびかたの画面で使う部品と絵（すべてコードで作る。シーンにもプレハブにも置かない）。
    /// 作り方はステージ抽選のルーレット（StageRouletteView）と同じ。
    /// </summary>
    internal static class HowToPlayArt
    {
        public static readonly Color Ink = new Color(0.02f, 0.05f, 0.12f, 1f);
        public static readonly Color Navy = new Color(0.05f, 0.09f, 0.17f, 1f);

        /// <summary>画面全体の後ろの色（不透明。ステージ抽選のルーレットと同じ紺）。</summary>
        public static readonly Color Backdrop = new Color(0.02f, 0.04f, 0.12f, 1f);

        private static Font _font;
        private static Texture2D _stripes;
        private static Sprite _rounded;
        private static Sprite _outline;
        private static Sprite _circle;
        private static Sprite _soft;

        public static Font LegacyFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>角丸の四角（9 分割で伸ばす）。</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = MakeRounded(64, 18f, false);

        /// <summary>角丸の枠（中が透明。9 分割で伸ばす）。</summary>
        public static Sprite Outline => _outline != null ? _outline : _outline = MakeRounded(64, 18f, true);

        /// <summary>丸。</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null)
                {
                    return _circle;
                }

                var texture = NewTexture(128, 128, (u, v) =>
                {
                    var r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                    return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - r) * 64f));
                });
                _circle = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
                _circle.hideFlags = HideFlags.HideAndDontSave;
                return _circle;
            }
        }

        /// <summary>ふちがぼやけた丸（光）。</summary>
        public static Sprite Soft
        {
            get
            {
                if (_soft != null)
                {
                    return _soft;
                }

                var texture = NewTexture(128, 128, (u, v) =>
                {
                    var r = new Vector2(u * 2f - 1f, v * 2f - 1f).magnitude;
                    var a = Mathf.Clamp01(1f - r);
                    return new Color(1f, 1f, 1f, a * a);
                });
                _soft = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f);
                _soft.hideFlags = HideFlags.HideAndDontSave;
                return _soft;
            }
        }

        // ───────── 部品 ─────────

        /// <summary>全画面の Overlay の画面（1920×1080 基準）を作る。最初は透明。</summary>
        public static CanvasGroup MakeCanvas(GameObject go, int sortingOrder)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        public static RectTransform MakeRect(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image MakeImage(string objectName, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == Rounded || sprite == Outline ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        public static Text MakeText(string objectName, Transform parent, string text, int size, Color color, TextAnchor anchor, Vector2 position, Vector2 box)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = LegacyFont;
            label.text = text ?? string.Empty;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = box;
            return label;
        }

        public static void AddOutline(Graphic graphic, Color color, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>左端を基準に伸び縮みする棒（anchoredPosition = 左端の位置）。</summary>
        public static void PivotLeft(RectTransform rect)
        {
            rect.pivot = new Vector2(0f, 0.5f);
        }

        public static float EaseOutBack(float t)
        {
            const float S = 1.70158f;
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((S + 1f) * t + S) + 1f;
        }

        // ───────── 絵 ─────────

        /// <summary>斜めのしま（くり返して貼る。ステージ抽選のルーレットと同じ模様）。</summary>
        public static Texture2D Stripes
        {
            get
            {
                if (_stripes != null)
                {
                    return _stripes;
                }

                _stripes = NewTexture(64, 64, (u, v) => new Color(1f, 1f, 1f, Mathf.Repeat(u + v, 0.5f) / 0.5f < 0.45f ? 1f : 0f));
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
                name = "HowToPlay",
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
