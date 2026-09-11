using UnityEngine;
using UnityEngine.UI;

namespace MS2026.SpriteAnim
{
    /// <summary>UI (uGUI) の Image 向けターゲット実装。</summary>
    public sealed class ImageTarget : ISpriteAnimationTarget
    {
        private readonly Image _image;

        public ImageTarget(Image image) => _image = image;

        public GameObject GameObject => _image.gameObject;
        public Sprite Sprite { get => _image.sprite; set => _image.sprite = value; }
        public Color Color { get => _image.color; set => _image.color = value; }

        public bool FlipX
        {
            get => _image.rectTransform.localScale.x < 0f;
            set
            {
                var s = _image.rectTransform.localScale;
                s.x = Mathf.Abs(s.x) * (value ? -1f : 1f);
                _image.rectTransform.localScale = s;
            }
        }

        public bool FlipY
        {
            get => _image.rectTransform.localScale.y < 0f;
            set
            {
                var s = _image.rectTransform.localScale;
                s.y = Mathf.Abs(s.y) * (value ? -1f : 1f);
                _image.rectTransform.localScale = s;
            }
        }

        public ISpriteAnimationTarget CreateBlendLayer()
        {
            var srcRect = _image.rectTransform;
            var go = new GameObject(_image.gameObject.name + " (Blend Layer)", typeof(RectTransform))
            {
                hideFlags = HideFlags.DontSave,
            };
            var rt = (RectTransform)go.transform;
            rt.SetParent(srcRect.parent, false);
            rt.SetSiblingIndex(srcRect.GetSiblingIndex());
            rt.anchorMin = srcRect.anchorMin;
            rt.anchorMax = srcRect.anchorMax;
            rt.anchoredPosition = srcRect.anchoredPosition;
            rt.sizeDelta = srcRect.sizeDelta;
            rt.localScale = srcRect.localScale;
            rt.pivot = srcRect.pivot;

            var copy = go.AddComponent<Image>();
            copy.sprite = _image.sprite;
            copy.color = _image.color;
            copy.type = _image.type;
            copy.preserveAspect = _image.preserveAspect;
            copy.raycastTarget = false;

            return new ImageTarget(copy);
        }

        public void DestroyBlendLayer(ISpriteAnimationTarget layer)
        {
            if (layer is ImageTarget t && t._image != null)
                Object.Destroy(t._image.gameObject);
        }
    }
}
