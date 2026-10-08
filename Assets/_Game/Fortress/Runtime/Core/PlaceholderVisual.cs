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
#if UNITY_EDITOR
            // OnValidate の中でスプライトを変えると「SendMessage は OnValidate 中に呼べません」の警告が出るので、1フレーム遅らせる。
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Apply();
                }
            };
#else
            Apply();
#endif
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
