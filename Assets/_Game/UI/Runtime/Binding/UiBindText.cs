using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// 値を文字で出す。書式で「{0:0}%」「残り {0:0.0} 秒」のように飾れる。数は「数え上げ」でなめらかに変えられる。
    /// 文字の部品は同じ GameObject の Text / TextMeshPro を自動で使う。
    /// </summary>
    [AddComponentMenu("UI Studio/値を文字で出す (UiBindText)")]
    public sealed class UiBindText : UiBinding
    {
        [Tooltip("書式。{0} が値に置き換わる。例: {0:0}% → 70%、HP {0:0}/100、{0} 人。空なら値そのまま。")]
        public string format = "{0}";

        [Tooltip("数が変わったとき、この秒数かけて数え上げる（0=すぐ変わる）。スコアなどに。")]
        [Min(0f)]
        public float countUpSeconds;

        [Tooltip("値がまだ無いときに出す文字（空なら今の文字のまま）。")]
        public string whenEmpty = "";

        private Graphic _target;
        private float _shown;
        private float _from;
        private float _to;
        private float _elapsed;
        private bool _counting;

        protected override void OnEnable()
        {
            _target = UiTextTarget.Find(gameObject);
            if (_target != null && !string.IsNullOrEmpty(whenEmpty))
            {
                UiTextTarget.Set(_target, whenEmpty);
            }

            base.OnEnable();
        }

        protected override void Apply(UiValue value)
        {
            if (_target == null)
            {
                _target = UiTextTarget.Find(gameObject);
                if (_target == null)
                {
                    return;
                }
            }

            if (countUpSeconds > 0f && value.Kind == UiValueKind.Number && Application.isPlaying)
            {
                _from = _shown;
                _to = value.Number;
                _elapsed = 0f;
                _counting = true;
                return;
            }

            _shown = value.Number;
            UiTextTarget.Set(_target, value.Format(format));
        }

        private void Update()
        {
            if (!_counting || _target == null)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / countUpSeconds);
            _shown = Mathf.Lerp(_from, _to, 1f - (1f - t) * (1f - t));
            UiTextTarget.Set(_target, UiValue.Of(t >= 1f ? _to : _shown).Format(format));
            _counting = t < 1f;
        }
    }
}
