using DDrive.Runtime.Ui;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 雛形の画面を組み立てる道具（パネル・文字・ボタン・ゲージ）。見た目はそろえた仮の物で、
    /// 絵や文字の部品はあとから自由に差し替えられる（標準の Image / Text / Button を使っている）。
    /// </summary>
    public static class UiTemplateBuilder
    {
        public static readonly Color Ink = new Color(0.93f, 0.94f, 0.95f);
        public static readonly Color SubInk = new Color(0.62f, 0.64f, 0.69f);
        public static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.12f, 0.86f);
        public static readonly Color CardColor = new Color(0.16f, 0.17f, 0.2f, 0.95f);
        public static readonly Color Accent = new Color(1f, 0.48f, 0.24f);

        private static Font _font;
        private static Sprite _rounded;

        public static Font DefaultFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        /// <summary>画面のルート（全画面に広がる、UiScreen 付き）。</summary>
        public static UiScreen Screen(string screenId, string displayName, string layer)
        {
            var go = new GameObject(screenId, typeof(RectTransform), typeof(CanvasGroup), typeof(UiScreen));
            Stretch((RectTransform)go.transform);
            var screen = go.GetComponent<UiScreen>();
            screen.screenId = screenId;
            screen.displayName = displayName;
            screen.layer = layer;
            return screen;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        public static Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
        {
            var rect = Rect(parent, name);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string label, Color color, Vector2 size)
        {
            var image = Panel(parent, name, color);
            image.rectTransform.sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f);
            button.colors = colors;
            var text = Text(image.transform, "Label", label, Mathf.RoundToInt(size.y * 0.42f), new Color(0.08f, 0.05f, 0.03f), TextAnchor.MiddleCenter, true);
            Stretch(text.rectTransform);

            // 乗せると少し大きく、押すとぷにっと縮む（D-Drive の定番の動き）。
            var motion = image.gameObject.AddComponent<UiElementMotion>();
            motion.entries.Add(new UiElementMotion.Entry { trigger = UiMotionTrigger.OnHover, motion = UiMotion.FromPreset(UiPreset.PunchScale, 0.25f) });
            motion.entries.Add(new UiElementMotion.Entry { trigger = UiMotionTrigger.OnClick, motion = UiMotion.FromPreset(UiPreset.Jelly, 0.35f) });
            return button;
        }

        /// <summary>ゲージ（背景＋残像＋中身）。中身は値 key で伸び縮みし、colorByValue なら値で色も変わる。</summary>
        public static UiBindFill Gauge(Transform parent, string name, string key, Vector2 size, Color fillColor, bool colorByValue)
        {
            var back = Panel(parent, name, new Color(0f, 0f, 0f, 0.55f));
            back.rectTransform.sizeDelta = size;

            var trail = Panel(back.transform, "Trail", new Color(1f, 1f, 1f, 0.55f));
            FillImage(trail);
            var fill = Panel(back.transform, "Fill", fillColor);
            FillImage(fill);

            var bind = fill.gameObject.AddComponent<UiBindFill>();
            bind.key = key;
            bind.fill = fill;
            bind.trail = trail;
            if (colorByValue)
            {
                var color = fill.gameObject.AddComponent<UiBindColor>();
                color.key = key;
                color.target = fill;
            }

            return bind;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static UiElementMotion AddMotion(GameObject go, UiMotionTrigger trigger, UiPreset preset, float duration = 0f, float delay = 0f, string valueKey = null)
        {
            var motion = go.GetComponent<UiElementMotion>();
            if (motion == null)
            {
                motion = go.AddComponent<UiElementMotion>();
            }

            motion.entries.Add(new UiElementMotion.Entry { trigger = trigger, valueKey = valueKey, motion = UiMotion.FromPreset(preset, duration, delay) });
            return motion;
        }

        private static void FillImage(Image image)
        {
            Stretch(image.rectTransform, 3f);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.sprite = Rounded;
        }
    }
}
