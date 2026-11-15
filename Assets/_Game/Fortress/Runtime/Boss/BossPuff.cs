using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 仮の「はじける」表現（白い輪が広がって消える）。D-Drive の演出（<see cref="BossDefinition"/> の演出の欄）が入るまで、
    /// 何が起きたか分かるように出す。各PCで出す（通信なし）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossPuff : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _from;
        private float _to;
        private float _seconds;
        private float _time;
        private Color _color;
        private Vector2 _velocity;

        public static void Spawn(Vector3 position, float fromRadius, float toRadius, float seconds, Color color, Vector2 velocity = default)
        {
            var go = new GameObject("[Boss] Puff");
            go.transform.position = position + new Vector3(0f, 0f, -0.05f);
            var puff = go.AddComponent<BossPuff>();
            puff._renderer = go.AddComponent<SpriteRenderer>();
            puff._renderer.sprite = BossArt.Puff;
            puff._renderer.sortingOrder = 20;
            puff._from = fromRadius;
            puff._to = toRadius;
            puff._seconds = Mathf.Max(0.05f, seconds);
            puff._color = color;
            puff._velocity = velocity;
            puff.Apply(0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);
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
            _renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
        }
    }
}
