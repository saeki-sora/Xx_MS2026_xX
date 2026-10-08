using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>文字の部品（標準の Text でも TextMeshPro でも）に文字を入れる小さな橋渡し。</summary>
    public static class UiTextTarget
    {
        /// <summary>同じ GameObject の文字の部品を探す（TextMeshPro を優先）。</summary>
        public static Graphic Find(GameObject go)
        {
            if (go.TryGetComponent<TMP_Text>(out var tmp))
            {
                return tmp;
            }

            return go.TryGetComponent<Text>(out var text) ? text : null;
        }

        public static void Set(Graphic target, string value)
        {
            switch (target)
            {
                case TMP_Text tmp:
                    tmp.text = value;
                    break;
                case Text text:
                    text.text = value;
                    break;
            }
        }

        public static string Get(Graphic target) => target switch
        {
            TMP_Text tmp => tmp.text,
            Text text => text.text,
            _ => string.Empty
        };

        /// <summary>文字が枠に収まっていないか（点検用）。</summary>
        public static bool Overflows(Graphic target)
        {
            var rect = target.rectTransform.rect;
            return target switch
            {
                TMP_Text tmp => tmp.preferredWidth > rect.width + 1f && tmp.textWrappingMode == TextWrappingModes.NoWrap || tmp.preferredHeight > rect.height + 1f,
                Text text => text.horizontalOverflow == HorizontalWrapMode.Wrap
                    ? text.preferredHeight > rect.height + 1f && text.verticalOverflow == VerticalWrapMode.Truncate
                    : text.preferredWidth > rect.width + 1f,
                _ => false
            };
        }
    }
}
