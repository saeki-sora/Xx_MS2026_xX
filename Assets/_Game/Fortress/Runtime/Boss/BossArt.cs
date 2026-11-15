using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボスの仮の絵（コードで作る。デザイナーの絵が来たら <see cref="BossDefinition"/> の絵の欄に入れて差し替える。2026-11-15）。
    /// どの絵も 2×2 単位（半径 1）の大きさで作るので、使う側で半径ぶん拡大する。
    /// </summary>
    internal static class BossArt
    {
        private static Sprite _body;
        private static Sprite _cannon;
        private static Sprite _vent;
        private static Sprite _broken;
        private static Sprite _bullet;
        private static Sprite _stripe;

        public static Sprite Body => _body != null ? _body : _body = Make(256, BodyPixel, "BossBody");

        public static Sprite Cannon => _cannon != null ? _cannon : _cannon = Make(128, CannonPixel, "BossCannon");

        public static Sprite Vent => _vent != null ? _vent : _vent = Make(128, VentPixel, "BossVent");

        public static Sprite Broken => _broken != null ? _broken : _broken = Make(128, BrokenPixel, "BossBroken");

        public static Sprite Bullet => _bullet != null ? _bullet : _bullet = Make(64, BulletPixel, "BossBullet");

        /// <summary>ふちが柔らかい白い輪（はじける・爆発の仮の表現）。</summary>
        public static Sprite Puff => _puff != null ? _puff : _puff = Make(64, PuffPixel, "BossPuff");

        private static Sprite _puff;

        private static Color PuffPixel(float x, float y)
        {
            var r = Mathf.Sqrt(x * x + y * y);
            var ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / 0.2f);
            var fill = Mathf.Clamp01(1f - r / 0.72f) * 0.45f;
            return new Color(1f, 1f, 1f, Mathf.Max(ring, fill) * Mathf.Clamp01((1f - r) / 0.08f));
        }

        /// <summary>真っ白の四角（HP のバーなどに使う。1×1 単位）。</summary>
        public static Sprite White => _white != null ? _white : _white = MakeWhite();

        private static Sprite _white;

        private static Sprite MakeWhite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "BossWhite", hideFlags = HideFlags.DontSave };
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>「WARNING」の帯の縞（横に繰り返す）。</summary>
        public static Sprite Stripe => _stripe != null ? _stripe : _stripe = MakeStripe();

        private delegate Color PixelFn(float x, float y);

        private static Sprite Make(int size, PixelFn fn, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    var x = (i + 0.5f) / size * 2f - 1f;
                    var y = (j + 0.5f) / size * 2f - 1f;
                    pixels[j * size + i] = fn(x, y);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Sprite MakeStripe()
        {
            const int w = 64, h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "BossStripe", wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[w * h];
            for (var j = 0; j < h; j++)
            {
                for (var i = 0; i < w; i++)
                {
                    var band = Mathf.Repeat(i + j, 32f) < 16f;
                    pixels[j * w + i] = band ? new Color32(229, 72, 77, 255) : new Color32(26, 10, 14, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        // ───────── 形 ─────────

        private static float Smooth(float edge, float d) => Mathf.Clamp01(0.5f - d / Mathf.Max(1e-4f, edge));

        private static Color Over(Color under, Color over)
        {
            var a = over.a + under.a * (1f - over.a);
            if (a <= 1e-5f)
            {
                return Color.clear;
            }

            var rgb = ((Vector4)over * over.a + (Vector4)under * under.a * (1f - over.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        private static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        private static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            var px = x - ax;
            var py = y - ay;
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            var qx = px - dx * t;
            var qy = py - dy * t;
            return Mathf.Sqrt(qx * qx + qy * qy) - r;
        }

        // 体: トゲのある丸い菌（左向きの顔）。トゲの付け根が半径 0.8。
        private static Color BodyPixel(float x, float y)
        {
            const float aa = 0.012f;
            var r = Mathf.Sqrt(x * x + y * y);
            var angle = Mathf.Atan2(y, x);
            var spike = Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 2f) * 22f, 1f) - 0.5f) * 2f; // 0..1 の三角波
            var edge = 0.8f + 0.15f * (1f - spike) * (1f - spike) + 0.01f * Mathf.Sin(angle * 5f);
            var d = r - edge;
            var color = Color.clear;
            var fill = Smooth(aa, d);
            if (fill <= 0f && d > 0.06f)
            {
                return Color.clear;
            }

            // 体の色: 左上が明るい紫、ふちが濃い紫。
            var light = Mathf.Clamp01(1f - Mathf.Sqrt((x + 0.35f) * (x + 0.35f) + (y - 0.4f) * (y - 0.4f)) / 1.2f);
            var body = Color.Lerp(new Color(0.26f, 0.08f, 0.35f), new Color(0.69f, 0.4f, 0.82f), light);
            color = Over(color, WithA(new Color(0.15f, 0.04f, 0.2f), Smooth(aa, d - 0.035f)));
            color = Over(color, WithA(body, fill * Smooth(aa, d + 0.035f)));

            // 斑点（薄く）。
            foreach (var (sx, sy, sr) in new[] { (0.25f, 0.5f, 0.12f), (0.45f, -0.1f, 0.16f), (0.12f, -0.55f, 0.1f), (0.55f, 0.25f, 0.08f) })
            {
                color = Over(color, WithA(Color.white, 0.09f * Smooth(aa, Circle(x, y, sx, sy, sr))));
            }

            // 目（左を見る）。
            foreach (var (ex, ey, er) in new[] { (-0.42f, 0.18f, 0.16f), (-0.08f, 0.27f, 0.13f) })
            {
                var de = Circle(x, y, ex, ey, er);
                color = Over(color, WithA(new Color(0.15f, 0.04f, 0.2f), Smooth(aa, de - 0.03f)));
                color = Over(color, WithA(Color.white, Smooth(aa, de)));
                color = Over(color, WithA(new Color(0.1f, 0.05f, 0.12f), Smooth(aa, Circle(x, y, ex - er * 0.4f, ey - er * 0.05f, er * 0.48f))));
                color = Over(color, WithA(Color.white, Smooth(aa, Circle(x, y, ex - er * 0.55f, ey + er * 0.15f, er * 0.13f))));
            }

            // 怒った眉。
            color = Over(color, WithA(new Color(0.15f, 0.04f, 0.2f), Smooth(aa, Segment(x, y, -0.62f, 0.44f, -0.3f, 0.36f, 0.035f))));
            color = Over(color, WithA(new Color(0.15f, 0.04f, 0.2f), Smooth(aa, Segment(x, y, -0.22f, 0.5f, 0.06f, 0.48f, 0.035f))));

            // ギザギザの口。
            var mouth = float.MaxValue;
            for (var k = 0; k < 7; k++)
            {
                var ax = -0.66f + k * 0.07f;
                var bx = ax + 0.07f;
                var ay = -0.12f - k * 0.016f + (k % 2 == 0 ? 0f : -0.07f);
                var by = -0.12f - (k + 1) * 0.016f + (k % 2 == 0 ? -0.07f : 0f);
                mouth = Mathf.Min(mouth, Segment(x, y, ax, ay, bx, by, 0.025f));
            }

            color = Over(color, WithA(new Color(0.15f, 0.04f, 0.2f), Smooth(aa, mouth)));
            return color;
        }

        // 砲口: 丸い付け根 ＋ 右へ伸びる筒（ピンクに光る口）。
        private static Color CannonPixel(float x, float y)
        {
            const float aa = 0.02f;
            var dark = new Color(0.1f, 0.06f, 0.13f);
            var tube = Mathf.Max(Mathf.Abs(y) - 0.26f, Mathf.Max(-0.2f - x, x - 0.86f));
            var color = Color.clear;
            color = Over(color, WithA(dark, Smooth(aa, tube - 0.06f)));
            color = Over(color, WithA(new Color(0.29f, 0.21f, 0.35f), Smooth(aa, tube)));
            color = Over(color, WithA(dark, Smooth(aa, Circle(x, y, 0.82f, 0f, 0.2f))));
            color = Over(color, WithA(new Color(1f, 0.42f, 0.66f), Smooth(aa, Circle(x, y, 0.82f, 0f, 0.14f))));
            var baseD = Circle(x, y, -0.3f, 0f, 0.5f);
            color = Over(color, WithA(dark, Smooth(aa, baseD - 0.06f)));
            color = Over(color, WithA(new Color(0.36f, 0.27f, 0.44f), Smooth(aa, baseD)));
            color = Over(color, WithA(Color.white, 0.22f * Smooth(aa, Circle(x, y, -0.42f, 0.16f, 0.14f))));
            return color;
        }

        // 菌を出す口: 暗い穴とピンクのふち。
        private static Color VentPixel(float x, float y)
        {
            const float aa = 0.02f;
            var d = Circle(x, y, 0f, 0f, 0.82f);
            var color = Color.clear;
            color = Over(color, WithA(new Color(0.83f, 0.48f, 0.63f), Smooth(aa, d - 0.12f)));
            color = Over(color, WithA(new Color(0.17f, 0.06f, 0.21f), Smooth(aa, d + 0.04f)));
            color = Over(color, WithA(new Color(0.92f, 0.43f, 0.66f), 0.45f * Smooth(0.3f, Circle(x, y, 0f, 0f, 0.35f))));
            color = Over(color, WithA(Color.white, Smooth(aa, Circle(x, y, -0.18f, 0.05f, 0.1f))));
            color = Over(color, WithA(Color.white, Smooth(aa, Circle(x, y, 0.18f, 0.05f, 0.1f))));
            return color;
        }

        // 壊れた部位: 灰色の切り株と、オレンジのひび。
        private static Color BrokenPixel(float x, float y)
        {
            const float aa = 0.02f;
            var d = Circle(x, y, 0f, 0f, 0.66f);
            var color = Color.clear;
            color = Over(color, WithA(new Color(0.1f, 0.08f, 0.12f), Smooth(aa, d - 0.07f)));
            color = Over(color, WithA(new Color(0.24f, 0.21f, 0.26f), Smooth(aa, d)));
            var crack = Mathf.Min(Segment(x, y, -0.35f, 0.3f, 0.05f, 0f, 0.05f), Segment(x, y, 0.05f, 0f, -0.1f, -0.35f, 0.05f));
            crack = Mathf.Min(crack, Segment(x, y, 0.3f, 0.32f, 0.05f, 0f, 0.05f));
            color = Over(color, WithA(new Color(1f, 0.6f, 0.24f), Smooth(aa, crack)));
            return color;
        }

        // 弾: オレンジに光る玉と、濃いふち（敵の菌と見分けやすく）。
        private static Color BulletPixel(float x, float y)
        {
            const float aa = 0.04f;
            var d = Circle(x, y, 0f, 0f, 0.8f);
            var core = Mathf.Clamp01(1f - Mathf.Sqrt((x + 0.2f) * (x + 0.2f) + (y - 0.2f) * (y - 0.2f)) / 0.9f);
            var color = Color.clear;
            color = Over(color, WithA(new Color(0.35f, 0.09f, 0.19f), Smooth(aa, d - 0.12f)));
            color = Over(color, WithA(Color.Lerp(new Color(0.95f, 0.33f, 0.18f), new Color(1f, 0.95f, 0.78f), core), Smooth(aa, d)));
            return color;
        }
    }
}
