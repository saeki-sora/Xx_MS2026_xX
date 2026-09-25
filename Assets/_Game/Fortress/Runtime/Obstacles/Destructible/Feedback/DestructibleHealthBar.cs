using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>破壊可能物の頭上に耐久ゲージを出す（Play中のみ）。障害物のスケールに影響されないよう、別オブジェクトとして追従させる。</summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleHealthBar : MonoBehaviour
    {
        private const int SortingOrder = 100;

        private DestructibleObstacle _obstacle;
        private Transform _barRoot;
        private Transform _back;
        private Transform _fill;
        private SpriteRenderer _backRenderer;
        private SpriteRenderer _fillRenderer;

        private void OnEnable()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
        }

        private void OnDisable()
        {
            DestroyBar();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || _obstacle == null)
            {
                return;
            }

            var settings = _obstacle.settings.visual.healthBar;
            if (!ShouldShow(settings))
            {
                if (_barRoot != null)
                {
                    _barRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (_barRoot == null)
            {
                CreateBar();
            }

            _barRoot.gameObject.SetActive(true);
            Layout(settings);
        }

        private bool ShouldShow(DestructibleHealthBarSettings settings)
        {
            if (_obstacle.IsDestroyed || settings.mode == DestructibleHealthBarMode.Never)
            {
                return false;
            }

            return settings.mode == DestructibleHealthBarMode.Always || _obstacle.Health01 < 1f;
        }

        private void Layout(DestructibleHealthBarSettings settings)
        {
            var scale = transform.lossyScale;
            var width = settings.width > 0f ? settings.width : Mathf.Abs(scale.x);
            var top = transform.position.y + Mathf.Abs(scale.y) * 0.5f + settings.offsetY + settings.height * 0.5f;
            _barRoot.position = new Vector3(transform.position.x, top, transform.position.z);

            var health01 = _obstacle.Health01;
            _back.localScale = new Vector3(width, settings.height, 1f);
            _fill.localScale = new Vector3(width * health01, settings.height, 1f);
            _fill.localPosition = new Vector3(-width * 0.5f + width * health01 * 0.5f, 0f, 0f);
            _backRenderer.color = settings.backColor;
            _fillRenderer.color = settings.fillColor;
        }

        private void CreateBar()
        {
            _barRoot = new GameObject("[DestructibleHealthBar]").transform;
            (_back, _backRenderer) = CreatePiece("Back", SortingOrder);
            (_fill, _fillRenderer) = CreatePiece("Fill", SortingOrder + 1);
        }

        private (Transform, SpriteRenderer) CreatePiece(string pieceName, int sortingOrder)
        {
            var piece = new GameObject(pieceName);
            piece.transform.SetParent(_barRoot, false);

            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSpriteFactory.CreateSquareSprite();
            renderer.sortingOrder = sortingOrder;
            return (piece.transform, renderer);
        }

        private void DestroyBar()
        {
            if (_barRoot != null)
            {
                Destroy(_barRoot.gameObject);
            }

            _barRoot = null;
        }
    }
}
