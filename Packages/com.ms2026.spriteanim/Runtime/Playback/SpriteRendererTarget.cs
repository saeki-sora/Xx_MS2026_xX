using UnityEngine;

namespace MS2026.SpriteAnim
{
    /// <summary>2D 空間の SpriteRenderer 向けターゲット実装。</summary>
    public sealed class SpriteRendererTarget : ISpriteAnimationTarget
    {
        private readonly SpriteRenderer _renderer;

        public SpriteRendererTarget(SpriteRenderer renderer) => _renderer = renderer;

        public GameObject GameObject => _renderer.gameObject;
        public Sprite Sprite { get => _renderer.sprite; set => _renderer.sprite = value; }
        public Color Color { get => _renderer.color; set => _renderer.color = value; }
        public bool FlipX { get => _renderer.flipX; set => _renderer.flipX = value; }
        public bool FlipY { get => _renderer.flipY; set => _renderer.flipY = value; }

        public ISpriteAnimationTarget CreateBlendLayer()
        {
            var go = new GameObject(_renderer.gameObject.name + " (Blend Layer)")
            {
                hideFlags = HideFlags.DontSave,
            };
            var t = go.transform;
            t.SetParent(_renderer.transform, false);

            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = _renderer.sprite;
            copy.color = _renderer.color;
            copy.flipX = _renderer.flipX;
            copy.flipY = _renderer.flipY;
            copy.sortingLayerID = _renderer.sortingLayerID;
            copy.sortingOrder = _renderer.sortingOrder - 1;
            copy.drawMode = _renderer.drawMode;
            copy.size = _renderer.size;
            copy.maskInteraction = _renderer.maskInteraction;

            return new SpriteRendererTarget(copy);
        }

        public void DestroyBlendLayer(ISpriteAnimationTarget layer)
        {
            if (layer is SpriteRendererTarget t && t._renderer != null)
                Object.Destroy(t._renderer.gameObject);
        }
    }
}
