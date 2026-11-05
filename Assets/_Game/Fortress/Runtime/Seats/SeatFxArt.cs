using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの演出の絵と材質（コードで作る。2026-11-02）。形は白で作り、使う所でプレイヤーの色を付ける。
    /// D-Drive の VFX を入れれば（<see cref="WaveSeatRotation"/> の演出欄）、これと一緒に鳴る。
    /// </summary>
    internal static class SeatFxArt
    {
        private const int GroundQueue = 2980; // 地面の物（ルートの線と同じ。群衆の敵 2990 より下）

        private static Material _ground;
        private static Material _air;
        private static Sprite _softCircle;
        private static Sprite _disc;
        private static Sprite _ring;
        private static Sprite _thinRing;
        private static Sprite _chevron;
        private static Sprite _star;
        private static Sprite _tile;
        private static Sprite _beam;
        private static Sprite _vignette;
        private static Sprite _streak;
        private static Sprite _check;

        /// <summary>地面に描く物（点線・輪・影・床の波）。</summary>
        public static Material Ground => _ground != null ? _ground : _ground = MakeMaterial(GroundQueue);

        /// <summary>空中・砲台のまわりに描く物（キラッ・光の柱・光の玉・光の尾）。</summary>
        public static Material Air => _air != null ? _air : _air = MakeMaterial(3000);

        public static Sprite SoftCircle => _softCircle != null ? _softCircle : _softCircle = Make(64, 64, 0.5f, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Radius(u, v) * 2f), 1.6f));

        public static Sprite Disc => _disc != null ? _disc : _disc = Make(64, 64, 0.5f, (u, v) => Radius(u, v) < 0.46f ? 1f : 0f);

        public static Sprite Ring => _ring != null ? _ring : _ring = Make(128, 128, 0.5f, (u, v) => Band(Radius(u, v), 0.36f, 0.48f));

        public static Sprite ThinRing => _thinRing != null ? _thinRing : _thinRing = Make(128, 128, 0.5f, (u, v) => Band(Radius(u, v), 0.455f, 0.48f));

        /// <summary>上向きの「＾」。</summary>
        public static Sprite Chevron => _chevron != null ? _chevron : _chevron = Make(64, 64, 0.5f, (u, v) =>
        {
            var x = Mathf.Abs(u - 0.5f);
            return Mathf.Abs(v - (0.72f - x * 1.1f)) < 0.13f && x < 0.42f ? 1f : 0f;
        });

        /// <summary>4つの角の星（キラッ）。</summary>
        public static Sprite Star => _star != null ? _star : _star = Make(64, 64, 0.5f, (u, v) =>
        {
            var x = Mathf.Abs(u - 0.5f) * 2f;
            var y = Mathf.Abs(v - 0.5f) * 2f;
            var arms = Mathf.Pow(x, 0.5f) + Mathf.Pow(y, 0.5f);
            return Mathf.Clamp01((1.05f - arms) * 6f);
        });

        /// <summary>角の丸いタイル（床の光の波）。</summary>
        public static Sprite Tile => _tile != null ? _tile : _tile = Make(32, 32, 0.5f, (u, v) =>
        {
            var x = Mathf.Max(0f, Mathf.Abs(u - 0.5f) - 0.3f);
            var y = Mathf.Max(0f, Mathf.Abs(v - 0.5f) - 0.3f);
            return Mathf.Clamp01((0.15f - Mathf.Sqrt(x * x + y * y)) * 25f);
        });

        /// <summary>下から上へ薄くなる光の柱（根元が基準点）。</summary>
        public static Sprite Beam => _beam != null ? _beam : _beam = Make(32, 128, 0f, (u, v) =>
            Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f), 1.5f) * Mathf.Pow(1f - v, 1.3f));

        /// <summary>真ん中が透明で、端ほど濃い（画面の端を暗く）。</summary>
        public static Sprite Vignette => _vignette != null ? _vignette : _vignette = Make(128, 128, 0.5f, (u, v) =>
        {
            var r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.7071f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
        });

        /// <summary>横に長い、両端が細い線（勢いの線）。</summary>
        public static Sprite Streak => _streak != null ? _streak : _streak = Make(128, 16, 0.5f, (u, v) =>
            Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f / Mathf.Max(0.05f, Mathf.Sin(Mathf.PI * u))) * Mathf.Sin(Mathf.PI * u));

        /// <summary>白い ✓。</summary>
        public static Sprite Check => _check != null ? _check : _check = Make(64, 64, 0.5f, (u, v) =>
            Segment(u, v, 0.26f, 0.52f, 0.43f, 0.34f) < 0.07f || Segment(u, v, 0.43f, 0.34f, 0.76f, 0.7f) < 0.07f ? 1f : 0f);

        private static Material MakeMaterial(int queue)
        {
            return new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave, renderQueue = queue };
        }

        private static float Radius(float u, float v) => Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));

        private static float Band(float r, float inner, float outer) => r > inner && r < outer ? 1f : 0f;

        private static float Segment(float u, float v, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(u, v);
            var a = new Vector2(ax, ay);
            var ab = new Vector2(bx, by) - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // 0〜1 の座標で、少しぼかした（4点で数えた）濃さの白い絵。pivotY は基準点の高さ（0 = 下、0.5 = 真ん中）。1 単位 = 1 メートル。
        private static Sprite Make(int width, int height, float pivotY, Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var a = 0f;
                    for (var s = 0; s < 4; s++)
                    {
                        a += alphaAt((x + 0.25f + 0.5f * (s & 1)) / width, (y + 0.25f + 0.5f * (s >> 1)) / height);
                    }

                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a / 4f) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, pivotY), Mathf.Max(width, height));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
