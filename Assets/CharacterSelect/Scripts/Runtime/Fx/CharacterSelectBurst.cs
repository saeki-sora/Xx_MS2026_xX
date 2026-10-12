using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面（UI）の上で、泡やキラキラをパッと弾けさせる演出（決定したときなど）。
    /// 最初に決まった数の Image を作っておき、使い回す（再生中に物を作らない）。
    /// 絵は <see cref="sprites"/> から順番に使う（泡・キラキラなど）。
    /// </summary>
    [AddComponentMenu("キャラ選択/演出/弾ける泡とキラキラ (CharacterSelectBurst)")]
    public sealed class CharacterSelectBurst : MonoBehaviour
    {
        [Tooltip("飛ばす絵（順番に使う）。")]
        public Sprite[] sprites = System.Array.Empty<Sprite>();

        [Tooltip("同時に出せる数（多めに用意しておく）。")]
        [Range(4, 256)]
        public int poolSize = 64;

        [Tooltip("1つの大きさ（ピクセル。この範囲でばらつく）。")]
        public Vector2 size = new Vector2(18f, 46f);

        [Tooltip("飛び出す速さ（ピクセル/秒。この範囲でばらつく）。")]
        public Vector2 speed = new Vector2(250f, 750f);

        [Tooltip("下に引っぱる力（ピクセル/秒²）。マイナスで上へ浮く（泡らしく）。")]
        public float gravity = 500f;

        [Tooltip("消えるまでの秒数（この範囲でばらつく）。")]
        public Vector2 life = new Vector2(0.5f, 1f);

        [Tooltip("回る速さ（度/秒。この範囲でばらつく）。")]
        public float spin = 360f;

        private struct Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 position;
            public Vector2 velocity;
            public Color color;
            public float age;
            public float life;
            public float size;
            public float spin;
            public float angle;
            public bool alive;
        }

        private Particle[] _particles = System.Array.Empty<Particle>();
        private int _next;
        private int _spriteIndex;

        private void Awake()
        {
            _particles = new Particle[poolSize];
            for (var i = 0; i < poolSize; i++)
            {
                var go = new GameObject("p", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                go.SetActive(false);
                _particles[i] = new Particle { rect = (RectTransform)go.transform, image = image };
            }
        }

        /// <summary>この物の真ん中（＋ずれ）から count 個を弾けさせる。白と color を交互に使う。</summary>
        public void Play(Color color, int count, Vector2 offset = default)
        {
            for (var n = 0; n < count && _particles.Length > 0; n++)
            {
                ref var p = ref _particles[_next];
                _next = (_next + 1) % _particles.Length;
                var angle = Random.Range(0f, Mathf.PI * 2f);
                p.position = offset;
                p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(speed.x, speed.y);
                p.color = n % 3 == 0 ? Color.white : color;
                p.age = 0f;
                p.life = Random.Range(life.x, life.y);
                p.size = Random.Range(size.x, size.y);
                p.spin = Random.Range(-spin, spin);
                p.angle = Random.Range(0f, 360f);
                p.alive = true;
                if (sprites != null && sprites.Length > 0)
                {
                    p.image.sprite = sprites[_spriteIndex++ % sprites.Length];
                }

                p.rect.gameObject.SetActive(true);
                Place(ref p);
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.alive)
                {
                    continue;
                }

                p.age += dt;
                if (p.age >= p.life)
                {
                    p.alive = false;
                    p.rect.gameObject.SetActive(false);
                    continue;
                }

                p.velocity *= Mathf.Exp(-2.5f * dt);
                p.velocity.y -= gravity * dt;
                p.position += p.velocity * dt;
                p.angle += p.spin * dt;
                Place(ref p);
            }
        }

        private static void Place(ref Particle p)
        {
            var t = p.age / p.life;
            p.rect.anchoredPosition = p.position;
            p.rect.localRotation = Quaternion.Euler(0f, 0f, p.angle);
            // 出た瞬間にふくらんで、だんだん小さく薄くなる。
            var grow = Mathf.Min(1f, t * 8f) * (1f - t * 0.5f);
            p.rect.sizeDelta = Vector2.one * (p.size * grow);
            var c = p.color;
            c.a = 1f - t * t;
            p.image.color = c;
        }
    }
}
