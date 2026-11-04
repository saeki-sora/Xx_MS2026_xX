using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 環境の粒の絵（横に4コマ: 「0」「1」の文字・泡の輪・湯気のぼかし）を、画像ファイルを使わずにその場で作る。
    /// 白い絵（アルファだけが形）なので、色は描くときに付ける。
    /// </summary>
    public static class AmbientMoteAtlas
    {
        public const int Tile = 64;
        public const int TileCount = 4;
        public const int GlyphZero = 0;
        public const int GlyphOne = 1;
        public const int Bubble = 2;
        public const int Steam = 3;

        // 5×7 の点で描いた文字（上の行から）。
        private static readonly string[] Zero =
        {
            ".###.",
            "#...#",
            "#..##",
            "#.#.#",
            "##..#",
            "#...#",
            ".###."
        };

        private static readonly string[] One =
        {
            "..#..",
            ".##..",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
            ".###."
        };

        /// <summary>コマ番号 → テクスチャ上の UV の範囲（x=左, y=下, z=幅, w=高さ）。</summary>
        public static Vector4 TileRect(int tile)
        {
            return new Vector4((float)tile / TileCount, 0f, 1f / TileCount, 1f);
        }

        public static Texture2D Build()
        {
            var texture = new Texture2D(Tile * TileCount, Tile, TextureFormat.RGBA32, false, false)
            {
                name = "AmbientMoteAtlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[Tile * TileCount * Tile];
            for (var y = 0; y < Tile; y++)
            {
                for (var x = 0; x < Tile; x++)
                {
                    Put(pixels, GlyphZero, x, y, GlyphAlpha(Zero, x, y));
                    Put(pixels, GlyphOne, x, y, GlyphAlpha(One, x, y));
                    Put(pixels, Bubble, x, y, BubbleAlpha(x, y));
                    Put(pixels, Steam, x, y, SteamAlpha(x, y));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void Put(Color32[] pixels, int tile, int x, int y, float alpha)
        {
            var a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            pixels[y * Tile * TileCount + tile * Tile + x] = new Color32(255, 255, 255, a);
        }

        // 点の文字: 点の四角からの距離でくっきりした形＋うっすらした光のにじみ。
        public static float GlyphAlpha(string[] rows, int px, int py)
        {
            const float cell = 7f;
            var width = rows[0].Length * cell;
            var height = rows.Length * cell;
            var left = (Tile - width) * 0.5f;
            var bottom = (Tile - height) * 0.5f;
            var nearest = float.MaxValue;
            for (var row = 0; row < rows.Length; row++)
            {
                for (var col = 0; col < rows[row].Length; col++)
                {
                    if (rows[row][col] != '#')
                    {
                        continue;
                    }

                    var minX = left + col * cell;
                    var minY = bottom + (rows.Length - 1 - row) * cell;
                    var dx = Mathf.Max(minX - (px + 0.5f), 0f, (px + 0.5f) - (minX + cell));
                    var dy = Mathf.Max(minY - (py + 0.5f), 0f, (py + 0.5f) - (minY + cell));
                    nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }

            return Mathf.Clamp01(1f - nearest / 1.5f) + 0.3f * Mathf.Clamp01(1f - nearest / 7f);
        }

        // 泡: 中が抜けた輪＋左上の小さなつや。
        public static float BubbleAlpha(int px, int py)
        {
            var dx = px + 0.5f - Tile * 0.5f;
            var dy = py + 0.5f - Tile * 0.5f;
            var r = Mathf.Sqrt(dx * dx + dy * dy);
            var ring = Mathf.Clamp01(1f - Mathf.Abs(r - 25f) / 2.5f);
            var inner = 0.08f * Mathf.Clamp01(1f - r / 25f);
            var sx = dx + 10f;
            var sy = dy - 10f;
            var shine = Mathf.Clamp01(1f - Mathf.Sqrt(sx * sx + sy * sy) / 5f);
            return Mathf.Max(ring, shine) + inner;
        }

        // 湯気: 外へ向かってなめらかに消える、ぼんやりした丸。
        public static float SteamAlpha(int px, int py)
        {
            var dx = px + 0.5f - Tile * 0.5f;
            var dy = py + 0.5f - Tile * 0.5f;
            var r = Mathf.Sqrt(dx * dx + dy * dy) / (Tile * 0.5f);
            var a = Mathf.Clamp01(1f - r);
            return a * a * (3f - 2f * a);
        }
    }
}
