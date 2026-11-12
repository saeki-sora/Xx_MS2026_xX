using UnityEngine;

namespace MS2026.CyberCleanLook
{
    /// <summary>画面演出の絵（画像ファイルを持たず、起動時にコードで作る）。白い絵で、色は描くときに付ける。</summary>
    internal static class CyberCleanArt
    {
        private static Texture2D _line, _grid, _soft, _ray, _star, _ring, _foamBall, _glossy, _pop;

        /// <summary>
        /// 画面を埋める泡の1粒。左上から光が当たった球の陰影（明るい面・縁の影・つや）が絵に入っているので、色を掛けても泡に見える。
        /// </summary>
        public static Texture2D FoamBall => _foamBall != null ? _foamBall : _foamBall = BuildColor("FoamBall", 128, (x, y) =>
        {
            var nx = (x - 0.5f) * 2f;
            var ny = (y - 0.5f) * 2f;
            var d = Mathf.Sqrt(nx * nx + ny * ny);
            if (d >= 1f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            var nz = Mathf.Sqrt(1f - d * d);
            var light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;
            var lambert = Mathf.Clamp01(nx * light.x + ny * light.y + nz * light.z);
            // 泡は白っぽく、陰は浅く。
            var shade = Mathf.Lerp(0.84f, 1f, lambert);
            var color = new Color(shade, shade, Mathf.Min(1f, shade * 1.02f));
            // 縁の内側に光る細い輪（膜が光を返す所）。
            var fresnel = Mathf.Clamp01(1f - Mathf.Abs(d - 0.83f) / 0.06f);
            color = Color.Lerp(color, Color.white, fresnel * 0.55f);
            // 一番外の細い線は少しラベンダーに暗く、となりの泡との境目が見えるようにする。
            var outline = Mathf.Clamp01((d - 0.9f) / 0.1f);
            color = Color.Lerp(color, new Color(0.68f, 0.66f, 0.84f), outline * 0.75f);
            // つや: 左上の三日月の光と、小さな強い光、右下の照り返し。
            var angle = Mathf.Atan2(ny, nx) * Mathf.Rad2Deg;
            var crescent = Mathf.Clamp01(1f - Mathf.Abs(d - 0.66f) / 0.08f) * Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(angle, 128f)) / 38f);
            var dot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.3f, 0.36f)) / 0.13f);
            var bounce = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.42f, -0.46f)) / 0.12f) * 0.45f;
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(crescent * 0.95f + dot + bounce));
            color.a = Mathf.Clamp01((1f - d) * 48f);
            return color;
        });

        /// <summary>上に浮かぶ、透けて虹色に光るしゃぼん玉。</summary>
        public static Texture2D Glossy => _glossy != null ? _glossy : _glossy = BuildColor("Glossy", 256, (x, y) =>
        {
            var nx = (x - 0.5f) * 2f;
            var ny = (y - 0.5f) * 2f;
            var d = Mathf.Sqrt(nx * nx + ny * ny);
            if (d >= 1f)
            {
                return new Color(1f, 1f, 1f, 0f);
            }

            var angle = Mathf.Atan2(ny, nx);
            var hue = Mathf.Repeat(angle / (Mathf.PI * 2f) + d * 0.35f, 1f);
            var rainbow = Color.Lerp(Color.white, Color.HSVToRGB(hue, 0.55f, 1f), 0.75f);
            var film = Mathf.Pow(d, 4f);
            var edge = Mathf.Clamp01((1f - d) * 60f);
            var color = Color.Lerp(new Color(0.9f, 0.96f, 1f), rainbow, film);
            var alpha = Mathf.Lerp(0.1f, 0.8f, film) * edge;
            // 左上の弓なりの光と、右下の小さな光。
            var arc = Mathf.Clamp01(1f - Mathf.Abs(d - 0.72f) / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 132f)) / 34f);
            var dot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.3f, 0.32f)) / 0.11f);
            var dot2 = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, -0.48f)) / 0.08f) * 0.7f;
            var shine = Mathf.Clamp01(arc + dot + dot2);
            color = Color.Lerp(color, Color.white, shine);
            color.a = Mathf.Max(alpha, shine * edge);
            return color;
        });

        /// <summary>はじけたしずく（小さな粒が輪に並び、切れた膜の線が残る）。</summary>
        public static Texture2D PopBurst => _pop != null ? _pop : _pop = BuildColor("Pop", 192, (x, y) =>
        {
            var nx = (x - 0.5f) * 2f;
            var ny = (y - 0.5f) * 2f;
            var d = Mathf.Sqrt(nx * nx + ny * ny);
            var angle = Mathf.Atan2(ny, nx);
            var a = 0f;
            const int Drops = 14;
            for (var i = 0; i < Drops; i++)
            {
                var dropAngle = i * Mathf.PI * 2f / Drops + (i % 2) * 0.12f;
                var radius = i % 3 == 0 ? 0.86f : 0.74f;
                var size = i % 3 == 0 ? 0.075f : 0.055f;
                var center = new Vector2(Mathf.Cos(dropAngle), Mathf.Sin(dropAngle)) * radius;
                a = Mathf.Max(a, Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), center) / size) * 1.6f);
            }

            // 切れ切れの膜の線。
            var broken = Mathf.Sin(angle * 9f) > 0.2f ? 1f : 0f;
            a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(d - 0.6f) / 0.03f) * broken * 0.7f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        });

        private static Texture2D BuildColor(string label, int size, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CyberClean " + label,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>広がる輪（ふちだけ）。</summary>
        public static Texture2D Ring => _ring != null ? _ring : _ring = Build("Ring", 256, 256, (x, y) =>
        {
            var d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
            return Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 12f);
        });

        /// <summary>走査線（縦に光が細くまとまった線）。</summary>
        public static Texture2D Line => _line != null ? _line : _line = Build("Line", 4, 64, (x, y) =>
        {
            var d = Mathf.Abs(y - 0.5f) * 2f;
            return Mathf.Exp(-d * d * 18f) + 0.25f * Mathf.Exp(-d * d * 3f);
        });

        /// <summary>走査線が通った跡の方眼（下ほど濃い）。横に繰り返して使う。</summary>
        public static Texture2D Grid => _grid != null ? _grid : _grid = Build("Grid", 256, 256, (x, y) =>
        {
            var px = x * 256f;
            var py = y * 256f;
            var line = (px % 64f < 2f || py % 64f < 2f) ? 1f : 0f;
            // 線（絵の下の端）の近くほど濃く、上へ行くほど薄くなる。
            return line * Mathf.Pow(1f - y, 1.6f);
        }, TextureWrapMode.Repeat);

        /// <summary>ふちがやわらかい丸。</summary>
        public static Texture2D Soft => _soft != null ? _soft : _soft = Build("Soft", 128, 128, (x, y) =>
        {
            var d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
        });

        /// <summary>光の線（根元が濃く、先が消える）。</summary>
        public static Texture2D Ray => _ray != null ? _ray : _ray = Build("Ray", 32, 256, (x, y) =>
        {
            var side = 1f - Mathf.Abs(x - 0.5f) * 2f;
            return side * side * (1f - y);
        });

        /// <summary>4つの角の星。</summary>
        public static Texture2D Star => _star != null ? _star : _star = Build("Star", 128, 128, (x, y) =>
        {
            var dx = Mathf.Abs(x - 0.5f) * 2f;
            var dy = Mathf.Abs(y - 0.5f) * 2f;
            var arms = Mathf.Max(Mathf.Clamp01(1f - dx * 9f - dy * 0.95f), Mathf.Clamp01(1f - dy * 9f - dx * 0.95f));
            var core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f);
            return Mathf.Clamp01(arms * 1.4f + core);
        });


        // alpha には 0〜1 の (x, y) を渡す。y は絵の下が 0、上が 1。
        private static Texture2D Build(string label, int w, int h, System.Func<float, float, float> alpha, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "CyberClean " + label,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var a = Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h));
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
