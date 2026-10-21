using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑スキルの絵（タブレット・葉っぱ・粉・十字・波の輪・膜・点線の輪・ケース・蓋・画面のふち）を、画像ファイルを使わずコードで描く係。
    /// 形は数式（円・角丸の四角・2つの円の重なり＝葉っぱ）で決め、ふちは1ピクセルぶんぼかしてギザギザを消す。
    /// 色は設定ファイルの「色（ミントの配色）」から作る（絵は Play の始めに1回だけ描く）。他人の絵や画像は一切使っていない。
    /// </summary>
    internal sealed class GreenMintArt : IDisposable
    {
        public const int CaseWidthPx = 150;
        public const int CaseBodyHeightPx = 200;
        public const int CaseLidHeightPx = 34;

        /// <summary>ケースの窓（絵の中の位置、0〜1。左下が 0,0）。中のタブレットはこの中で跳ねる。</summary>
        public static readonly Rect CaseWindow = new Rect(0.2f, 0.43f, 0.6f, 0.42f);

        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly List<Sprite> _sprites = new List<Sprite>();

        public GreenMintArt(GreenMintPalette palette)
        {
            Tablet = MakeSprite(PaintTablet(palette, 128), new Vector2(0.5f, 0.5f));
            Leaf = MakeSprite(PaintLeaf(palette, 96), new Vector2(0.5f, 0.5f));
            SoftDot = PaintSoftDot(64);
            Cross = PaintCross(64);
            LeafTexture = Leaf.texture;
            WaveRing = MakeSprite(PaintWaveRing(256), new Vector2(0.5f, 0.5f));
            Membrane = MakeSprite(PaintMembrane(256), new Vector2(0.5f, 0.5f));
            DashedRing = MakeSprite(PaintDashedRing(384), new Vector2(0.5f, 0.5f));
            CrossSprite = MakeSprite(Cross, new Vector2(0.5f, 0.5f));
            Vignette = PaintVignette(128);
            CaseBody = MakeSprite(PaintCaseBody(palette), new Vector2(0.5f, 0f), CaseWidthPx);
            CaseWindowBack = MakeSprite(PaintCaseWindowBack(palette), new Vector2(0.5f, 0f), CaseWidthPx);
            // 蓋は左下（ちょうつがい）を中心に開くので、絵の中心も左下にする。
            CaseLid = MakeSprite(PaintCaseLid(palette), new Vector2(0f, 0f), CaseWidthPx);
        }

        /// <summary>白い錠剤（ふちが緑、真ん中に割線）。直径 1 m。</summary>
        public Sprite Tablet { get; }

        /// <summary>ミントの葉っぱ（縦長）。高さ 1 m。</summary>
        public Sprite Leaf { get; }

        public Texture2D LeafTexture { get; }

        /// <summary>白くやわらかい丸（粉の粒）。</summary>
        public Texture2D SoftDot { get; }

        /// <summary>回復の十字（白。外側に少し暗いふち）。</summary>
        public Texture2D Cross { get; }

        public Sprite CrossSprite { get; }

        /// <summary>広がる波の輪（白。直径 1 m）。</summary>
        public Sprite WaveRing { get; }

        /// <summary>コアを包む膜（シャボン玉のような丸。直径 1 m）。</summary>
        public Sprite Membrane { get; }

        /// <summary>守る範囲の点線の輪（直径 1 m）。</summary>
        public Sprite DashedRing { get; }

        /// <summary>画面のふちを光らせる絵（真ん中が透明）。</summary>
        public Texture2D Vignette { get; }

        /// <summary>ケースの本体（窓の部分は透明）。幅 1 m、中心は下のまん中。</summary>
        public Sprite CaseBody { get; }

        /// <summary>ケースの窓の奥（うすいミント色の透けた板）。</summary>
        public Sprite CaseWindowBack { get; }

        /// <summary>ケースの蓋。中心は左下（ちょうつがい）。</summary>
        public Sprite CaseLid { get; }

        public void Dispose()
        {
            for (var i = 0; i < _sprites.Count; i++)
            {
                DestroySafe(_sprites[i]);
            }

            for (var i = 0; i < _textures.Count; i++)
            {
                DestroySafe(_textures[i]);
            }

            _sprites.Clear();
            _textures.Clear();
        }

        // ───────────── 1枚ずつの絵 ─────────────

        private Texture2D PaintTablet(GreenMintPalette palette, int size)
        {
            var white = palette.tablet;
            var rim = Color.Lerp(palette.tablet, palette.green, 0.55f);
            var outline = palette.outline;
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                var r = p.magnitude;
                var alpha = Edge(0.97f - r, px * 2f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                // 面: 右下ほど少し暗い（立体感）。
                var shade = Mathf.Clamp01(0.5f + Vector2.Dot(p, new Vector2(-0.45f, 0.55f)) * 0.5f);
                var face = Color.Lerp(white * 0.84f, white, shade);
                face.a = 1f;

                // 割線（真ん中の溝）と、その下の光。
                var groove = Edge(0.055f - Mathf.Abs(p.y - 0.01f), px * 2f) * Edge(0.6f - Mathf.Abs(p.x), px * 2f);
                face = Color.Lerp(face, white * 0.66f, groove * 0.8f);
                var grooveLight = Edge(0.035f - Mathf.Abs(p.y + 0.075f), px * 2f) * Edge(0.58f - Mathf.Abs(p.x), px * 2f);
                face = Color.Lerp(face, Color.white, grooveLight * 0.7f);

                // ふち（ミント）と輪郭。
                var c = Color.Lerp(face, rim, Edge(r - 0.7f, px * 2f));
                c = Color.Lerp(c, outline, Edge(r - 0.86f, px * 2f));

                // つや（左上の三日月）。
                var shine = Edge(0.17f - (p - new Vector2(-0.34f, 0.38f)).magnitude, px * 4f) * (1f - Edge(0.12f - (p - new Vector2(-0.26f, 0.3f)).magnitude, px * 4f));
                c = Color.Lerp(c, Color.white, shine * 0.9f);
                c.a = alpha;
                return c;
            });
        }

        private Texture2D PaintLeaf(GreenMintPalette palette, int size)
        {
            var fill = palette.leaf;
            var light = Color.Lerp(palette.leaf, palette.mint, 0.55f);
            var outline = palette.outline;
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                // 2つの円の重なり＝葉っぱ（縦長）。先を少しだけ右に曲げる。
                p.x -= 0.12f * p.y * p.y;
                const float d = 0.9f;
                const float radius = 1.32f;
                var sdf = Mathf.Max((p - new Vector2(d, 0f)).magnitude - radius, (p - new Vector2(-d, 0f)).magnitude - radius);
                sdf = Mathf.Max(sdf, Mathf.Abs(p.y) - 0.96f);
                var alpha = Edge(-sdf, px * 2f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                var c = Color.Lerp(fill, light, Mathf.Clamp01(0.5f - p.x * 1.6f));
                var vein = Edge(0.035f - Mathf.Abs(p.x), px * 2f) * Edge(0.85f - Mathf.Abs(p.y), px);
                c = Color.Lerp(c, light * 1.15f, vein * 0.8f);
                // 横のすじ（斜め）。
                var side = Mathf.Abs(Mathf.Repeat(p.y - Mathf.Abs(p.x) * 0.9f, 0.32f) - 0.16f);
                c = Color.Lerp(c, light, Edge(0.022f - side, px * 2f) * Edge(0.3f - Mathf.Abs(p.x), px) * 0.55f);
                c = Color.Lerp(c, outline, Edge(sdf + 0.09f, px * 2f));
                c.a = alpha;
                return c;
            });
        }

        private Texture2D PaintSoftDot(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                var a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        private Texture2D PaintCross(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                var sdf = Mathf.Min(RoundBox(p, new Vector2(0.78f, 0.26f), 0.12f), RoundBox(p, new Vector2(0.26f, 0.78f), 0.12f));
                var alpha = Edge(0.12f - sdf, px * 2f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                // 中は白、外側のふちは暗め（色を掛けると濃い緑のふちになる）。
                var inner = Edge(-sdf, px * 2f);
                var c = Color.Lerp(new Color(0.32f, 0.32f, 0.32f), Color.white, inner);
                c.a = alpha;
                return c;
            });
        }

        private Texture2D PaintWaveRing(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                var ring = Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.05f, 2f));
                var trail = Mathf.Pow(Mathf.Clamp01(r / 0.9f), 4f) * 0.28f * (r < 0.9f ? 1f : 0f);
                var a = Mathf.Clamp01(ring + trail) * Edge(1f - r, px * 2f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        private Texture2D PaintMembrane(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                var r = p.magnitude;
                var inside = Edge(0.98f - r, px * 2f);
                if (inside <= 0f)
                {
                    return Color.clear;
                }

                var a = 0.16f + 0.62f * Mathf.SmoothStep(0.72f, 0.97f, r);
                // つや（左上）。
                var shine = Edge(0.2f - (p - new Vector2(-0.45f, 0.5f)).magnitude, px * 6f);
                var c = Color.Lerp(new Color(1f, 1f, 1f), new Color(1.2f, 1.2f, 1.2f), shine);
                c.a = Mathf.Clamp01(a + shine * 0.5f) * inside;
                return c;
            });
        }

        private Texture2D PaintDashedRing(int size)
        {
            const int dashes = 56;
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                var r = p.magnitude;
                var band = Edge(0.012f - Mathf.Abs(r - 0.975f), px * 1.5f);
                if (band <= 0f)
                {
                    return Color.clear;
                }

                var angle = Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) + 0.5f;
                var dash = Mathf.Repeat(angle * dashes, 1f);
                var on = Edge(0.62f - dash, 0.04f) * Edge(dash, 0.04f);
                return new Color(1f, 1f, 1f, band * on);
            });
        }

        private Texture2D PaintVignette(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(Mathf.Abs(x * 2f - 1f), Mathf.Abs(y * 2f - 1f));
                var q = new Vector2(Mathf.Max(0f, p.x - 0.35f), Mathf.Max(0f, p.y - 0.35f)) / 0.65f;
                var e = Mathf.Clamp01(q.magnitude);
                return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.SmoothStep(0f, 1f, e), 1.6f));
            });
        }

        private Texture2D PaintCaseBody(GreenMintPalette palette)
        {
            const int w = CaseWidthPx;
            const int h = CaseBodyHeightPx;
            var aspect = (float)h / w;
            var body = palette.green;
            var dark = palette.deep;
            var light = Color.Lerp(palette.green, palette.mint, 0.7f);
            var outline = palette.outline;
            var label = palette.tablet;
            var emblem = palette.leaf;
            var window = CaseWindow;
            return Paint(w, h, (x, y, px) =>
            {
                // 横 0〜1、縦 0〜aspect のメートル風の座標で描く（角丸が縦に伸びないように）。
                var p = new Vector2(x, y * aspect);
                var outer = RoundBoxCorner(p, new Vector2(0.02f, 0.02f), new Vector2(0.98f, aspect - 0.005f), 0.16f);
                var alpha = Edge(-outer, px * 1.5f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                // 本体: 左に明るい帯、右に影。
                var c = body;
                c = Color.Lerp(c, dark, Edge(x - 0.8f, 0.03f) * 0.7f);
                c = Color.Lerp(c, light, Edge(0.05f - Mathf.Abs(x - 0.13f), 0.02f) * 0.85f);
                // 下のふくらみの影。
                c = Color.Lerp(c, dark, Edge(0.06f - y * aspect, 0.04f) * 0.5f);

                // ラベル（白い帯）と葉っぱのマーク。
                var labelBox = RoundBoxCorner(p, new Vector2(0.1f, 0.15f * aspect), new Vector2(0.9f, 0.36f * aspect), 0.05f);
                var onLabel = Edge(-labelBox, px * 1.5f);
                c = Color.Lerp(c, label, onLabel);
                var leafCenter = new Vector2(0.5f, 0.255f * aspect);
                var lp = (p - leafCenter) / 0.13f;
                lp = new Vector2(lp.x * 0.8f + lp.y * 0.6f, -lp.x * 0.6f + lp.y * 0.8f);
                var leafSdf = Mathf.Max((lp - new Vector2(0.9f, 0f)).magnitude - 1.32f, (lp - new Vector2(-0.9f, 0f)).magnitude - 1.32f);
                c = Color.Lerp(c, emblem, Edge(-leafSdf * 0.13f, px * 1.5f) * onLabel);
                // ラベルの細い線（左右）。
                var stripe = Edge(0.012f - Mathf.Abs(y * aspect - 0.185f * aspect), px) + Edge(0.012f - Mathf.Abs(y * aspect - 0.325f * aspect), px);
                c = Color.Lerp(c, emblem, Mathf.Clamp01(stripe) * onLabel * Edge(Mathf.Abs(x - 0.5f) - 0.2f, px));
                c = Color.Lerp(c, outline, Edge(0.01f - Mathf.Abs(labelBox), px * 1.5f) * 0.8f);

                // 窓（透明）と窓のふち。
                var windowBox = RoundBoxCorner(p, new Vector2(window.xMin, window.yMin * aspect), new Vector2(window.xMax, window.yMax * aspect), 0.07f);
                var ring = Edge(0.022f - Mathf.Abs(windowBox), px * 1.5f);
                var hole = Edge(-windowBox - 0.022f, px * 1.5f);
                c = Color.Lerp(c, outline, ring);

                // 外側の輪郭。
                c = Color.Lerp(c, outline, Edge(outer + 0.035f, px * 1.5f));
                c.a = alpha * (1f - hole);
                return c;
            });
        }

        private Texture2D PaintCaseWindowBack(GreenMintPalette palette)
        {
            const int w = CaseWidthPx;
            const int h = CaseBodyHeightPx;
            var aspect = (float)h / w;
            var tint = Color.Lerp(palette.mint, Color.white, 0.35f);
            var window = CaseWindow;
            return Paint(w, h, (x, y, px) =>
            {
                var p = new Vector2(x, y * aspect);
                var box = RoundBoxCorner(p, new Vector2(window.xMin, window.yMin * aspect), new Vector2(window.xMax, window.yMax * aspect), 0.07f);
                var inside = Edge(-box, px * 1.5f);
                if (inside <= 0f)
                {
                    return Color.clear;
                }

                var c = tint;
                var shine = Edge(0.04f - Mathf.Abs(x - 0.32f - (y - 0.6f) * 0.25f), px * 2f);
                c = Color.Lerp(c, Color.white, shine * 0.8f);
                c.a = (0.55f + shine * 0.35f) * inside;
                return c;
            });
        }

        private Texture2D PaintCaseLid(GreenMintPalette palette)
        {
            const int w = CaseWidthPx;
            const int h = CaseLidHeightPx;
            var aspect = (float)h / w;
            var lid = palette.deep;
            var light = Color.Lerp(palette.deep, palette.green, 0.6f);
            var outline = palette.outline;
            return Paint(w, h, (x, y, px) =>
            {
                var p = new Vector2(x, y * aspect);
                var box = RoundBoxCorner(p, new Vector2(0f, 0f), new Vector2(1f, aspect), 0.08f);
                var alpha = Edge(-box, px * 1.5f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                var c = Color.Lerp(lid, light, Mathf.Clamp01(y * 1.4f));
                // 縦のみぞ（すべり止め）。
                var groove = Mathf.Abs(Mathf.Repeat(x * 14f, 1f) - 0.5f);
                c = Color.Lerp(c, lid * 0.75f, Edge(0.12f - groove, 0.08f) * Edge(Mathf.Abs(x - 0.5f) - 0.06f, px) * 0.6f);
                c = Color.Lerp(c, Color.white, Edge(0.012f - Mathf.Abs(y * aspect - aspect * 0.78f), px) * 0.35f);
                c = Color.Lerp(c, outline, Edge(box + 0.03f, px * 1.5f));
                c.a = alpha;
                return c;
            });
        }

        // ───────────── 道具 ─────────────

        /// <summary>v が 0 より大きいほど 1 に近づく、幅 width のなめらかな境目（ふちのギザギザ消し）。</summary>
        private static float Edge(float v, float width)
        {
            return Mathf.Clamp01(v / Mathf.Max(width, 1e-5f) + 0.5f);
        }

        private static float RoundBox(Vector2 p, Vector2 halfSize, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(radius, radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static float RoundBoxCorner(Vector2 p, Vector2 min, Vector2 max, float radius)
        {
            var center = (min + max) * 0.5f;
            return RoundBox(p - center, (max - min) * 0.5f, radius);
        }

        /// <summary>
        /// 1ピクセルずつ色を決めて絵を作る。paint には 0〜1 の座標（ピクセルの真ん中）と、1ピクセルの大きさ（0〜1 の単位）を渡す。
        /// </summary>
        private Texture2D Paint(int width, int height, Func<float, float, float, Color> paint)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "GreenSkillArt",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[width * height];
            var px = 1f / width;
            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                for (var x = 0; x < width; x++)
                {
                    var c = paint((x + 0.5f) / width, v, px);
                    c.r = Mathf.Clamp01(c.r);
                    c.g = Mathf.Clamp01(c.g);
                    c.b = Mathf.Clamp01(c.b);
                    pixels[y * width + x] = c;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            _textures.Add(texture);
            return texture;
        }

        private Sprite MakeSprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit = -1f)
        {
            var ppu = pixelsPerUnit > 0f ? pixelsPerUnit : texture.width;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _sprites.Add(sprite);
            return sprite;
        }

        private static void DestroySafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
