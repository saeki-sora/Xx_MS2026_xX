using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 破壊可能物の見た目1種類分（通常・各ダメージ段階・破壊後）。表示の切り替えと、色の乗算（ダメージで暗く・跡として薄く・被弾で光る）を担当する。
    /// Prefabから作った見た目でも、ルートに付いた仮の四角（PlaceholderVisual）でも同じように扱える。
    /// </summary>
    public sealed class DestructibleVisualNode
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly GameObject _root;
        private readonly Renderer[] _renderers;
        private readonly Color[] _baseColors;
        private readonly int[] _baseSortingOrders;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <param name="root">Prefabから作った見た目のルート。仮の四角のようにGameObject自体は切り替えない場合はnull。</param>
        public DestructibleVisualNode(GameObject root, Renderer[] renderers)
        {
            _root = root;
            _renderers = renderers;
            _baseColors = new Color[renderers.Length];
            _baseSortingOrders = new int[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                _baseColors[i] = ReadColor(renderers[i]);
                _baseSortingOrders[i] = renderers[i].sortingOrder;
            }
        }

        public Transform Transform => _root != null ? _root.transform : null;
        public bool HasRenderers => _renderers.Length > 0;

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
                return;
            }

            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        /// <summary>元の色に対して、色（RGBは乗算・Aは乗算）を掛けて表示する。</summary>
        public void ApplyTint(Color multiplier)
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var color = _baseColors[i] * multiplier;
                if (renderer is SpriteRenderer sprite)
                {
                    sprite.color = color;
                    continue;
                }

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_block);
            }
        }

        public void ApplySortingOffset(int offset)
        {
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].sortingOrder = _baseSortingOrders[i] + offset;
                }
            }
        }

        private static Color ReadColor(Renderer renderer)
        {
            if (renderer is SpriteRenderer sprite)
            {
                return sprite.color;
            }

            var material = renderer.sharedMaterial;
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
        }
    }
}
