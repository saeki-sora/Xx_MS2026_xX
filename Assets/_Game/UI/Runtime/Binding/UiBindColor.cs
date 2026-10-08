using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// 値で色を変える。数なら「グラデーション」のどこの色か（例: 熱が上がるほど 黄→橙→赤）、色の値ならその色そのもの。
    /// </summary>
    [AddComponentMenu("UI Studio/値で色を変える (UiBindColor)")]
    public sealed class UiBindColor : UiBinding
    {
        [Tooltip("色を変える部品（空なら同じ GameObject の画像・文字）。")]
        public Graphic target;

        [Tooltip("数の値のとき使う色の帯。左端=最小、右端=最大。")]
        public Gradient gradient = DefaultGradient();

        [Tooltip("値がこの数のとき帯の左端の色。")]
        public float min;

        [Tooltip("値がこの数のとき帯の右端の色。")]
        public float max = 1f;

        [Tooltip("ONなら、元の透明度（アルファ）はそのまま残す。")]
        public bool keepAlpha = true;

        protected override void OnEnable()
        {
            if (target == null)
            {
                target = GetComponent<Graphic>();
            }

            base.OnEnable();
        }

        protected override void Apply(UiValue value)
        {
            if (target == null)
            {
                return;
            }

            var color = value.Kind == UiValueKind.Color
                ? value.Color
                : gradient.Evaluate(Mathf.Approximately(max, min) ? 0f : Mathf.Clamp01((value.AsNumber - min) / (max - min)));
            if (keepAlpha)
            {
                color.a = target.color.a;
            }

            target.color = color;
        }

        private static Gradient DefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.48f, 0.24f), 0.6f), new GradientColorKey(new Color(1f, 0.2f, 0.25f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
