using System.IO;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// キャラ選択の画面の仮の絵（PNG）。無いときだけ作るので、同じ名前のファイルを本番の絵で上書きすれば、その絵に替わる。
    /// 白で描いてあり、画面の側で色を付けて使う（札・板・丸はプレイヤーの色、仮のキャラのキューブはキャラの色）。
    /// </summary>
    public static class CharacterSelectArt
    {
        public const string Folder = CharacterSelectSceneBuilder.Root + "/Art";
        /// <summary>背景（電脳オフィスの仮の絵: 明るい窓の壁・床の方眼・うっすら2進数・グリッチの帯）。会社のオフィスの絵で上書きする。</summary>
        public const string Background = Folder + "/CS_Background_Cyber.png";
        public const string Circle = Folder + "/CS_Circle.png";
        public const string Ring = Folder + "/CS_Ring.png";
        public const string Pill = Folder + "/CS_Pill.png";
        public const string Check = Folder + "/CS_Check.png";

        /// <summary>区画のドット柄（右上ほど大きい水玉。白）。</summary>
        public const string Halftone = Folder + "/CS_Halftone.png";

        /// <summary>区画を流れる斜めのしま（繰り返して使う。白）。</summary>
        public const string Stripes = Folder + "/CS_Stripes.png";

        /// <summary>歯磨き粉の泡（縁と光のある丸）。</summary>
        public const string Bubble = Folder + "/CS_Bubble.png";

        /// <summary>キラキラ（4つの角の星）。</summary>
        public const string Sparkle = Folder + "/CS_Sparkle.png";

        /// <summary>区画の白い枠（角の丸い線。9分割）。</summary>
        public const string Frame = Folder + "/CS_Frame.png";

        /// <summary>歯（ぷっくりした白い歯。顔なし。弾ける演出・はんこ・GO! に使う）。</summary>
        public const string Tooth = Folder + "/CS_Tooth.png";

        /// <summary>キャラ名の札（斜めの平行四辺形。左右だけ伸ばせる9分割）。</summary>
        public const string NameTag = Folder + "/CS_NameTag.png";

        /// <summary>説明の板（角の丸い四角。9分割）。</summary>
        public const string Panel = Folder + "/CS_Panel.png";

        /// <summary>棒グラフ・ゲージの棒（角の丸い横長。Filled で伸び縮みさせる）。</summary>
        public const string Bar = Folder + "/CS_Bar.png";

        /// <summary>仮のキャラ（斜め上から見た白いキューブ。キャラのファイルの「立ち絵にかける色」でキャラの色になる）。</summary>
        public const string CharaCube = Folder + "/CS_CharaCube.png";

        private const int PillBorder = 30;
        private const int PanelBorder = 24;
        private const int NameTagBorder = 30;
        private const int FrameBorder = 18;
        private static readonly Color Line = new Color(0.13f, 0.13f, 0.18f);

        public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>仮の絵1枚（置き場所・何に使うか・描き方）。調整の画面の「画像の差し替え」にも、この一覧の順で並ぶ。</summary>
        public sealed class Entry
        {
            public string path;
            public string label;
            public string usage;
            public int width;
            public int height;
            internal PixelFunc paint;
        }

        /// <summary>仮の絵の一覧。</summary>
        public static readonly Entry[] Entries =
        {
            new Entry { path = Background, label = "背景", usage = "画面いっぱいの背景（今は電脳オフィスの仮の絵。会社のオフィスの絵にする）。1920×1080 がおすすめ。", width = 1920, height = 1080, paint = BackgroundColor },
            new Entry { path = CharaCube, label = "仮のキャラ（キューブ）", usage = "キャラの絵が無いときの仮の立ち絵。白で描き、キャラごとの色をかけて使う。本番はキャラのファイルの「立ち絵」に絵を入れる。", width = 300, height = 300, paint = PaintCube },
            new Entry { path = Halftone, label = "区画のドット柄", usage = "4つの区画の水玉模様。白で描き、プレイヤーの色をかけて使う。", width = 512, height = 256, paint = PaintHalftone },
            new Entry { path = Stripes, label = "区画の流れるしま", usage = "区画をゆっくり流れる斜めのしま（繰り返して使う）。白。", width = 128, height = 128, paint = (x, y, w, h) => White(Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(x + y, 32f) - 8f) / 8f * 2f + 0.5f) * 0.6f) },
            new Entry { path = Frame, label = "区画の枠", usage = "区画のふちの白い線（9分割）。", width = 64, height = 64, paint = (x, y, w, h) => White(Coverage(Mathf.Abs(RoundedRectDistance(x - 32f, y - 32f, 30f, 30f, 14f) + 3f) - 3f)) },
            new Entry { path = NameTag, label = "キャラ名の札", usage = "キャラ名の後ろの斜めの札（プレイヤーの色をかける。高さ 64 のまま）。", width = 256, height = 64, paint = (x, y, w, h) => White(Polygon(x, y, new[] { new Vector2(1f, 1f), new Vector2(234f, 1f), new Vector2(255f, 63f), new Vector2(22f, 63f) })) },
            new Entry { path = Panel, label = "説明の板", usage = "能力・スキルの板と、スキルの枠（9分割）。", width = 96, height = 96, paint = (x, y, w, h) => White(RoundedRect(x, y, w, h, 18f)) },
            new Entry { path = Bar, label = "棒グラフ・ゲージ", usage = "能力の棒グラフと、長押しのゲージ。", width = 128, height = 16, paint = (x, y, w, h) => White(RoundedRect(x, y, w, h, 7f)) },
            new Entry { path = Pill, label = "丸い札", usage = "「1P」の札・READY! のはんこ・GO! の板・プレイヤー名の帯（9分割）。", width = 128, height = 72, paint = (x, y, w, h) => White(RoundedRect(x, y, w, h, 32f)) },
            new Entry { path = Ring, label = "丸の縁", usage = "下の丸の縁と、長押しのゲージ（丸）。", width = 256, height = 256, paint = (x, y, w, h) => White(Coverage(Mathf.Abs(Dist(x, y, 128f, 128f) - 114f) - 10f)) },
            new Entry { path = Circle, label = "丸", usage = "下の丸の中、絵が無いキャラの丸。", width = 256, height = 256, paint = (x, y, w, h) => White(Coverage(Dist(x, y, 128f, 128f) - 124f)) },
            new Entry { path = Check, label = "チェック", usage = "READY のときに「1P」の札に付く印。", width = 128, height = 128, paint = PaintCheck },
            new Entry { path = Tooth, label = "歯", usage = "READY! のはんこ・GO! の両側・決めたときに弾ける歯。", width = 96, height = 96, paint = PaintTooth },
            new Entry { path = Bubble, label = "泡", usage = "漂う泡・弾ける泡・塗り替えの泡。", width = 64, height = 64, paint = PaintBubble },
            new Entry { path = Sparkle, label = "キラキラ", usage = "またたく星・弾けるキラキラ・塗り替えのキラーン。", width = 64, height = 64, paint = PaintSparkle },
        };

        /// <summary>無い絵だけ作る。戻り値は作った数。</summary>
        public static int EnsureAll()
        {
            StudioAssets.EnsureFolder(Folder);
            var made = new System.Collections.Generic.List<string>();
            foreach (var entry in Entries)
            {
                if (!File.Exists(entry.path))
                {
                    Write(entry);
                    made.Add(entry.path);
                }
            }

            if (made.Count == 0)
            {
                return 0;
            }

            AssetDatabase.Refresh();
            // 今回作った絵だけ画像の設定をする（差し替えた絵の設定は変えない）。
            foreach (var path in made)
            {
                Configure(path, BorderOf(path), path == Stripes ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
            }

            return made.Count;
        }

        /// <summary>その絵を、仮の絵で描き直す（ファイルは同じなので、使っている所はそのまま）。</summary>
        public static void RestorePlaceholder(Entry entry)
        {
            StudioAssets.EnsureFolder(Folder);
            var existed = File.Exists(entry.path);
            Write(entry);
            AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
            if (!existed)
            {
                Configure(entry.path, BorderOf(entry.path), entry.path == Stripes ? TextureWrapMode.Repeat : TextureWrapMode.Clamp);
            }
        }

        /// <summary>
        /// その絵を、選んだ画像ファイル（PNG / JPG）で上書きする。ファイルは同じなので、使っている所は全部その絵になる。
        /// 9分割の枠などの画像の設定はそのまま。戻り値 = 上書きできたか。
        /// </summary>
        public static bool ReplaceWithFile(Entry entry, string sourcePath)
        {
            var bytes = File.ReadAllBytes(sourcePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes))
                {
                    return false;
                }

                File.WriteAllBytes(entry.path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(entry.path, ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static Vector4 BorderOf(string path)
        {
            switch (path)
            {
                case Pill:
                    return new Vector4(PillBorder, PillBorder, PillBorder, PillBorder);
                case Panel:
                    return new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder);
                case Frame:
                    return new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder);
                case NameTag:
                    // 左右の斜めの部分だけ伸ばさない（高さは絵のまま 64 で使う）。
                    return new Vector4(NameTagBorder, 0f, NameTagBorder, 0f);
                default:
                    return Vector4.zero;
            }
        }

        // ── 描く ─────────────────

        internal delegate Color PixelFunc(float x, float y, float w, float h);

        private static void Write(Entry entry)
        {
            var texture = new Texture2D(entry.width, entry.height, TextureFormat.RGBA32, false);
            var pixels = new Color32[entry.width * entry.height];
            for (var y = 0; y < entry.height; y++)
            {
                for (var x = 0; x < entry.width; x++)
                {
                    pixels[y * entry.width + x] = entry.paint(x + 0.5f, y + 0.5f, entry.width, entry.height);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(entry.path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        /// <summary>画像の設定（スプライト・9分割の枠）にする。</summary>
        private static void Configure(string path, Vector4 border, TextureWrapMode wrap)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        // 背景（電脳オフィスの仮の絵）: 上はラベンダー、下は水色のパステル。
        // 上の方は大きな明るい窓の壁（窓枠・斜めの光の反射）、下は奥へすぼまる床の方眼。
        // 窓にはうっすら 0 と 1（2進数）、ところどころに色ずれのグリッチの帯、細い走査線。
        private static Color BackgroundColor(float x, float y, float w, float h)
        {
            var t = y / h;
            var top = new Color(0.82f, 0.8f, 1f);
            var bottom = new Color(0.78f, 0.93f, 1f);
            var color = Color.Lerp(bottom, top, t);

            const float floorTop = 0.34f;
            if (t > floorTop)
            {
                // 窓の壁: 幅 240 の窓が並ぶ。窓枠は白っぽく、窓は上ほど明るい。
                var u = (t - floorTop) / (1f - floorTop);
                var col = Mathf.Repeat(x + 60f, 240f);
                var row = Mathf.Repeat(y - h * floorTop, 230f);
                var mullion = Mathf.Min(col, 240f - col) < 7f || Mathf.Min(row, 230f - row) < 6f;
                var pane = Color.Lerp(new Color(0.9f, 0.95f, 1f), new Color(1f, 1f, 1f), u);
                color = Color.Lerp(color, mullion ? new Color(0.96f, 0.97f, 1f) : pane, mullion ? 0.85f : 0.55f);

                // 窓に映る斜めの光の反射。
                var streak = Mathf.Repeat(x * 0.7f + y, 520f);
                color = Color.Lerp(color, Color.white, Smooth(1f - Mathf.Abs(streak - 120f) / 50f) * 0.35f);

                // うっすら 0 と 1。
                color = Color.Lerp(color, new Color(0.35f, 0.75f, 1f), BinaryDigits(x, y) * 0.22f);
            }
            else
            {
                // 床: 奥（上）へすぼまる方眼と、つやの帯。
                var depth = 1f - t / floorTop;
                var lineX = Mathf.Abs(Mathf.Repeat((x - w * 0.5f) / (0.45f + depth * 1.6f) / 110f, 1f) - 0.5f);
                var lineY = Mathf.Abs(Mathf.Repeat(Mathf.Pow(depth, 1.7f) * 8f, 1f) - 0.5f);
                var grid = Mathf.Clamp01(1f - Mathf.Min(0.5f - lineX, 0.5f - lineY) * 26f);
                color = Color.Lerp(color, new Color(0.45f, 0.62f, 1f), grid * (0.12f + depth * 0.15f));
                color = Color.Lerp(color, Color.white, Smooth(1f - Mathf.Abs(t - floorTop * 0.75f) / 0.04f) * 0.35f);
            }

            // 窓と床の境目の明るい線。
            color = Color.Lerp(color, Color.white, Smooth(1f - Mathf.Abs(t - floorTop) / 0.006f) * 0.8f);

            // ぼんやりした光。
            var glow = Blob(x, y, w * 0.2f, h * 0.82f, 460f) + Blob(x, y, w * 0.8f, h * 0.7f, 420f) * 0.8f;
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(glow) * 0.35f);

            // グリッチの帯（マゼンタと水色にずれた横の帯）。
            var band = GlitchBand(y);
            if (band > 0f)
            {
                var shift = Mathf.Repeat(x * 0.013f + y, 2f) < 1f ? new Color(1f, 0.55f, 0.9f) : new Color(0.45f, 0.95f, 1f);
                color = Color.Lerp(color, shift, band * 0.35f);
            }

            // 細い走査線。
            color *= Mathf.Repeat(y, 4f) < 1f ? 0.985f : 1f;
            color.a = 1f;
            return color;
        }

        private static float Smooth(float v)
        {
            v = Mathf.Clamp01(v);
            return v * v * (3f - 2f * v);
        }

        private static float GlitchBand(float y)
        {
            // 決まった高さに数本だけ（毎回同じ絵になるように乱数は使わない）。
            var bands = new[] { (980f, 10f), (742f, 5f), (604f, 14f), (455f, 6f), (180f, 8f) };
            var k = 0f;
            foreach (var (center, half) in bands)
            {
                k = Mathf.Max(k, Mathf.Clamp01(1f - Mathf.Abs(y - center) / half));
            }

            return k;
        }

        // 5×7 の点で描いた 0 と 1 を、窓の上にまばらに並べる（濃さ 0〜1 を返す）。
        private static readonly string[] Zero = { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." };
        private static readonly string[] One = { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." };

        private static float BinaryDigits(float x, float y)
        {
            const float cellW = 26f;
            const float cellH = 40f;
            var cx = Mathf.FloorToInt(x / cellW);
            var cy = Mathf.FloorToInt(y / cellH);
            var hash = Hash(cx, cy);
            if (hash % 7 != 0)
            {
                return 0f;
            }

            var glyph = hash % 2 == 0 ? Zero : One;
            var px = Mathf.FloorToInt((x - cx * cellW - 5f) / 3f);
            var py = 6 - Mathf.FloorToInt((y - cy * cellH - 8f) / 3f);
            if (px < 0 || px > 4 || py < 0 || py > 6)
            {
                return 0f;
            }

            return glyph[py][px] == '#' ? 0.4f + (hash % 5) * 0.15f : 0f;
        }

        private static int Hash(int x, int y)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) & 0x7fffffff;
            }
        }

        // ドット柄: 18px ごとの水玉。右上ほど大きい（参考の対戦ゲームの選択画面の網点）。
        private static Color PaintHalftone(float x, float y, float w, float h)
        {
            const float cell = 18f;
            var ox = (Mathf.Floor(x / cell) + 0.5f) * cell;
            var oy = (Mathf.Floor(y / cell) + 0.5f) * cell;
            var t = Mathf.Clamp01(ox / w * 0.75f + oy / h * 0.25f);
            var radius = Mathf.Lerp(0.5f, cell * 0.46f, t * t);
            return White(Coverage(Dist(x, y, ox, oy) - radius));
        }

        // 泡: 縁の濃い丸、中はうっすら、左上に光。
        private static Color PaintBubble(float x, float y, float w, float h)
        {
            var d = Dist(x, y, 32f, 32f);
            var inside = Coverage(d - 30f);
            var rim = Coverage(Mathf.Abs(d - 28.5f) - 2f);
            var highlight = Coverage(Dist(x, y, 22f, 43f) - 6f);
            return White(Mathf.Max(inside * 0.18f, Mathf.Max(rim * 0.9f, highlight)));
        }

        // キラキラ: 縦長・横長のひし形を重ねた4つの角の星＋真ん中の光。
        private static Color PaintSparkle(float x, float y, float w, float h)
        {
            var dx = Mathf.Abs(x - 32f);
            var dy = Mathf.Abs(y - 32f);
            var vertical = Coverage((dx / 5f + dy / 30f - 1f) * 5f);
            var horizontal = Coverage((dx / 30f + dy / 5f - 1f) * 5f);
            var glow = Mathf.Clamp01(1f - Dist(x, y, 32f, 32f) / 14f);
            return White(Mathf.Max(Mathf.Max(vertical, horizontal), glow * glow));
        }

        // 仮のキャラ: 斜め上から見たキューブ。上の面がいちばん明るく、左・右の面は少しずつ暗い。縁は黒。
        // 色をかける（掛け算）と、明るさの差が残ったままキャラの色のキューブになる。
        private static Color PaintCube(float x, float y, float w, float h)
        {
            var top = new Vector2(150f, 292f);
            var upperLeft = new Vector2(26f, 220f);
            var upperRight = new Vector2(274f, 220f);
            var center = new Vector2(150f, 148f);
            var lowerLeft = new Vector2(26f, 78f);
            var lowerRight = new Vector2(274f, 78f);
            var bottom = new Vector2(150f, 6f);

            var alpha = Polygon(x, y, new[] { top, upperRight, lowerRight, bottom, lowerLeft, upperLeft });
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            float shade;
            if (Inside(x, y, new[] { top, upperRight, center, upperLeft }))
            {
                shade = 1f;
            }
            else if (Inside(x, y, new[] { upperLeft, center, bottom, lowerLeft }))
            {
                shade = 0.78f;
            }
            else
            {
                shade = 0.58f;
            }

            var edges = new[]
            {
                (top, upperRight), (upperRight, lowerRight), (lowerRight, bottom), (bottom, lowerLeft), (lowerLeft, upperLeft), (upperLeft, top),
                (upperLeft, center), (upperRight, center), (center, bottom)
            };
            var line = 0f;
            foreach (var (a, b) in edges)
            {
                line = Mathf.Max(line, Stroke(x, y, a, b, 3f));
            }

            var color = Color.Lerp(new Color(shade, shade, shade), Line, line);
            color.a = alpha;
            return color;
        }

        /// <summary>中心からの位置 (px, py) の、角の丸い四角（半分の幅・高さ・角の半径）までの距離（中は負）。</summary>
        private static float RoundedRectDistance(float px, float py, float halfW, float halfH, float r)
        {
            var qx = Mathf.Abs(px) - (halfW - r);
            var qy = Mathf.Abs(py) - (halfH - r);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        // 歯: ぷっくりした歯（上は2つのふくらみ、下は2本の根）。白に黒い縁、左上につや。顔は付けない。
        private static Color PaintTooth(float x, float y, float w, float h)
        {
            var crown = RoundedRectDistance(x - 48f, y - 54f, 34f, 24f, 18f);
            var bumps = Mathf.Min(Dist(x, y, 32f, 70f) - 17f, Dist(x, y, 64f, 70f) - 17f);
            var roots = Mathf.Min(RoundedRectDistance(x - 32f, y - 28f, 12f, 20f, 11f), RoundedRectDistance(x - 64f, y - 28f, 12f, 20f, 11f));
            var notch = -(Dist(x, y, 48f, 14f) - 10f);
            var tooth = Mathf.Max(Mathf.Min(Mathf.Min(crown, bumps), roots), notch);
            var color = Outlined(Color.white, tooth, 4f);
            if (color.a <= 0f)
            {
                return color;
            }

            // 光（左上のつや）
            color = Over(color, new Color(0.7f, 0.92f, 1f, Stroke(x, y, new Vector2(26f, 66f), new Vector2(33f, 77f), 3.5f)));
            return color;
        }

        /// <summary>色 fill の形（d = 形の外までの距離。中は負）に黒い縁を付けた色。</summary>
        private static Color Outlined(Color fill, float d, float outline)
        {
            var alpha = Coverage(d);
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            var color = Color.Lerp(Line, fill, Coverage(d + outline));
            color.a = alpha;
            return color;
        }

        /// <summary>top を bottom の上に重ねる。</summary>
        private static Color Over(Color bottom, Color top)
        {
            var a = top.a + bottom.a * (1f - top.a);
            if (a <= 0f)
            {
                return Color.clear;
            }

            var rgb = ((Vector4)top * top.a + (Vector4)bottom * bottom.a * (1f - top.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        private static Color PaintCheck(float x, float y, float w, float h)
        {
            // 丸の中にチェック（チェックの形は抜く）。
            var circle = Coverage(Dist(x, y, 64f, 64f) - 60f);
            var check = Mathf.Max(Stroke(x, y, new Vector2(34f, 66f), new Vector2(56f, 44f), 9f), Stroke(x, y, new Vector2(56f, 44f), new Vector2(96f, 86f), 9f));
            return White(circle * (1f - check));
        }

        private static Color White(float alpha) => new Color(1f, 1f, 1f, alpha);

        private static float Blob(float x, float y, float cx, float cy, float r)
        {
            var d = Dist(x, y, cx, cy) / r;
            return Mathf.Clamp01(1f - d * d);
        }

        private static float RoundedRect(float x, float y, float w, float h, float r)
        {
            var qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r - 1f);
            var qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r - 1f);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            var inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return Coverage(outside + inside - r);
        }

        private static float Polygon(float x, float y, Vector2[] points)
        {
            // 2×2 で数えて端をなめらかに。
            var hits = 0;
            for (var sy = 0; sy < 2; sy++)
            {
                for (var sx = 0; sx < 2; sx++)
                {
                    hits += Inside(x - 0.25f + sx * 0.5f, y - 0.25f + sy * 0.5f, points) ? 1 : 0;
                }
            }

            return hits / 4f;
        }

        private static bool Inside(float x, float y, Vector2[] p)
        {
            var inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float Stroke(float x, float y, Vector2 a, Vector2 b, float halfWidth)
        {
            var p = new Vector2(x, y);
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Coverage(Vector2.Distance(p, a + ab * t) - halfWidth);
        }

        private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static float Coverage(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);
    }
}
