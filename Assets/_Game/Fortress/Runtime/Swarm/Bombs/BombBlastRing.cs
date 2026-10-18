using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 爆発の届く範囲を見せる、広がって消える輪（仮の見た目。EnemyBombSettings.showBlastRing）。
    /// 地面に寝かせて置き、使い終わったら非表示にして使い回す。
    /// 輪はPlay中のシーンの物として作る（DontSave にしない）。DontSave だと、爆発の途中でPlayを止めたとき輪がシーンの外に残り、
    /// 広がりかけのまま固まって、コアの近くにオレンジの輪が残っていた（2026-10-18 修正）。すでに残った輪は <see cref="DestroyLeftovers"/> で消す。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BombBlastRing : MonoBehaviour
    {
        private const float Duration = 0.35f;
        private const string RootName = "[Fortress] BombBlastRings";
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
                var rootObject = new GameObject(RootName);
                _root = rootObject.transform;
            }

            var go = new GameObject("BlastRing");
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

        /// <summary>
        /// 以前の作り方（DontSave）でPlayの外に残ってしまった輪を、すべて消す。残った輪は誰にも動かされず、広がりかけのまま固まって見える。
        /// Play開始時（下の ResetStatics）と、エディタでスクリプトを読み込んだとき・Playを止めたとき（エディタ側の BombBlastRingCleanup）に呼ぶ。
        /// 戻り値は消したオブジェクトの数。
        /// </summary>
        public static int DestroyLeftovers()
        {
            var count = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.transform.parent != null || go.name != RootName || !IsLeftover(go))
                {
                    continue;
                }

                DestroyImmediate(go);
                count++;
            }

            // 親が無くなって単独で残った輪も消す。
            foreach (var ring in Resources.FindObjectsOfTypeAll<BombBlastRing>())
            {
                if (ring != null && IsLeftover(ring.gameObject))
                {
                    DestroyImmediate(ring.transform.root.gameObject);
                    count++;
                }
            }

            Pool.Clear();
            _root = null;
            return count;
        }

        // アセット（Prefab など）は消さない。シーンに属する物か、シーンに保存しない印の付いた物だけ。
        private static bool IsLeftover(GameObject go)
        {
            return go.scene.IsValid() || (go.hideFlags & HideFlags.DontSaveInEditor) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DestroyLeftovers();
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
