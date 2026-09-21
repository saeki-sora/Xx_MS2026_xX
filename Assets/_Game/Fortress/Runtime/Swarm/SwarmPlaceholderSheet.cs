using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 本物のスプライトシートが無い敵のための、歩行アニメ付きの仮絵を自動生成する。
    /// 体（円）＋向きを示す白い目印＋交互に動く足。横=コマ、縦=向き（上から 右, 右上, 上, ...）の並び。
    /// </summary>
    public static class SwarmPlaceholderSheet
    {
        private const int CellPixels = 32;

        public static Texture2D Create(Color bodyColor, int frames, int directions)
        {
            frames = Mathf.Max(1, frames);
            directions = Mathf.Max(1, directions);

            var width = frames * CellPixels;
            var height = directions * CellPixels;
            var pixels = new Color32[width * height];

            for (var d = 0; d < directions; d++)
            {
                var angle = directions == 1 ? -Mathf.PI * 0.5f : d * Mathf.PI * 2f / directions;
                var originY = (directions - 1 - d) * CellPixels;

                for (var f = 0; f < frames; f++)
                {
                    DrawCell(pixels, width, f * CellPixels, originY, bodyColor, angle, f / (float)frames);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Fortress_SwarmPlaceholderSheet",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawCell(
            Color32[] pixels, int stride, int originX, int originY, Color body, float angle, float phase)
        {
            var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-forward.y, forward.x);
            var swing = Mathf.Sin(phase * Mathf.PI * 2f);
            var bob = Mathf.Abs(swing) * 1.5f;
            var center = new Vector2(CellPixels * 0.5f, CellPixels * 0.5f + bob);

            var dark = new Color(body.r * 0.55f, body.g * 0.55f, body.b * 0.55f, 1f);

            // 足（体の左右で前後に交互に動く）
            FillCircle(pixels, stride, originX, originY, center - Vector2.up * 2f + side * 6f + forward * (swing * 4f), 3.2f, dark);
            FillCircle(pixels, stride, originX, originY, center - Vector2.up * 2f - side * 6f - forward * (swing * 4f), 3.2f, dark);

            // 体
            FillCircle(pixels, stride, originX, originY, center, 9.5f, dark);
            FillCircle(pixels, stride, originX, originY, center, 8f, body);

            // 向きを示す目印
            FillCircle(pixels, stride, originX, originY, center + forward * 6.5f, 2.6f, Color.white);
        }

        private static void FillCircle(
            Color32[] pixels, int stride, int originX, int originY, Vector2 center, float radius, Color color)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1f));
            var maxX = Mathf.Min(CellPixels - 1, Mathf.CeilToInt(center.x + radius + 1f));
            var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1f));
            var maxY = Mathf.Min(CellPixels - 1, Mathf.CeilToInt(center.y + radius + 1f));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var index = (originY + y) * stride + originX + x;
                    var existing = (Color)pixels[index];
                    var outAlpha = alpha + existing.a * (1f - alpha);
                    var rgb = outAlpha > 0f
                        ? (new Vector3(color.r, color.g, color.b) * alpha
                           + new Vector3(existing.r, existing.g, existing.b) * (existing.a * (1f - alpha))) / outAlpha
                        : Vector3.zero;
                    pixels[index] = new Color(rgb.x, rgb.y, rgb.z, outAlpha);
                }
            }
        }
    }
}
