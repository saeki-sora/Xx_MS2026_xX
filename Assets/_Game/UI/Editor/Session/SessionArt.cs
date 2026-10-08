using System.IO;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ロビーの画面の仮の絵（PNG）。無いときだけ作るので、同じ名前のファイルを本番の絵で上書きすれば、全部の画面がその絵になる。
    /// 色はタイトルに合わせたパステル（水色・ピンク・ラベンダー）。
    /// </summary>
    public static class SessionArt
    {
        public const string Folder = "Assets/_Game/UI/Art/Session";
        public const string Background = Folder + "/Session_Background.png";
        public const string Panel = Folder + "/Session_Panel.png";
        public const string Dot = Folder + "/Session_Dot.png";
        public const string Ready = Folder + "/Session_Ready.png";
        public const string Host = Folder + "/Session_Host.png";

        public static readonly Color Ink = new Color(0.16f, 0.17f, 0.33f);
        public static readonly Color SubInk = new Color(0.4f, 0.42f, 0.58f);
        public static readonly Color Card = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color CardShade = new Color(0.93f, 0.94f, 1f, 0.95f);
        public static readonly Color Pink = new Color(1f, 0.46f, 0.72f);
        public static readonly Color Cyan = new Color(0.22f, 0.78f, 0.95f);
        public static readonly Color Lavender = new Color(0.62f, 0.55f, 1f);
        public static readonly Color Mint = new Color(0.3f, 0.85f, 0.62f);
        public static readonly Color Gray = new Color(0.72f, 0.74f, 0.84f);

        /// <summary>無い絵だけ作る。戻り値は作った数。</summary>
        public static int EnsureAll()
        {
            StudioAssets.EnsureFolder(Folder);
            var made = 0;
            made += Make(Background, 1920, 1080, PaintBackground, Vector4.zero);
            made += Make(Panel, 96, 96, (x, y, w, h) => RoundedRect(x, y, w, h, 30f), new Vector4(36, 36, 36, 36));
            made += Make(Dot, 64, 64, (x, y, w, h) => Mathf.Pow(Mathf.Clamp01(1f - Dist(x, y, 32f, 32f) / 31f), 1.4f), Vector4.zero);
            made += Make(Ready, 128, 128, PaintCheck, Vector4.zero);
            made += Make(Host, 128, 128, PaintCrown, Vector4.zero);
            if (made > 0)
            {
                AssetDatabase.Refresh();
                foreach (var path in new[] { Background, Panel, Dot, Ready, Host })
                {
                    Configure(path, path == Panel ? new Vector4(36, 36, 36, 36) : Vector4.zero);
                }
            }

            return made;
        }

        public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // ── 描く ─────────────────

        private delegate float AlphaFunc(float x, float y, float w, float h);

        private static int Make(string path, int width, int height, AlphaFunc alpha, Vector4 border)
        {
            if (File.Exists(path))
            {
                return 0;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = path == Background ? (Color32)BackgroundColor(x, y, width, height) : new Color(1f, 1f, 1f, alpha(x + 0.5f, y + 0.5f, width, height));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return 1;
        }

        private static void Configure(string path, Vector4 border)
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
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static float PaintBackground(float x, float y, float w, float h) => 1f;

        // パステルの縦グラデーション＋薄い方眼＋ぼんやりした丸（タイトルの「塗り替えた後」の雰囲気）。
        private static Color BackgroundColor(int x, int y, int w, int h)
        {
            var t = y / (float)(h - 1);
            var top = new Color(0.86f, 0.93f, 1f);
            var middle = new Color(0.93f, 0.89f, 1f);
            var bottom = new Color(1f, 0.9f, 0.96f);
            var color = t > 0.5f ? Color.Lerp(middle, top, (t - 0.5f) * 2f) : Color.Lerp(bottom, middle, t * 2f);

            var gx = Mathf.Abs(Mathf.Repeat(x, 64f) - 32f);
            var gy = Mathf.Abs(Mathf.Repeat(y, 64f) - 32f);
            var grid = Mathf.Clamp01(1f - Mathf.Min(32f - gx, 32f - gy) / 1.2f) * 0.08f;
            color = Color.Lerp(color, new Color(0.55f, 0.65f, 1f), grid);

            var glow = 0f;
            glow += Blob(x, y, w * 0.18f, h * 0.78f, 260f);
            glow += Blob(x, y, w * 0.82f, h * 0.24f, 320f);
            glow += Blob(x, y, w * 0.64f, h * 0.86f, 180f) * 0.6f;
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(glow) * 0.55f);
            return color;
        }

        private static float Blob(float x, float y, float cx, float cy, float r)
        {
            var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
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

        private static float PaintCheck(float x, float y, float w, float h)
        {
            // 丸の中にチェック（チェックの形は抜く）。
            var circle = Coverage(Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 64f) * (y - 64f)) - 60f);
            var check = Mathf.Max(Stroke(x, y, new Vector2(34f, 66f), new Vector2(56f, 44f), 9f), Stroke(x, y, new Vector2(56f, 44f), new Vector2(96f, 86f), 9f));
            return circle * (1f - check);
        }

        private static float PaintCrown(float x, float y, float w, float h)
        {
            var crown = new[]
            {
                new Vector2(16f, 26f), new Vector2(112f, 26f), new Vector2(118f, 92f), new Vector2(90f, 62f),
                new Vector2(64f, 104f), new Vector2(38f, 62f), new Vector2(10f, 92f)
            };
            var body = Polygon(x, y, crown);
            var gems = Mathf.Max(Coverage(Dist(x, y, 10f, 98f) - 9f), Mathf.Max(Coverage(Dist(x, y, 64f, 110f) - 10f), Coverage(Dist(x, y, 118f, 98f) - 9f)));
            return Mathf.Max(body, gems);
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
