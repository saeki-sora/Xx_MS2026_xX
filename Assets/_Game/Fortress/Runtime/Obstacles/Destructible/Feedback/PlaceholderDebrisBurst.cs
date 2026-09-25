using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>素材が無い間の代わりに、四角い破片を四方へ飛ばして薄れさせる一時オブジェクト。</summary>
    public sealed class PlaceholderDebrisBurst : MonoBehaviour
    {
        private const float LifetimeSeconds = 0.7f;
        private const float MinSpeed = 2f;
        private const float MaxSpeed = 5f;

        private Transform[] _pieces;
        private SpriteRenderer[] _renderers;
        private Vector2[] _velocities;
        private float[] _spins;
        private Color _color;
        private float _age;

        public static void Spawn(Vector2 center, Vector2 areaSize, int count, Color color)
        {
            var go = new GameObject("DebrisBurst");
            go.transform.position = center;
            go.AddComponent<PlaceholderDebrisBurst>().Initialize(areaSize, count, color);
        }

        private void Initialize(Vector2 areaSize, int count, Color color)
        {
            _color = color;
            _pieces = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _velocities = new Vector2[count];
            _spins = new float[count];

            var sprite = PlaceholderSpriteFactory.CreateSquareSprite();
            var pieceSize = Mathf.Clamp(Mathf.Min(areaSize.x, areaSize.y) * 0.18f, 0.08f, 0.5f);

            for (var i = 0; i < count; i++)
            {
                var piece = new GameObject("Piece");
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = new Vector3(
                    Random.Range(-0.5f, 0.5f) * areaSize.x,
                    Random.Range(-0.5f, 0.5f) * areaSize.y,
                    0f);
                piece.transform.localScale = Vector3.one * (pieceSize * Random.Range(0.6f, 1.2f));

                var renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = 50;

                _pieces[i] = piece.transform;
                _renderers[i] = renderer;
                _velocities[i] = Random.insideUnitCircle.normalized * Random.Range(MinSpeed, MaxSpeed);
                _spins[i] = Random.Range(-360f, 360f);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            var life01 = _age / LifetimeSeconds;
            if (life01 >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var color = _color;
            color.a *= 1f - life01;

            for (var i = 0; i < _pieces.Length; i++)
            {
                _pieces[i].position += (Vector3)(_velocities[i] * Time.deltaTime);
                _pieces[i].Rotate(0f, 0f, _spins[i] * Time.deltaTime);
                _velocities[i] *= 1f - 3f * Time.deltaTime;
                _renderers[i].color = color;
            }
        }
    }
}
