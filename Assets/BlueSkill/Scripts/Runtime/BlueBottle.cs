using System;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// うがい薬のボトル（使える状態の間に砲台の上に持ち、投げると放物線を描いて飛ぶ）。床に寝かせた SpriteRenderer 1枚＋床に落とす影。
    /// 絵が設定されていなければ、コードで描いた仮のボトル（青い液体・白いラベルに水のしずく・太い線のトゥーン調）を使う。
    /// </summary>
    internal sealed class BlueBottle : IDisposable
    {
        private const int PlaceholderWidth = 112;
        private const int PlaceholderHeight = 208;

        private static Sprite _placeholder;

        private readonly GameObject _root;
        private readonly SpriteRenderer _renderer;
        private readonly SpriteRenderer _shadow;
        private readonly Material _shadowMaterial;

        public BlueBottle(Transform parent)
        {
            _root = new GameObject("Bottle") { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(parent, false);
            _renderer = _root.AddComponent<SpriteRenderer>();
            _renderer.enabled = false;

            var shadowGo = new GameObject("BottleShadow") { hideFlags = HideFlags.DontSave };
            shadowGo.transform.SetParent(parent, false);
            _shadow = shadowGo.AddComponent<SpriteRenderer>();
            _shadow.sprite = BlueFx.SoftCircle;
            _shadow.enabled = false;
            // 影は敵より下に描く（自分だけのマテリアルになる。消すときに一緒に消す）。
            _shadowMaterial = _shadow.material;
            _shadowMaterial.renderQueue = BlueFx.GroundShadowRenderQueue;
        }

        public bool IsShown => _renderer.enabled;

        public Vector3 Position => _root.transform.position;

        /// <summary>ボトルの Transform（効果音・エフェクトを付けていく先）。</summary>
        public Transform Transform => _root.transform;

        /// <summary>ボトルを出す。up = 口の向き（床の上の向き）。</summary>
        public void Show(BlueBottleSettings settings, Vector3 position, Vector2 up, float scale, float alpha)
        {
            var sprite = settings.sprite != null ? settings.sprite : Placeholder;
            if (_renderer.sprite != sprite)
            {
                _renderer.sprite = sprite;
            }

            var bounds = sprite.bounds.size;
            var sx = bounds.x > 0.0001f ? settings.width / bounds.x : 1f;
            var sy = bounds.y > 0.0001f ? settings.length / bounds.y : 1f;
            var angle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
            _root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
            _root.transform.localScale = new Vector3(sx * scale, sy * scale, 1f);
            var color = settings.tint;
            color.a *= Mathf.Clamp01(alpha);
            _renderer.color = color;
            _renderer.sortingOrder = settings.sortingOrder;
            _renderer.enabled = scale > 0.001f && color.a > 0.001f;
        }

        public void Hide()
        {
            _renderer.enabled = false;
            _shadow.enabled = false;
        }

        /// <summary>床に落とす影（飛んでいる間）。</summary>
        public void ShowShadow(Vector3 groundPosition, float size, float alpha, int sortingOrder)
        {
            if (alpha <= 0.001f || size <= 0.001f)
            {
                _shadow.enabled = false;
                return;
            }

            _shadow.transform.SetPositionAndRotation(groundPosition, Quaternion.identity);
            _shadow.transform.localScale = new Vector3(size, size * 0.8f, 1f);
            _shadow.color = new Color(0.02f, 0.05f, 0.15f, Mathf.Clamp01(alpha));
            _shadow.sortingOrder = sortingOrder;
            _shadow.enabled = true;
        }

        public void HideShadow()
        {
            _shadow.enabled = false;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                BlueFx.Destroy(_root);
            }

            if (_shadow != null)
            {
                BlueFx.Destroy(_shadow.gameObject);
            }

            BlueFx.Destroy(_shadowMaterial);
        }

        // ------------------------------------------------------------------
        // 仮のボトルの絵（コードで描く）
        // ------------------------------------------------------------------

        private static Sprite Placeholder
        {
            get
            {
                if (_placeholder != null)
                {
                    return _placeholder;
                }

                var texture = new Texture2D(PlaceholderWidth, PlaceholderHeight, TextureFormat.RGBA32, false)
                {
                    name = "BlueSkillBottle",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                texture.SetPixels32(PaintBottle(PlaceholderWidth, PlaceholderHeight));
                texture.Apply(false, true);
                _placeholder = Sprite.Create(texture, new Rect(0f, 0f, PlaceholderWidth, PlaceholderHeight), new Vector2(0.5f, 0.5f), PlaceholderHeight);
                _placeholder.hideFlags = HideFlags.HideAndDontSave;
                return _placeholder;
            }
        }

        /// <summary>ボトルの半分の幅（ピクセル）。外なら -1。v = 下から 0〜1。</summary>
        private static float HalfWidth(float v, float w, float h)
        {
            const float bottom = 0.035f;
            if (v < bottom || v > 0.965f)
            {
                return -1f;
            }

            var body = 0.41f * w;
            if (v < 0.6f)
            {
                // 底の角を丸くする
                var r = 0.13f * w;
                var dy = (v - bottom) * h;
                if (dy < r)
                {
                    var t = r - dy;
                    return body - r + Mathf.Sqrt(Mathf.Max(0f, r * r - t * t));
                }

                return body;
            }

            if (v < 0.72f)
            {
                var s = Mathf.SmoothStep(0f, 1f, (v - 0.6f) / 0.12f);
                return Mathf.Lerp(body, 0.19f * w, s);
            }

            if (v < 0.77f)
            {
                return 0.19f * w;
            }

            if (v < 0.8f)
            {
                return 0.23f * w;
            }

            // キャップ（上の角を丸くする）
            var capHalf = 0.27f * w;
            var capR = 0.07f * w;
            var top = (0.965f - v) * h;
            if (top < capR)
            {
                var t = capR - top;
                return capHalf - capR + Mathf.Sqrt(Mathf.Max(0f, capR * capR - t * t));
            }

            return capHalf;
        }

        private static Color32[] PaintBottle(int w, int h)
        {
            var inside = new bool[w * h];
            for (var y = 0; y < h; y++)
            {
                var v = (y + 0.5f) / h;
                var half = HalfWidth(v, w, h);
                for (var x = 0; x < w; x++)
                {
                    var ux = x + 0.5f - w * 0.5f;
                    inside[y * w + x] = half > 0f && Mathf.Abs(ux) <= half;
                }
            }

            const int ink = 4;
            const int offset = 4;
            var inkColor = new Color(0.04f, 0.07f, 0.2f, 1f);
            var cyan = new Color(0.25f, 0.9f, 1f, 1f);
            var violet = new Color(0.5f, 0.32f, 1f, 1f);
            var pixels = new Color32[w * h];

            for (var y = 0; y < h; y++)
            {
                var v = (y + 0.5f) / h;
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (inside[i])
                    {
                        pixels[i] = Interior(x + 0.5f - w * 0.5f, v, w, h);
                        continue;
                    }

                    if (Near(inside, w, h, x, y, ink))
                    {
                        pixels[i] = inkColor;
                        continue;
                    }

                    // タイトルの絵のような、左右にずれた色の縁取り
                    if (Near(inside, w, h, x + offset, y, ink))
                    {
                        pixels[i] = cyan;
                    }
                    else if (Near(inside, w, h, x - offset, y, ink))
                    {
                        pixels[i] = violet;
                    }
                    else
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                    }
                }
            }

            return pixels;
        }

        private static bool Near(bool[] inside, int w, int h, int cx, int cy, int radius)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var y = cy + dy;
                if (y < 0 || y >= h)
                {
                    continue;
                }

                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = cx + dx;
                    if (x < 0 || x >= w || dx * dx + dy * dy > radius * radius)
                    {
                        continue;
                    }

                    if (inside[y * w + x])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Color Interior(float ux, float v, int w, int h)
        {
            var hw = 0.41f * w;
            Color color;

            if (v >= 0.8f)
            {
                // キャップ: 濃い青＋縦のみぞ、上にハイライト
                var ridge = Mathf.Repeat(ux + w, 7f) < 2.2f ? 0.78f : 1f;
                color = new Color(0.1f, 0.24f, 0.7f) * ridge;
                if (v > 0.93f)
                {
                    color = Color.Lerp(color, new Color(0.45f, 0.65f, 1f), 0.6f);
                }
            }
            else if (v >= 0.77f)
            {
                color = new Color(0.93f, 0.97f, 1f);
            }
            else if (v >= 0.6f && v < 0.77f && v > LiquidLevel(ux))
            {
                color = new Color(0.78f, 0.9f, 1f);
            }
            else
            {
                var level = LiquidLevel(ux);
                if (v <= level)
                {
                    // うがい薬（青い液体）。下ほど濃い。
                    var t = Mathf.Clamp01(v / level);
                    color = Color.Lerp(new Color(0.03f, 0.18f, 0.72f), new Color(0.18f, 0.55f, 1f), t);
                    if (Mathf.Abs(v - level) * h < 2.5f)
                    {
                        color = new Color(0.6f, 0.9f, 1f);
                    }

                    // 中の泡
                    if (Bubble(ux, v * h, -0.18f * w, 0.12f * h, 4.5f) || Bubble(ux, v * h, 0.2f * w, 0.47f * h, 3.2f)
                        || Bubble(ux, v * h, 0.05f * w, 0.08f * h, 2.6f))
                    {
                        color = Color.Lerp(color, Color.white, 0.55f);
                    }
                }
                else
                {
                    color = new Color(0.8f, 0.92f, 1f);
                }
            }

            // ラベル（白地に青い水のしずく＋波線）
            if (v > 0.17f && v < 0.43f && v < 0.6f)
            {
                var border = v < 0.185f || v > 0.415f;
                color = border ? new Color(0.12f, 0.4f, 1f) : new Color(0.97f, 0.98f, 1f);
                if (!border)
                {
                    var py = v * h;
                    if (Drop(ux, py, 0f, 0.285f * h, 0.15f * w))
                    {
                        color = new Color(0.1f, 0.45f, 1f);
                        if (Bubble(ux, py, -0.05f * w, 0.27f * h, 3.2f))
                        {
                            color = Color.white;
                        }
                    }

                    var wave = 0.215f * h + Mathf.Sin(ux * 0.22f) * 2.2f;
                    if (Mathf.Abs(py - wave) < 1.6f && Mathf.Abs(ux) < 0.33f * w)
                    {
                        color = new Color(0.25f, 0.8f, 1f);
                    }
                }
            }

            // 右側に影、左側に細い光
            if (v < 0.8f)
            {
                var shade = Mathf.SmoothStep(0.15f * w, hw, ux);
                color = Color.Lerp(color, color * 0.62f, shade * 0.8f);
                if (ux > -0.3f * w && ux < -0.22f * w && v > 0.07f && v < 0.7f)
                {
                    color = Color.Lerp(color, Color.white, 0.65f);
                }
            }

            color.a = 1f;
            return color;
        }

        private static float LiquidLevel(float ux)
        {
            return 0.52f + Mathf.Sin(ux * 0.12f) * 0.012f;
        }

        private static bool Bubble(float ux, float py, float cx, float cy, float r)
        {
            var dx = ux - cx;
            var dy = py - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>水のしずく（下が丸く、上がとがる）。</summary>
        private static bool Drop(float ux, float py, float cx, float cy, float r)
        {
            var dx = ux - cx;
            var dy = py - cy;
            if (dx * dx + dy * dy <= r * r)
            {
                return true;
            }

            var tipHeight = r * 1.6f;
            if (dy < 0f || dy > tipHeight)
            {
                return false;
            }

            var halfAtY = r * (1f - dy / tipHeight) * 0.92f;
            return Mathf.Abs(dx) <= halfAtY;
        }
    }
}
