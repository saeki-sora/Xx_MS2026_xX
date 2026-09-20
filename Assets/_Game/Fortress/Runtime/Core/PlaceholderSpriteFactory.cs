using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 本番アセットが用意されるまでのプレースホルダー図形を生成する。
    /// 生成物はシーン/アセットに保存されない一時オブジェクトとして扱う（HideAndDontSave）。
    /// 本番の見た目に差し替える場合は、各コンポーネントのSpriteRenderer等を置き換えればよい。
    /// </summary>
    public static class PlaceholderSpriteFactory
    {
        private static Sprite _circleSprite;
        private static Sprite _squareSprite;

        public static Sprite CreateCircleSprite()
        {
            if (_circleSprite == null)
            {
                _circleSprite = BuildSprite("Fortress_PlaceholderCircle", true);
            }

            return _circleSprite;
        }

        public static Sprite CreateSquareSprite()
        {
            if (_squareSprite == null)
            {
                _squareSprite = BuildSprite("Fortress_PlaceholderSquare", false);
            }

            return _squareSprite;
        }

        private static Sprite BuildSprite(string spriteName, bool circle)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = 1f;
                    if (circle)
                    {
                        var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        alpha = Mathf.Clamp01(radius - dist + 1f);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = spriteName + "Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
