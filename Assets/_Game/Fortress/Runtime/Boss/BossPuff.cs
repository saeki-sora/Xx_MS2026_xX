using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 仮の「はじける」表現（白い輪が広がって消える。黒煙は柔らかい丸がふくらんで消える）。D-Drive の演出（<see cref="BossDefinition"/> の演出の欄）が入るまで、
    /// 何が起きたか分かるように出す。各PCで出す（通信なし）。ゲームの時間で動く（止め・スローの間は一緒に止まる・ゆっくりになる）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossPuff : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _from;
        private float _to;
        private float _seconds;
        private float _time;
        private float _delay;
        private Color _color;
        private Vector2 _velocity;
        private bool _smoke;

        public static void Spawn(Vector3 position, float fromRadius, float toRadius, float seconds, Color color, Vector2 velocity = default) =>
            Create(position, fromRadius, toRadius, seconds, color, velocity, 0f, false);

        /// <summary>黒煙（delay 秒たってから湧き、ふくらみながら薄くなる）。</summary>
        public static void Smoke(Vector3 position, float fromRadius, float toRadius, float seconds, Color color, Vector2 velocity, float delay) =>
            Create(position, fromRadius, toRadius, seconds, color, velocity, delay, true);

        private static void Create(Vector3 position, float fromRadius, float toRadius, float seconds, Color color, Vector2 velocity, float delay, bool smoke)
        {
            var go = new GameObject(smoke ? "[Boss] Smoke" : "[Boss] Puff");
            go.transform.position = position + new Vector3(0f, 0f, smoke ? -0.03f : -0.05f);
            var puff = go.AddComponent<BossPuff>();
            puff._renderer = go.AddComponent<SpriteRenderer>();
            puff._renderer.sprite = smoke ? BossArt.SoftDisc : BossArt.Puff;
            puff._renderer.sortingOrder = smoke ? 19 : 20;
            puff._from = fromRadius;
            puff._to = toRadius;
            puff._seconds = Mathf.Max(0.05f, seconds);
            puff._color = color;
            puff._velocity = velocity;
            puff._delay = Mathf.Max(0f, delay);
            puff._smoke = smoke;
            puff.Apply(0f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_delay > 0f)
            {
                _delay -= dt;
                Apply(0f);
                return;
            }

            _time += dt;
            transform.position += (Vector3)(_velocity * dt);
            var t = _time / _seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Apply(t);
        }

        private void Apply(float t)
        {
            var eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(_from, _to, eased);
            var alpha = _delay > 0f ? 0f : _smoke ? Mathf.Clamp01(t * 6f) * (1f - t) : 1f - t;
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
        }
    }
}
