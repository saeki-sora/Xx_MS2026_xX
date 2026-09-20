using UnityEngine;

namespace MS2026.Fortress
{
    public enum PlaceholderShape
    {
        Circle,
        Square
    }

    /// <summary>
    /// 本番の絵が無いオブジェクトに仮の図形を表示する。スプライトはロード時に毎回生成し直すため、
    /// シーンに保存されず、本番アセットへの差し替えは「このコンポーネントを外してSpriteRendererに絵を設定」するだけでよい。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlaceholderVisual : MonoBehaviour
    {
        public PlaceholderShape shape = PlaceholderShape.Circle;
        public Color color = Color.white;

        [Tooltip("SpriteRendererの描画順。大きいほど手前に描かれる。")]
        public int sortingOrder;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sprite = shape == PlaceholderShape.Square
                ? PlaceholderSpriteFactory.CreateSquareSprite()
                : PlaceholderSpriteFactory.CreateCircleSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }
    }
}
