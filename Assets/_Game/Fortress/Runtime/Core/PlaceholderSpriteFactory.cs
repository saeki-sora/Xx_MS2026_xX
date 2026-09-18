using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 本番アセットが用意されるまでのプレースホルダー図形を生成する。
    /// 各コンポーネントの visualPrefab / sprite フィールドに本番アセットを設定すれば、
    /// このプレースホルダーは使われなくなる（差し替え前提の設計）。
    /// </summary>
    public static class PlaceholderSpriteFactory
    {
        private static Sprite _circleSprite;

        public static Sprite CreateCircleSprite()
        {
            if (_circleSprite != null)
            {
                return _circleSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(radius - dist + 1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderCircleSprite";
            _circleSprite = sprite;
            return _circleSprite;
        }
    }
}
