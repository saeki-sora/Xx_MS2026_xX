using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Fortress.Hud
{
    /// <summary>
    /// <see cref="LocalTurretGaugeHud"/>の「差し替え用」が未設定のとき、仮のゲージ(無地の四角と文字)を実行時に組み立てる。
    /// 作ったものはシーンに保存されない(実行中だけ存在する)。
    /// </summary>
    internal static class DefaultGaugeBuilder
    {
        private const float LabelWidth = 64f;
        private const float Gap = 6f;

        public static void Build(LocalTurretGaugeHud hud)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("GaugeCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(hud.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var size = hud.defaultGaugeSize;
            var chargeHeight = Mathf.Round(size.y * 0.45f);
            var barsLeft = LabelWidth + 8f;

            var root = CreateRect("Gauge", canvasObject.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(barsLeft + size.x, size.y + Gap + chargeHeight);
            root.anchoredPosition = new Vector2(0f, hud.defaultBottomMargin);

            var label = CreateText("PlayerLabel", root, font, Mathf.RoundToInt(size.y * 1.1f), TextAnchor.MiddleLeft);
            Place(label.rectTransform, 0f, 0f, LabelWidth, root.sizeDelta.y);
            label.fontStyle = FontStyle.Bold;

            var heatFill = CreateBar("Heat", root, font, "HEAT", new Vector2(barsLeft, 0f), size);

            var chargeRoot = CreateRect("Charge", root);
            Place(chargeRoot, barsLeft, size.y + Gap, size.x, chargeHeight);
            var chargeFill = CreateBar("ChargeBar", chargeRoot, font, null, Vector2.zero, new Vector2(size.x, chargeHeight));

            var overheat = CreateText("Overheat", root, font, Mathf.RoundToInt(size.y * 1.2f), TextAnchor.MiddleCenter);
            Place(overheat.rectTransform, barsLeft, root.sizeDelta.y + Gap, size.x, size.y * 1.6f);
            overheat.text = "OVERHEAT";
            overheat.fontStyle = FontStyle.Bold;
            overheat.color = hud.dangerColor;
            overheat.gameObject.SetActive(false);

            hud.root = root.gameObject;
            hud.heatFill = heatFill;
            hud.chargeGaugeRoot = chargeRoot.gameObject;
            hud.chargeFill = chargeFill;
            hud.overheatIndicator = overheat.gameObject;
            hud.playerLabel = label;
            hud.playerColorTargets = new Graphic[] { label };
        }

        // 半透明の背景 + 左端から伸びる中身。中身のImageを返す。
        private static Image CreateBar(string name, RectTransform parent, Font font, string caption, Vector2 position, Vector2 size)
        {
            var background = CreateRect(name, parent);
            Place(background, position.x, position.y, size.x, size.y);
            background.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var fill = CreateRect("Fill", background);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();

            if (!string.IsNullOrEmpty(caption))
            {
                var text = CreateText("Caption", background, font, Mathf.RoundToInt(size.y * 0.6f), TextAnchor.MiddleLeft);
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(8f, 0f);
                text.rectTransform.offsetMax = Vector2.zero;
                text.text = caption;
                text.color = new Color(1f, 1f, 1f, 0.85f);
            }

            return fillImage;
        }

        private static Text CreateText(string name, RectTransform parent, Font font, int fontSize, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
