using System;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// 見やすくする処理の値（<see cref="SdArtSettings"/> から作る。Unity の型を使わないので、Unity の外でも試せる）。
    /// 大きさの値はピクセル。Size 系（〜Size, Grow, Rim, Sticker〜）は奇数の窓の幅（0 か 1 なら何もしない）。
    /// </summary>
    public struct SdArtParams
    {
        public float LineMaxLum;     // 線とみなす暗さ（明るさがこれ未満）
        public float LineBlueLead;   // 線とみなす青さ（青 − max(赤, 緑) がこれより大きい）
        public int FillSize;         // これより太い濃い青は「塗り」なので太くしない（元の絵のピクセル）
        public int LineGrow;         // 線を太くする幅（元の絵のピクセル）
        public float LineSoft;       // 太くした線のふちのぼかし
        public float Saturation;     // 色の鮮やかさ（1 = そのまま）
        public float Contrast;       // 明暗の差（1 = そのまま）
        public int OutHeight;        // 書き出す絵のキャラ部分の高さ
        public int Rim;              // キャラのまわりの紺のふち（書き出す大きさでのピクセル）
        public int Pad;              // ふち・影のための余白（片側）
        public int StickerWhite;     // 白いふち
        public int StickerLine;      // 白いふちの外の細い紺の線
        public float ShadowBlur;     // 影のぼかし（0 = 影なし）
        public int ShadowGrow;       // 影の広がり
        public float ShadowAlpha;    // 影の濃さ

        public static SdArtParams Default => new SdArtParams
        {
            LineMaxLum = 0.15f, LineBlueLead = 0.2f, FillSize = 9, LineGrow = 5, LineSoft = 0.8f,
            Saturation = 1.15f, Contrast = 1.1f, OutHeight = 512, Rim = 7, Pad = 24,
            StickerWhite = 13, StickerLine = 5, ShadowBlur = 6f, ShadowGrow = 5, ShadowAlpha = 0.7f,
        };
    }

    /// <summary>RGBA の絵（0〜1、ストレートアルファ、1ピクセル4つずつ）。行の向きは問わない（処理は上下対称）。</summary>
    public sealed class SdImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float[] Px;

        public SdImage(int width, int height, float[] px = null)
        {
            Width = width;
            Height = height;
            Px = px ?? new float[width * height * 4];
        }
    }

    /// <summary>
    /// 砲台の SD の絵を、ゲームの小さな表示でも見やすくする処理（2026-10-22〜24。元は Tools/UIArt/make_sd_game_art.py）。
    /// 順番: 紺の線だけ太くする → 鮮やかさ・明暗 → 書き出す大きさへ縮小 → 紺のふち → 余白 → 白いふち → 細い紺の線 → 影。
    /// </summary>
    public static class SdArtProcessor
    {
        private static readonly float[] ShadowColor = { 10f / 255f, 10f / 255f, 40f / 255f };

        public static SdImage Process(SdImage source, SdArtParams p)
        {
            var img = ThickenLines(source, p, out var ink);
            Enhance(img, p.Saturation, p.Contrast);
            var outW = Math.Max(1, (int)Math.Round(img.Width * (double)p.OutHeight / img.Height));
            img = Resize(img, outW, Math.Max(1, p.OutHeight));
            if (p.Rim > 1)
            {
                img = AddBand(img, p.Rim, ink, 0.6f);
            }

            img = Pad(img, Math.Max(0, p.Pad));
            if (p.StickerWhite > 1)
            {
                img = AddBand(img, p.StickerWhite, new[] { 1f, 1f, 1f }, 0.7f);
            }

            if (p.StickerLine > 1)
            {
                img = AddBand(img, p.StickerLine, ink, 0.7f);
            }

            if (p.ShadowBlur > 0f && p.ShadowAlpha > 0f)
            {
                img = AddShadow(img, p.ShadowGrow, p.ShadowBlur, p.ShadowAlpha);
            }

            return img;
        }

        /// <summary>キャラ部分の高さに対する、書き出した絵全体の高さの比（ふち・影の余白の分だけ大きい）。</summary>
        public static float CanvasScale(SdArtParams p) => p.OutHeight <= 0 ? 1f : (p.OutHeight + 2f * Math.Max(0, p.Pad)) / p.OutHeight;

        // 紺の線（暗くて青い、細いもの）だけを太くする。塗り（太い濃い青の部分）はそのまま。ink = 線の平均の色。
        private static SdImage ThickenLines(SdImage src, SdArtParams p, out float[] ink)
        {
            int w = src.Width, h = src.Height, n = w * h;
            var s = src.Px;
            var darkBlue = new float[n];
            for (var i = 0; i < n; i++)
            {
                float r = s[i * 4], g = s[i * 4 + 1], b = s[i * 4 + 2], a = s[i * 4 + 3];
                var lum = 0.299f * r + 0.587f * g + 0.114f * b;
                darkBlue[i] = lum < p.LineMaxLum && b - Math.Max(r, g) > p.LineBlueLead && a > 0.5f ? 1f : 0f;
            }

            var line = darkBlue;
            if (p.FillSize > 1)
            {
                var fills = MaxFilter(MinFilter(darkBlue, w, h, p.FillSize), w, h, p.FillSize + 2);
                line = new float[n];
                for (var i = 0; i < n; i++)
                {
                    line[i] = darkBlue[i] > 0.5f && fills[i] <= 0.5f ? 1f : 0f;
                }
            }

            double sr = 0, sg = 0, sb = 0;
            var count = 0;
            for (var i = 0; i < n; i++)
            {
                if (line[i] < 0.5f) continue;
                sr += s[i * 4]; sg += s[i * 4 + 1]; sb += s[i * 4 + 2]; count++;
            }

            ink = count > 0 ? new[] { (float)(sr / count), (float)(sg / count), (float)(sb / count) } : new[] { 0.12f, 0.05f, 0.6f };

            var grown = line;
            if (p.LineGrow > 1)
            {
                grown = MaxFilter(line, w, h, p.LineGrow);
            }

            if (p.LineSoft > 0f)
            {
                grown = GaussianBlur(grown, w, h, p.LineSoft);
            }

            var outImg = new SdImage(w, h);
            var o = outImg.Px;
            for (var i = 0; i < n; i++)
            {
                var k = grown[i];
                o[i * 4] = s[i * 4] * (1 - k) + ink[0] * k;
                o[i * 4 + 1] = s[i * 4 + 1] * (1 - k) + ink[1] * k;
                o[i * 4 + 2] = s[i * 4 + 2] * (1 - k) + ink[2] * k;
                o[i * 4 + 3] = Math.Max(s[i * 4 + 3], k);
            }

            return outImg;
        }

        // 鮮やかさ: 白黒との間で伸ばす。明暗: 全体の平均の明るさとの間で伸ばす（Pillow の ImageEnhance と同じ考え方）。
        private static void Enhance(SdImage img, float saturation, float contrast)
        {
            var px = img.Px;
            var n = img.Width * img.Height;
            for (var i = 0; i < n; i++)
            {
                var l = Luma(px, i);
                for (var c = 0; c < 3; c++)
                {
                    px[i * 4 + c] = Clamp01(l + (px[i * 4 + c] - l) * saturation);
                }
            }

            double sum = 0;
            for (var i = 0; i < n; i++)
            {
                sum += Luma(px, i);
            }

            var mean = (float)(sum / Math.Max(1, n));
            for (var i = 0; i < n; i++)
            {
                for (var c = 0; c < 3; c++)
                {
                    px[i * 4 + c] = Clamp01(mean + (px[i * 4 + c] - mean) * contrast);
                }
            }
        }

        private static float Luma(float[] px, int i) => 0.299f * px[i * 4] + 0.587f * px[i * 4 + 1] + 0.114f * px[i * 4 + 2];

        // 絵のまわりに color の帯を足す（絵の後ろ）。size = 広げる窓の幅、soft = 帯のふちのぼかし。
        private static SdImage AddBand(SdImage img, int size, float[] color, float soft)
        {
            int w = img.Width, h = img.Height, n = w * h;
            var alpha = Channel(img, 3);
            var grown = GaussianBlur(MaxFilter(alpha, w, h, size), w, h, soft);
            var outImg = new SdImage(w, h);
            var s = img.Px;
            var o = outImg.Px;
            for (var i = 0; i < n; i++)
            {
                var a = alpha[i];
                var ring = Clamp01(grown[i] - a);
                var outA = Clamp01(a + ring);
                o[i * 4 + 3] = outA;
                if (outA <= 0f) continue;
                for (var c = 0; c < 3; c++)
                {
                    o[i * 4 + c] = (s[i * 4 + c] * a + color[c] * ring) / outA;
                }
            }

            return outImg;
        }

        // 絵の下にうっすら影（暗い紺）を敷く。
        private static SdImage AddShadow(SdImage img, int grow, float blur, float strength)
        {
            int w = img.Width, h = img.Height, n = w * h;
            var halo = Channel(img, 3);
            if (grow > 1)
            {
                halo = MaxFilter(halo, w, h, grow);
            }

            halo = GaussianBlur(halo, w, h, blur);
            var outImg = new SdImage(w, h);
            var s = img.Px;
            var o = outImg.Px;
            for (var i = 0; i < n; i++)
            {
                var a = s[i * 4 + 3];
                var sh = halo[i] * strength;
                var outA = a + sh * (1 - a);
                o[i * 4 + 3] = outA;
                if (outA <= 0f) continue;
                for (var c = 0; c < 3; c++)
                {
                    o[i * 4 + c] = (s[i * 4 + c] * a + ShadowColor[c] * sh * (1 - a)) / outA;
                }
            }

            return outImg;
        }

        private static SdImage Pad(SdImage img, int pad)
        {
            if (pad <= 0) return img;
            var outImg = new SdImage(img.Width + 2 * pad, img.Height + 2 * pad);
            for (var y = 0; y < img.Height; y++)
            {
                Array.Copy(img.Px, y * img.Width * 4, outImg.Px, ((y + pad) * outImg.Width + pad) * 4, img.Width * 4);
            }

            return outImg;
        }

        /// <summary>面積平均で縮小・拡大（色はアルファを掛けてから平均するので、透明な所の色がにじまない）。</summary>
        public static SdImage Resize(SdImage img, int newW, int newH)
        {
            int w = img.Width, h = img.Height;
            if (newW == w && newH == h) return img;
            var pre = new float[w * h * 4];
            for (var i = 0; i < w * h; i++)
            {
                var a = img.Px[i * 4 + 3];
                pre[i * 4] = img.Px[i * 4] * a;
                pre[i * 4 + 1] = img.Px[i * 4 + 1] * a;
                pre[i * 4 + 2] = img.Px[i * 4 + 2] * a;
                pre[i * 4 + 3] = a;
            }

            var tmp = new float[newW * h * 4];
            var wx = Weights(w, newW);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < newW; x++)
                {
                    foreach (var (src, weight) in wx[x])
                    {
                        for (var c = 0; c < 4; c++) tmp[(y * newW + x) * 4 + c] += pre[(y * w + src) * 4 + c] * weight;
                    }
                }
            }

            var outImg = new SdImage(newW, newH);
            var wy = Weights(h, newH);
            for (var y = 0; y < newH; y++)
            {
                for (var x = 0; x < newW; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    foreach (var (src, weight) in wy[y])
                    {
                        var j = (src * newW + x) * 4;
                        r += tmp[j] * weight; g += tmp[j + 1] * weight; b += tmp[j + 2] * weight; a += tmp[j + 3] * weight;
                    }

                    var k = (y * newW + x) * 4;
                    outImg.Px[k + 3] = Clamp01(a);
                    if (a > 1e-6f)
                    {
                        outImg.Px[k] = Clamp01(r / a); outImg.Px[k + 1] = Clamp01(g / a); outImg.Px[k + 2] = Clamp01(b / a);
                    }
                }
            }

            return outImg;
        }

        // 1次元の面積平均の重み（縮小: 範囲に入る元のピクセルの割合。拡大: 近い2つの直線補間）。
        private static (int, float)[][] Weights(int from, int to)
        {
            var result = new (int, float)[to][];
            var scale = (double)from / to;
            for (var i = 0; i < to; i++)
            {
                if (scale >= 1.0)
                {
                    var start = i * scale;
                    var end = start + scale;
                    var list = new System.Collections.Generic.List<(int, float)>();
                    for (var s = (int)Math.Floor(start); s < Math.Min(from, (int)Math.Ceiling(end)); s++)
                    {
                        var cover = Math.Min(end, s + 1) - Math.Max(start, s);
                        if (cover > 0) list.Add((s, (float)(cover / scale)));
                    }

                    result[i] = list.ToArray();
                }
                else
                {
                    var center = (i + 0.5) * scale - 0.5;
                    var s0 = (int)Math.Floor(center);
                    var t = (float)(center - s0);
                    result[i] = new[] { (Math.Max(0, Math.Min(from - 1, s0)), 1 - t), (Math.Max(0, Math.Min(from - 1, s0 + 1)), t) };
                }
            }

            return result;
        }

        private static float[] Channel(SdImage img, int c)
        {
            var n = img.Width * img.Height;
            var result = new float[n];
            for (var i = 0; i < n; i++) result[i] = img.Px[i * 4 + c];
            return result;
        }

        // 正方形の窓の最大・最小（縦横に分けて計算）。size は窓の幅。
        private static float[] MaxFilter(float[] v, int w, int h, int size) => WindowFilter(v, w, h, size, true);

        private static float[] MinFilter(float[] v, int w, int h, int size) => WindowFilter(v, w, h, size, false);

        private static float[] WindowFilter(float[] v, int w, int h, int size, bool max)
        {
            if (size <= 1) return (float[])v.Clone();
            var r = size / 2;
            var tmp = new float[v.Length];
            var outV = new float[v.Length];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var best = max ? float.MinValue : float.MaxValue;
                    for (var d = -r; d <= r; d++)
                    {
                        var xx = Math.Max(0, Math.Min(w - 1, x + d));
                        var val = v[y * w + xx];
                        best = max ? Math.Max(best, val) : Math.Min(best, val);
                    }

                    tmp[y * w + x] = best;
                }
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var best = max ? float.MinValue : float.MaxValue;
                    for (var d = -r; d <= r; d++)
                    {
                        var yy = Math.Max(0, Math.Min(h - 1, y + d));
                        var val = tmp[yy * w + x];
                        best = max ? Math.Max(best, val) : Math.Min(best, val);
                    }

                    outV[y * w + x] = best;
                }
            }

            return outV;
        }

        // ガウスぼかし（sigma = ぼかしの強さ、縦横に分けて計算、端は端の値をのばす）。
        private static float[] GaussianBlur(float[] v, int w, int h, float sigma)
        {
            if (sigma <= 0f) return (float[])v.Clone();
            var r = Math.Max(1, (int)Math.Ceiling(sigma * 3));
            var kernel = new float[r * 2 + 1];
            float total = 0;
            for (var i = -r; i <= r; i++)
            {
                kernel[i + r] = (float)Math.Exp(-(i * i) / (2.0 * sigma * sigma));
                total += kernel[i + r];
            }

            for (var i = 0; i < kernel.Length; i++) kernel[i] /= total;
            var tmp = new float[v.Length];
            var outV = new float[v.Length];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (var d = -r; d <= r; d++) sum += v[y * w + Math.Max(0, Math.Min(w - 1, x + d))] * kernel[d + r];
                    tmp[y * w + x] = sum;
                }
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    float sum = 0;
                    for (var d = -r; d <= r; d++) sum += tmp[Math.Max(0, Math.Min(h - 1, y + d)) * w + x] * kernel[d + r];
                    outV[y * w + x] = sum;
                }
            }

            return outV;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
