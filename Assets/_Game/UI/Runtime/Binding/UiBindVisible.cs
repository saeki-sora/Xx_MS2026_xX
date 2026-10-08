using UnityEngine;

namespace MS2026.UI
{
    public enum UiVisibleCondition
    {
        /// <summary>ONのとき（数なら0.5より大きい）。</summary>
        WhenOn,

        /// <summary>OFFのとき。</summary>
        WhenOff,

        /// <summary>数がしきい値より大きいとき。</summary>
        WhenGreater,

        /// <summary>数がしきい値より小さいとき。</summary>
        WhenLess,

        /// <summary>数がしきい値と同じとき（例: プレイヤー番号が2のとき）。</summary>
        WhenEqual
    }

    /// <summary>
    /// 値によって表示・非表示を切り替える（例: オーバーヒート中だけ「OVERHEAT」を出す）。
    /// 消すときは透明にして押せなくする（GameObjectは消さないので、この部品自体は動き続ける）。
    /// 「動き」の部品（UiElementMotion）が付いていれば、出す／消すときにその動きも再生する。
    /// </summary>
    [AddComponentMenu("UI Studio/値で表示を切り替える (UiBindVisible)")]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiBindVisible : UiBinding
    {
        [Tooltip("いつ表示するか。")]
        public UiVisibleCondition condition = UiVisibleCondition.WhenOn;

        [Tooltip("「より大きい／より小さい／同じ」のときに比べる数。")]
        public float threshold = 0.5f;

        /// <summary>編集中に「隠れている」状態を示す薄さ。</summary>
        public const float EditorHiddenAlpha = 0.25f;

        private CanvasGroup _group;
        private UiElementMotion _motion;
        private bool? _visible;

        /// <summary>今表示されているか。</summary>
        public bool IsVisible => _visible ?? true;

        protected override void OnEnable()
        {
            _group = GetComponent<CanvasGroup>();
            _motion = GetComponent<UiElementMotion>();
            base.OnEnable();
        }

        protected override void Apply(UiValue value)
        {
            var visible = Evaluate(value);
            if (_visible == visible)
            {
                return;
            }

            var first = _visible == null;
            _visible = visible;
            if (_motion != null && Application.isPlaying && !first)
            {
                if (visible)
                {
                    SetShown(true);
                    _motion.Play(UiMotionTrigger.OnShow);
                }
                else
                {
                    _motion.Play(UiMotionTrigger.OnHide, () => SetShown(false));
                }

                return;
            }

            SetShown(visible);
        }

        public bool Evaluate(UiValue value) => condition switch
        {
            UiVisibleCondition.WhenOff => !value.AsBool,
            UiVisibleCondition.WhenGreater => value.AsNumber > threshold,
            UiVisibleCondition.WhenLess => value.AsNumber < threshold,
            UiVisibleCondition.WhenEqual => Mathf.Abs(value.AsNumber - threshold) < 0.001f,
            _ => value.AsBool
        };

        private void SetShown(bool shown)
        {
            if (_group == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                // 編集中は完全には消さず薄く見せる（隠れている状態でも位置や見た目を直せるように）。
                _group.alpha = shown ? 1f : EditorHiddenAlpha;
                return;
            }

            _group.alpha = shown ? 1f : 0f;
            _group.blocksRaycasts = shown;
            _group.interactable = shown;
        }
    }
}
