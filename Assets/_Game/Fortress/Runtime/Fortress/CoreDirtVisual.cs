using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアクリスタルのHPが減るほど、見た目の色を少しずつ「汚れた色」へ寄せる。
    /// <see cref="CoreCrystalController"/> が起動時に自動で付けるので、シーンに手で置く必要はない。
    /// 色だけを変える（クリーンな色は起動時のスプライトの色）。HPはHostの値なので、今はHost（と1人用）の画面でだけ変わる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CoreCrystalController))]
    public sealed class CoreDirtVisual : MonoBehaviour
    {
        [Tooltip("HPが0に近いときの汚れた色。クリーンな色（起動時の色）からこの色へ少しずつ変わる。")]
        public Color dirtyColor = new Color(0.30f, 0.22f, 0.18f, 1f);

        [Tooltip("汚れ方の強さのカーブ。横軸=失ったHPの割合、縦軸=汚れの度合い。直線なら一定のペースで汚れていく。")]
        public AnimationCurve dirtCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private CoreCrystalController _core;
        private SpriteRenderer[] _renderers;
        private Color[] _cleanColors;

        private void Awake()
        {
            _core = GetComponent<CoreCrystalController>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _cleanColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
            {
                _cleanColors[i] = _renderers[i].color;
            }
        }

        private void OnEnable()
        {
            _core.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(_core.CurrentHealth, _core.maxHealth);
        }

        private void OnDisable()
        {
            _core.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            var lost01 = max > 0f ? Mathf.Clamp01(1f - current / max) : 0f;
            var t = Mathf.Clamp01(dirtCurve.Evaluate(lost01));
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }

                var dirty = new Color(dirtyColor.r, dirtyColor.g, dirtyColor.b, _cleanColors[i].a);
                _renderers[i].color = Color.Lerp(_cleanColors[i], dirty, t);
            }
        }
    }
}
