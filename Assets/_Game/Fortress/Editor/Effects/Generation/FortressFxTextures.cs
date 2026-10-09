using System.IO;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 仮エフェクト用のテクスチャ（白＋透明度）をコードで作る。色はパーティクル側で付ける。
    /// soft=やわらかい光 / bubble=泡 / star=4点のきらめき / digits=0と1（2コマ） / streak=集中線。
    /// </summary>
    public static class FortressFxTextures
    {
        public const int Size = 128;

        public static void SaveAll(string folder)
        {
            Directory.CreateDirectory(folder);
            Save(Path.Combine(folder, "Fx_Soft.png"), Soft());
            Save(Path.Combine(folder, "Fx_Bubble.png"), Bubble());
            Save(Path.Combine(folder, "Fx_Star.png"), Star());
            Save(Path.Combine(folder, "Fx_Digits.png"), Digits());
            Save(Path.Combine(folder, "Fx_Streak.png"), Streak());
        }

        private static void Save(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Texture2D Make(int w, int h, System.Func<float, float, float> alphaAt)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float v = (y + 0.5f) / h * 2f - 1f;
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAt(u, v)));
                }
            }

            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        public static Texture2D Soft() => Make(Size, Size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Pow(1f - Smooth(0f, 1f, r), 1.6f);
        });

        public static Texture2D Bubble() => Make(Size, Size, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float ring = Smooth(0.72f, 0.9f, r) * (1f - Smooth(0.9f, 0.98f, r));
            float fill = (1f - Smooth(0.0f, 0.95f, r)) * 0.12f;
            float hx = u + 0.38f, hy = v - 0.4f; // 左上のハイライト
            float hl = (1f - Smooth(0f, 0.2f, Mathf.Sqrt(hx * hx + hy * hy))) * 0.9f;
            return ring * 0.85f + fill + hl;
        });

        public static Texture2D Star() => Make(Size, Size, (u, v) =>
        {
            float au = Mathf.Abs(u), av = Mathf.Abs(v);
            float f = Mathf.Sqrt(au) + Mathf.Sqrt(av);
            float spike = Mathf.Pow(Mathf.Clamp01(1f - f), 1.5f);
            float core = Mathf.Pow(1f - Smooth(0f, 0.35f, Mathf.Sqrt(u * u + v * v)), 2f);
            return Mathf.Max(spike, core);
        });

        public static Texture2D Streak() => Make(Size, Size, (u, v) =>
        {
            float line = Mathf.Pow(1f - Mathf.Abs(v), 6f);
            float len = 1f - Smooth(0.2f, 1f, Mathf.Abs(u));
            return line * len;
        });

        // 5x7 のドット文字
        private static readonly string[] Zero =
        {
            ".###.",
            "#...#",
            "#..##",
            "#.#.#",
            "##..#",
            "#...#",
            ".###.",
        };

        private static readonly string[] One =
        {
            "..#..",
            ".##..",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
            ".###.",
        };

        /// <summary>2コマ横並び（0と1）。256x128。</summary>
        public static Texture2D Digits()
        {
            int w = Size * 2, h = Size;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color(1f, 1f, 1f, 0f);
            }

            DrawGlyph(px, w, 0, Zero);
            DrawGlyph(px, w, Size, One);
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        private static void DrawGlyph(Color[] px, int width, int xOffset, string[] glyph)
        {
            const int cell = 14; // 5*14=70 × 7*14=98
            int gx = xOffset + (Size - 5 * cell) / 2;
            int gy = (Size - 7 * cell) / 2;
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if (glyph[row][col] != '#')
                    {
                        continue;
                    }

                    int x0 = gx + col * cell, y0 = gy + (6 - row) * cell;
                    for (int y = 0; y < cell - 2; y++)
                    {
                        for (int x = 0; x < cell - 2; x++)
                        {
                            px[(y0 + y) * width + x0 + x] = Color.white;
                        }
                    }
                }
            }
        }
    }
}
