using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑スキルの仮の絵（守護霊の3つのポーズとまわりの光・葉っぱ・チューブ・煙・粉・十字・衝撃波の輪・白い線・光の柱・画面のふち）を、
    /// 画像ファイルを使わずコードで描く係。形は数式（円・太い線・楕円・多角形）で決め、ふちは1ピクセルぶんぼかす。
    /// 形と色は、デザイナー向け決定版ページの見本と同じ（ユーザーのキャラの絵の守護霊に合わせた形。他人の絵は一切使っていない）。
    /// 設定ファイルの「絵（差し替え）」に絵が入っていれば、その絵を使う（この係の絵は使われない）。絵は Play の始めに1回だけ描く。
    /// </summary>
    internal sealed class GreenMintArt : IDisposable
    {
        /// <summary>守護霊の絵の解像度（守護霊の高さ 1 あたりのピクセル）。</summary>
        private const float SpiritPixelsPerHeight = 560f;

        private const int SpiritWidthPx = 640;
        private const int SpiritHeightPx = 576;

        /// <summary>守護霊の中心（コアに重なる点）のピクセル位置（上から）。</summary>
        private const float SpiritOriginFromTop = 403f;

        private const int HaloDownsample = 4;

        /// <summary>手の位置（守護霊の高さ 1、y は下向き）: 降りる・広げる・抱える。</summary>
        private static readonly Vector2[][] PoseHands =
        {
            new[] { new Vector2(-0.26f, -0.58f), new Vector2(0.26f, -0.58f) },
            new[] { new Vector2(-0.40f, -0.40f), new Vector2(0.40f, -0.40f) },
            new[] { new Vector2(-0.17f, 0.03f), new Vector2(0.17f, 0.03f) }
        };

        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private readonly List<Vector2> _path = new List<Vector2>(64);

        public GreenMintArt(GreenGuardianPalette palette, float spiritFillOpacity)
        {
            SpiritPoses = new Sprite[PoseHands.Length];
            SpiritHalos = new Sprite[PoseHands.Length];
            for (var i = 0; i < PoseHands.Length; i++)
            {
                PaintSpirit(palette, spiritFillOpacity, PoseHands[i], out var body, out var halo);
                var pivot = new Vector2(0.5f, 1f - SpiritOriginFromTop / SpiritHeightPx);
                SpiritPoses[i] = MakeSprite(body, pivot, SpiritPixelsPerHeight);
                SpiritHalos[i] = MakeSprite(halo, pivot, SpiritPixelsPerHeight / HaloDownsample);
            }

            Leaf = MakeSprite(PaintLeaf(palette), new Vector2(LeafPivotX, 0.5f), LeafWidthPx);
            Tube = MakeSprite(PaintTube(palette), new Vector2(TubePivotX, 0.5f), TubePixelsPerMeter);
            SoftDot = PaintSoftDot(64);
            PowderDot = PaintPowder(palette, 64);
            SmokeDot = PaintSmoke(palette, 96);
            Cross = PaintCross(palette, 64);
            WaveRing = MakeSprite(PaintWaveRing(256), new Vector2(0.5f, 0.5f));
            Slash = MakeSprite(PaintSlash(128, 48), new Vector2(0.5f, 0.5f), 128f);
            Pillar = MakeSprite(PaintPillar(48, 192), new Vector2(0.5f, 0f), 48f);
            Vignette = PaintVignette(128);
        }

        /// <summary>守護霊の仮の絵（降りる・広げる・抱える）。高さ 1 の守護霊が 1 単位、中心（Pivot）がコアに重なる点。</summary>
        public Sprite[] SpiritPoses { get; }

        /// <summary>守護霊のまわりの光（ポーズごと。体の絵と同じ大きさ・中心）。</summary>
        public Sprite[] SpiritHalos { get; }

        /// <summary>葉っぱ（先が右）。横幅 1 単位。</summary>
        public Sprite Leaf { get; }

        /// <summary>歯みがき粉のチューブ（口が右）。1 単位 = 1 m（長さ約2.2m の絵）。</summary>
        public Sprite Tube { get; }

        /// <summary>チューブの口の位置（チューブの横幅を 1 とした割合、中心から）。</summary>
        public static Vector2 TubeNozzle => new Vector2((1.13f) / TubeWidthMeters, 0f);

        /// <summary>白くやわらかい丸（光の粒など）。</summary>
        public Texture2D SoftDot { get; }

        /// <summary>ミントの粉（白い中・緑のふち。色付き）。</summary>
        public Texture2D PowderDot { get; }

        /// <summary>煙の粒（うすい黄緑の中・緑のふち。色付き）。</summary>
        public Texture2D SmokeDot { get; }

        /// <summary>回復の十字（緑の中・白いふち。色付き）。</summary>
        public Texture2D Cross { get; }

        /// <summary>広がる衝撃波の輪（白。直径 1）。</summary>
        public Sprite WaveRing { get; }

        /// <summary>当たったときの白い線（弧。横幅 1）。</summary>
        public Sprite Slash { get; }

        /// <summary>光の柱（下が中心。幅 1、縦 4）。</summary>
        public Sprite Pillar { get; }

        /// <summary>画面のふちを光らせる絵（真ん中が透明）。</summary>
        public Texture2D Vignette { get; }

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

        // ───────────── 守護霊 ─────────────

        /// <summary>
        /// 守護霊1ポーズ。決定版ページの見本の形をそのまま使う（ふわふわの髪・長いコート・広い袖・手）。
        /// 体はうすく（fillOpacity）、輪郭は濃い線。服と髪の細い線は体の中だけ。まわりの光は別の絵（ぼかした形）。
        /// </summary>
        private void PaintSpirit(GreenGuardianPalette palette, float fillOpacity, Vector2[] hands, out Texture2D body, out Texture2D halo)
        {
            var scale = SpiritPixelsPerHeight;
            var outlinePx = Mathf.Max(1.2f, 0.006f * scale);
            var shape = new GreenSdfCanvas(SpiritWidthPx, SpiritHeightPx, scale, SpiritWidthPx * 0.5f, SpiritOriginFromTop, Mathf.CeilToInt(outlinePx) + 3);
            var lines = new GreenSdfCanvas(SpiritWidthPx, SpiritHeightPx, scale, SpiritWidthPx * 0.5f, SpiritOriginFromTop, 3);

            // コート（肩から裾まで。裾は小さく波打つ）。
            _path.Clear();
            _path.Add(new Vector2(-0.08f, -0.32f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(-0.08f, -0.32f), new Vector2(-0.15f, -0.30f), new Vector2(-0.135f, -0.13f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(-0.135f, -0.13f), new Vector2(-0.12f, 0.02f), new Vector2(-0.19f, 0.18f));
            const float hemHalf = 0.19f;
            for (var i = 1; i <= 8; i++)
            {
                var s = i / 8f;
                _path.Add(new Vector2(-hemHalf + 2f * hemHalf * s, 0.18f + Mathf.Sin(s * Mathf.PI * 3f) * 0.018f + (i % 2 == 1 ? 0.02f : 0f)));
            }

            GreenSdfCanvas.AddQuadratic(_path, new Vector2(0.19f, 0.18f), new Vector2(0.12f, 0.02f), new Vector2(0.135f, -0.13f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(0.135f, -0.13f), new Vector2(0.15f, -0.30f), new Vector2(0.08f, -0.32f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(0.08f, -0.32f), new Vector2(0f, -0.35f), new Vector2(-0.08f, -0.32f), 6);
            shape.Polygon(_path);
            shape.Circle(new Vector2(0f, -0.34f), 0.04f);

            // 腕（肩から手まで曲がった袖）。
            for (var side = 0; side < 2; side++)
            {
                PaintArm(shape, lines, side == 0 ? -1f : 1f, hands[side]);
            }

            // 頭とふわふわの髪。
            var head = new Vector2(0.005f, -0.43f);
            shape.Circle(head, 0.078f);
            for (var j = 0; j < 11; j++)
            {
                var a = j / 11f * Mathf.PI * 2f - Mathf.PI * 0.5f;
                shape.Circle(head + new Vector2(Mathf.Cos(a) * 0.072f, Mathf.Sin(a) * 0.068f), 0.032f + 0.006f * Mathf.Sin(j * 1.7f));
            }

            shape.Circle(head + new Vector2(0.11f, 0.02f), 0.03f);
            shape.Circle(head + new Vector2(0.135f, 0.045f), 0.018f);
            shape.Circle(head + new Vector2(-0.10f, 0.05f), 0.028f);
            shape.Circle(head + new Vector2(-0.06f, 0.075f), 0.03f);
            shape.Circle(head + new Vector2(0.06f, 0.075f), 0.03f);

            // 服と髪の細い線。
            var lineRadius = Mathf.Max(0.5f, outlinePx * 0.5f) / scale;
            lines.Capsule(new Vector2(-0.06f, -0.31f), new Vector2(0f, -0.16f), lineRadius);
            lines.Capsule(new Vector2(0f, -0.16f), new Vector2(0.06f, -0.31f), lineRadius);
            lines.Capsule(new Vector2(0.005f, -0.16f), new Vector2(0.015f, 0.17f), lineRadius);
            lines.Capsule(new Vector2(-0.12f, -0.03f), new Vector2(0.12f, -0.01f), lineRadius);
            lines.Capsule(new Vector2(-0.12f, 0.01f), new Vector2(0.12f, 0.03f), lineRadius);
            for (var j = 0; j < 5; j++)
            {
                var sx = head.x + (j - 2) * 0.03f;
                lines.CurveStroke(new Vector2(sx, head.y - 0.07f), new Vector2(sx + 0.02f, head.y), new Vector2(sx + (j - 2) * 0.01f, head.y + 0.07f), lineRadius, 6);
            }

            // 塗る: 体の中はうすいグラデーション、外側に濃い輪郭。
            var w = SpiritWidthPx;
            var h = SpiritHeightPx;
            var pixels = new Color[w * h];
            var coverage = new float[w * h];
            var lineColor = palette.spiritLines;
            for (var y = 0; y < h; y++)
            {
                // y はデモと同じ上から。守護霊の上（-0.55）→ 下（+0.5）でグラデーション。
                var unitY = (y + 0.5f - SpiritOriginFromTop) / scale;
                var g = Mathf.Clamp01((unitY + 0.55f) / 1.05f);
                var fill = g < 0.5f
                    ? Color.Lerp(palette.spiritTop, palette.spiritMiddle, g * 2f)
                    : Color.Lerp(palette.spiritMiddle, palette.spiritBottom, (g - 0.5f) * 2f);
                var textureRow = (h - 1 - y) * w;
                for (var x = 0; x < w; x++)
                {
                    var d = shape.DistanceAt(x, y);
                    var inside = Mathf.Clamp01(0.5f - d);
                    var dilated = Mathf.Clamp01(0.5f - (d - outlinePx));
                    coverage[textureRow + x] = dilated;
                    if (dilated <= 0f)
                    {
                        continue;
                    }

                    var rgb = fill;
                    var line = Mathf.Clamp01(0.5f - lines.DistanceAt(x, y)) * lineColor.a;
                    rgb = Color.Lerp(rgb, lineColor, line);
                    var outline = dilated - inside;
                    var fillAlpha = inside * fillOpacity;
                    var alpha = fillAlpha + outline;
                    var c = alpha > 1e-4f ? (rgb * fillAlpha + palette.spiritOutline * outline) / alpha : rgb;
                    c.a = Mathf.Clamp01(alpha);
                    pixels[textureRow + x] = c;
                }
            }

            body = MakeTexture(w, h, pixels, "GreenSpirit");
            halo = PaintHalo(coverage, w, h, palette.spiritGlow);
        }

        /// <summary>腕1本: 肩から手まで曲がった線＋袖（先が広い）＋手（指4本と親指）。</summary>
        private void PaintArm(GreenSdfCanvas shape, GreenSdfCanvas lines, float side, Vector2 hand)
        {
            var shoulder = new Vector2(side * 0.11f, -0.27f);
            var mid = (shoulder + hand) * 0.5f;
            var delta = hand - shoulder;
            var length = Mathf.Max(delta.magnitude, 1e-4f);
            var normal = new Vector2(-delta.y / length, delta.x / length);
            if (normal.x * side < 0f)
            {
                normal = -normal;
            }

            var elbow = mid + normal * 0.06f + new Vector2(0f, 0.02f);
            shape.CurveStroke(shoulder, elbow, hand, 0.03f, 12);

            var a = GreenSdfCanvas.Quadratic(shoulder, elbow, hand, 0.55f);
            var b = GreenSdfCanvas.Quadratic(shoulder, elbow, hand, 0.86f);
            var tangent = (b - a).normalized;
            var across = new Vector2(-tangent.y, tangent.x);
            const float wa = 0.035f;
            const float wb = 0.085f;
            _path.Clear();
            _path.Add(a + across * wa);
            _path.Add(b + across * wb);
            GreenSdfCanvas.AddQuadratic(_path, b + across * wb, b + tangent * 0.03f, b - across * wb, 6);
            _path.Add(a - across * wa);
            shape.Polygon(_path);

            var angle = Mathf.Atan2(hand.y - elbow.y, hand.x - elbow.x);
            shape.Circle(hand, 0.032f);
            for (var f = 0; f < 4; f++)
            {
                var fa = angle - 0.55f + f * 0.37f;
                shape.Ellipse(hand + new Vector2(Mathf.Cos(fa), Mathf.Sin(fa)) * 0.035f, 0.034f, 0.012f, fa);
            }

            var thumb = angle - side * 1.15f;
            shape.Ellipse(hand + new Vector2(Mathf.Cos(thumb), Mathf.Sin(thumb)) * 0.03f, 0.025f, 0.012f, thumb);

            // 袖口の線。
            var lineRadius = Mathf.Max(0.5f, 0.003f * SpiritPixelsPerHeight) / SpiritPixelsPerHeight;
            lines.Capsule(b + across * wb * 0.9f, b - across * wb * 0.9f, lineRadius);
        }

        /// <summary>まわりの光: 形を 1/4 に縮めてぼかしたもの（ふちほど明るい）。</summary>
        private Texture2D PaintHalo(float[] coverage, int width, int height, Color glow)
        {
            var w = width / HaloDownsample;
            var h = height / HaloDownsample;
            var small = new float[w * h];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var sum = 0f;
                    for (var dy = 0; dy < HaloDownsample; dy++)
                    {
                        var row = (y * HaloDownsample + dy) * width + x * HaloDownsample;
                        for (var dx = 0; dx < HaloDownsample; dx++)
                        {
                            sum += coverage[row + dx];
                        }
                    }

                    small[y * w + x] = sum / (HaloDownsample * HaloDownsample);
                }
            }

            var blurred = Blur(small, w, h, 4.5f);
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                // 形の中ほど消して（体の中は体の色に任せる）、ふちのまわりだけ光らせる。
                var a = Mathf.Clamp01(blurred[i] * 1.7f) * (1f - small[i] * 0.55f);
                pixels[i] = new Color(glow.r, glow.g, glow.b, a * glow.a);
            }

            return MakeTexture(w, h, pixels, "GreenSpiritHalo");
        }

        private static float[] Blur(float[] source, int width, int height, float sigma)
        {
            var radius = Mathf.CeilToInt(sigma * 2.5f);
            var kernel = new float[radius * 2 + 1];
            var total = 0f;
            for (var i = -radius; i <= radius; i++)
            {
                kernel[i + radius] = Mathf.Exp(-i * i / (2f * sigma * sigma));
                total += kernel[i + radius];
            }

            for (var i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= total;
            }

            var temp = new float[source.Length];
            var result = new float[source.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var sx = Mathf.Clamp(x + k, 0, width - 1);
                        sum += source[y * width + sx] * kernel[k + radius];
                    }

                    temp[y * width + x] = sum;
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var sy = Mathf.Clamp(y + k, 0, height - 1);
                        sum += temp[sy * width + x] * kernel[k + radius];
                    }

                    result[y * width + x] = sum;
                }
            }

            return result;
        }

        // ───────────── 葉っぱ ─────────────

        // 葉の絵の単位: l = 葉の半分の長さ。軸（-1.2l）から先（l）まで。
        private const float LeafUnitPx = 100f;
        private const float LeafLeft = 1.25f;
        private const float LeafRight = 1.05f;
        private const float LeafHalfHeight = 0.82f;
        private const int LeafWidthPx = (int)((LeafLeft + LeafRight) * LeafUnitPx);
        private const float LeafPivotX = LeafLeft / (LeafLeft + LeafRight);

        /// <summary>ギザギザのふちのミントの葉（先が右）。明るい側→濃い側のグラデーション、濃いふち、白っぽい葉脈。</summary>
        private Texture2D PaintLeaf(GreenGuardianPalette palette)
        {
            var w = LeafWidthPx;
            var h = (int)(LeafHalfHeight * 2f * LeafUnitPx);
            var shape = new GreenSdfCanvas(w, h, LeafUnitPx, LeafLeft * LeafUnitPx, h * 0.5f, 6);
            var veins = new GreenSdfCanvas(w, h, LeafUnitPx, LeafLeft * LeafUnitPx, h * 0.5f, 3);
            const float l = 1f;
            const float halfWidth = 0.24f / 0.55f * l;
            _path.Clear();
            _path.Add(new Vector2(l, 0f));
            for (var j = 1; j <= 12; j++)
            {
                var q = j / 12f;
                _path.Add(new Vector2(Mathf.Lerp(l, -l * 0.95f, q), -Mathf.Pow(Mathf.Sin(q * Mathf.PI), 0.7f) * halfWidth * (j % 2 == 1 ? 1.75f : 1.5f)));
            }

            for (var j = 11; j >= 1; j--)
            {
                var q = j / 12f;
                _path.Add(new Vector2(Mathf.Lerp(l, -l * 0.95f, q), Mathf.Pow(Mathf.Sin(q * Mathf.PI), 0.7f) * halfWidth * (j % 2 == 1 ? 1.75f : 1.5f)));
            }

            shape.Polygon(_path);

            // 葉の大きさ s = 1.45 のとき、ふちの線 0.05m・葉脈 0.045m（l = 0.55s）。
            var edgeHalf = 0.05f / (0.55f * 1.45f) * 0.5f * LeafUnitPx;
            var veinRadius = 0.045f / (0.55f * 1.45f) * 0.5f;
            veins.Capsule(new Vector2(-l * 1.2f, 0f), new Vector2(l * 0.85f, 0f), veinRadius);
            for (var j = 0; j < 3; j++)
            {
                var vx = Mathf.Lerp(-l * 0.55f, l * 0.35f, j / 2f);
                veins.Capsule(new Vector2(vx, 0f), new Vector2(vx + l * 0.28f, -halfWidth * 1.05f), veinRadius);
                veins.Capsule(new Vector2(vx, 0f), new Vector2(vx + l * 0.28f, halfWidth * 1.05f), veinRadius);
            }

            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            {
                var g = Mathf.Clamp01((y + 0.5f) / h);
                var fill = Color.Lerp(palette.leafLight, palette.leafDark, g);
                var row = (h - 1 - y) * w;
                for (var x = 0; x < w; x++)
                {
                    var d = shape.DistanceAt(x, y);
                    var inside = Mathf.Clamp01(0.5f - d);
                    var edge = Mathf.Clamp01(0.5f - (Mathf.Abs(d) - edgeHalf));
                    var vein = Mathf.Clamp01(0.5f - veins.DistanceAt(x, y)) * palette.leafVein.a;
                    var alpha = Mathf.Max(Mathf.Max(inside, edge), vein);
                    if (alpha <= 0f)
                    {
                        continue;
                    }

                    var c = Color.Lerp(fill, palette.leafEdge, edge);
                    c = Color.Lerp(c, palette.leafVein, vein);
                    c.a = alpha;
                    pixels[row + x] = c;
                }
            }

            return MakeTexture(w, h, pixels, "GreenLeaf");
        }

        // ───────────── チューブ ─────────────

        private const float TubePixelsPerMeter = 100f;
        private const float TubeLeftMeters = 1.2f;
        private const float TubeWidthMeters = 2.36f;
        private const float TubeHalfHeightMeters = 0.52f;
        private const float TubePivotX = TubeLeftMeters / TubeWidthMeters;

        /// <summary>歯みがき粉のチューブ（口が右）。白い本体、緑の帯、うすい緑の線、口、しぼった端。</summary>
        private Texture2D PaintTube(GreenGuardianPalette palette)
        {
            var w = (int)(TubeWidthMeters * TubePixelsPerMeter);
            var h = (int)(TubeHalfHeightMeters * 2f * TubePixelsPerMeter);
            var origin = new Vector2(TubeLeftMeters * TubePixelsPerMeter, h * 0.5f);
            var body = new GreenSdfCanvas(w, h, TubePixelsPerMeter, origin.x, origin.y, 6);
            var crimp = new GreenSdfCanvas(w, h, TubePixelsPerMeter, origin.x, origin.y, 6);
            var nozzle = new GreenSdfCanvas(w, h, TubePixelsPerMeter, origin.x, origin.y, 3);
            _path.Clear();
            _path.Add(new Vector2(-1f, -0.42f));
            _path.Add(new Vector2(0.55f, -0.36f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(0.55f, -0.36f), new Vector2(0.8f, -0.32f), new Vector2(0.85f, -0.16f), 6);
            _path.Add(new Vector2(1.08f, -0.14f));
            _path.Add(new Vector2(1.08f, 0.14f));
            _path.Add(new Vector2(0.85f, 0.16f));
            GreenSdfCanvas.AddQuadratic(_path, new Vector2(0.85f, 0.16f), new Vector2(0.8f, 0.32f), new Vector2(0.55f, 0.36f), 6);
            _path.Add(new Vector2(-1f, 0.42f));
            body.Polygon(_path);
            _path.Clear();
            _path.Add(new Vector2(-1.14f, -0.47f));
            _path.Add(new Vector2(-0.96f, -0.47f));
            _path.Add(new Vector2(-0.96f, 0.47f));
            _path.Add(new Vector2(-1.14f, 0.47f));
            crimp.Polygon(_path);
            nozzle.Ellipse(new Vector2(1.08f, 0f), 0.05f, 0.13f, 0f);

            var strokeHalf = 0.03f * TubePixelsPerMeter;
            var mint = Color.Lerp(palette.tubeGreen, Color.white, 0.65f);
            var nozzleColor = Color.Lerp(palette.tubeGreen, Color.white, 0.75f);
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            {
                var my = (y + 0.5f - origin.y) / TubePixelsPerMeter;
                var row = (h - 1 - y) * w;
                for (var x = 0; x < w; x++)
                {
                    var mx = (x + 0.5f - origin.x) / TubePixelsPerMeter;
                    var c = Color.clear;
                    var d = body.DistanceAt(x, y);
                    var inside = Mathf.Clamp01(0.5f - d);
                    if (inside > 0f)
                    {
                        var fill = palette.tubeBody;
                        if (mx > -0.55f && mx < -0.1f)
                        {
                            fill = palette.tubeGreen;
                        }
                        else if (my > 0.18f && my < 0.28f && mx < 0.6f)
                        {
                            fill = mint;
                        }

                        c = Over(c, fill, inside);
                    }

                    c = Over(c, palette.tubeOutline, Mathf.Clamp01(0.5f - (Mathf.Abs(d) - strokeHalf)));
                    var dc = crimp.DistanceAt(x, y);
                    c = Over(c, palette.tubeGreen, Mathf.Clamp01(0.5f - dc));
                    c = Over(c, palette.tubeOutline, Mathf.Clamp01(0.5f - (Mathf.Abs(dc) - strokeHalf)));
                    c = Over(c, nozzleColor, Mathf.Clamp01(0.5f - nozzle.DistanceAt(x, y)));
                    pixels[row + x] = c;
                }
            }

            return MakeTexture(w, h, pixels, "GreenTube");
        }

        /// <summary>色 c の上に、色 top を濃さ amount で重ねる（ふつうの重ね塗り）。</summary>
        private static Color Over(Color c, Color top, float amount)
        {
            if (amount <= 0f)
            {
                return c;
            }

            var a = amount + c.a * (1f - amount);
            var rgb = a > 1e-4f ? ((Color)(top * amount) + c * c.a * (1f - amount)) / a : top;
            rgb.a = a;
            return rgb;
        }

        // ───────────── 小さな絵 ─────────────

        private Texture2D PaintSoftDot(int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                var a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>中がうすい色、ふちが少し濃い丸（粉・煙）。</summary>
        private Texture2D PaintRimmedDot(int size, Color fill, Color rim, float rimWidth)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                var inside = Edge(0.94f - r, px * 2f);
                if (inside <= 0f)
                {
                    return Color.clear;
                }

                var edge = Edge(r - (0.94f - rimWidth), px * 2f);
                var c = Color.Lerp(fill, rim, edge);
                c.a = Mathf.Lerp(fill.a, rim.a, edge) * inside;
                return c;
            });
        }

        private Texture2D PaintPowder(GreenGuardianPalette palette, int size)
        {
            var rim = Color.Lerp(palette.leafDark, palette.powder, 0.35f);
            rim.a = 0.7f;
            return PaintRimmedDot(size, palette.powder, rim, 0.12f);
        }

        private Texture2D PaintSmoke(GreenGuardianPalette palette, int size)
        {
            // 煙のふちは見本どおりうすい緑。重なっても濃くなりすぎないよう、中をさらにうすく。
            var fill = palette.smoke;
            var rim = palette.smokeEdge;
            rim.a = Mathf.Max(rim.a, fill.a * 0.9f);
            return PaintRimmedDot(size, fill, rim, 0.08f);
        }

        /// <summary>回復の十字（中は色、まわりは白いふち）。</summary>
        private Texture2D PaintCross(GreenGuardianPalette palette, int size)
        {
            return Paint(size, size, (x, y, px) =>
            {
                var p = new Vector2(x * 2f - 1f, y * 2f - 1f);
                var sdf = Mathf.Min(RoundBox(p, new Vector2(0.78f, 0.28f), 0.08f), RoundBox(p, new Vector2(0.28f, 0.78f), 0.08f));
                var alpha = Edge(0.12f - sdf, px * 2f);
                if (alpha <= 0f)
                {
                    return Color.clear;
                }

                var inner = Edge(-sdf, px * 2f);
                var c = Color.Lerp(Color.white, palette.cross, inner);
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

        /// <summary>白い弧（斬ったような線。真ん中が太く、両端が細い）。</summary>
        private Texture2D PaintSlash(int width, int height)
        {
            return Paint(width, height, (x, y, px) =>
            {
                var u = x * 2f - 1f;
                // 弧: 両端が上、まん中が下（見本の quadraticCurveTo と同じ向き）。
                var curveY = 0.62f - 0.5f * (1f - u * u);
                var thickness = 0.16f * (1f - u * u) + 0.02f;
                var d = Mathf.Abs(y - curveY) - thickness;
                var a = Edge(-d, 2f / height) * Edge(1f - Mathf.Abs(u), px * 4f);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>光の柱（横はやわらかく、上ほど消える）。</summary>
        private Texture2D PaintPillar(int width, int height)
        {
            return Paint(width, height, (x, y, px) =>
            {
                var across = Mathf.Exp(-Mathf.Pow((x - 0.5f) / 0.22f, 2f));
                var core = Mathf.Exp(-Mathf.Pow((x - 0.5f) / 0.07f, 2f)) * 0.5f;
                var along = Mathf.SmoothStep(0f, 0.04f, y) * (1f - Mathf.SmoothStep(0.7f, 1f, y));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(across + core) * along);
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

        /// <summary>
        /// 1ピクセルずつ色を決めて絵を作る。paint には 0〜1 の座標（ピクセルの真ん中。y は下が 0）と、1ピクセルの大きさ（横幅を 1 とした単位）を渡す。
        /// </summary>
        private Texture2D Paint(int width, int height, Func<float, float, float, Color> paint)
        {
            var pixels = new Color[width * height];
            var px = 1f / width;
            for (var y = 0; y < height; y++)
            {
                var v = (y + 0.5f) / height;
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = paint((x + 0.5f) / width, v, px);
                }
            }

            return MakeTexture(width, height, pixels, "GreenSkillArt");
        }

        private Texture2D MakeTexture(int width, int height, Color[] pixels, string textureName)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                c.a = Mathf.Clamp01(c.a);
                pixels[i] = c;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
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
