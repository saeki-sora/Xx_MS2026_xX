using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆発の届く範囲を見せる、広がって消える輪（仮の見た目。EnemyBombSettings.showBlastRing）。
    /// 地面に寝かせて置き、使い終わったら非表示にして使い回す。保存されない（HideFlags.DontSave）。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BombBlastRing : MonoBehaviour
    {
        private const float Duration = 0.35f;
        private const int TextureSize = 128;

        // 床（z=0）より少しだけカメラ側（上は -Z）に置いて、床に埋もれないようにする。
        private const float Lift = -0.05f;

        private static readonly Stack<BombBlastRing> Pool = new Stack<BombBlastRing>();
        private static Sprite _sprite;
        private static Transform _root;

        private SpriteRenderer _renderer;
        private float _age;
        private float _radius;
        private Color _color;

        /// <summary>その場所に、半径 radius まで広がる輪を出す。</summary>
        public static void Spawn(Vector2 center, float radius, Color color)
        {
            // 場面の切り替えで壊れたもの（Unity の null）は読み飛ばす。
            BombBlastRing ring = null;
            while (ring == null && Pool.Count > 0)
            {
                ring = Pool.Pop();
            }

            if (ring == null)
            {
                ring = Create();
            }

            ring.transform.position = new Vector3(center.x, center.y, Lift);
            ring._radius = radius;
            ring._color = color;
            ring._age = 0f;
            ring.Apply(0f);
            ring.gameObject.SetActive(true);
        }

        private static BombBlastRing Create()
        {
            if (_root == null)
            {
                var rootObject = new GameObject("[Fortress] BombBlastRings") { hideFlags = HideFlags.DontSave };
                _root = rootObject.transform;
            }

            var go = new GameObject("BlastRing") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root, false);
            go.AddComponent<BillboardIgnore>();
            var ring = go.AddComponent<BombBlastRing>();
            ring._renderer = go.AddComponent<SpriteRenderer>();
            ring._renderer.sprite = GetSprite();
            ring._renderer.sortingOrder = 100;
            return ring;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Duration)
            {
                gameObject.SetActive(false);
                Pool.Push(this);
                return;
            }

            Apply(_age / Duration);
        }

        private void Apply(float t)
        {
            // 速く広がって、ゆっくり消える。
            var grow = 1f - (1f - t) * (1f - t);
            var diameter = Mathf.Lerp(0.3f, 1f, grow) * _radius * 2f;
            transform.localScale = new Vector3(diameter, diameter, 1f);
            var color = _color;
            color.a *= 1f - t;
            _renderer.color = color;
        }

        private static Sprite GetSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "BombBlastRing",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[TextureSize * TextureSize];
            var half = TextureSize * 0.5f;
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);

                    // 縁がくっきりした輪＋内側にうっすら塗り。
                    var ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.1f);
                    var fill = r < 1f ? 0.25f * r : 0f;
                    var alpha = Mathf.Clamp01(Mathf.Max(ring, fill));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
            _sprite.hideFlags = HideFlags.DontSave;
            return _sprite;
        }
    }
}
