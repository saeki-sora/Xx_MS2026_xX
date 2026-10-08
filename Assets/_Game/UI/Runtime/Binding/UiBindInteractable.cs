using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// 値によってボタンなどを押せる／押せない（灰色）に切り替える（例: 全員が準備OKのときだけ「ゲーム開始」を押せる）。
    /// 条件の決め方は「値で表示を切り替える」と同じ。
    /// </summary>
    [AddComponentMenu("UI Studio/値で押せる・押せないを切り替える (UiBindInteractable)")]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiBindInteractable : UiBinding
    {
        [Tooltip("いつ押せるようにするか。")]
        public UiVisibleCondition condition = UiVisibleCondition.WhenOn;

        [Tooltip("「より大きい／より小さい／同じ」のときに比べる数。")]
        public float threshold = 0.5f;

        private Selectable _target;

        protected override void OnEnable()
        {
            _target = GetComponent<Selectable>();
            base.OnEnable();
        }

        protected override void Apply(UiValue value)
        {
            if (_target == null)
            {
                return;
            }

            _target.interactable = condition switch
            {
                UiVisibleCondition.WhenOff => !value.AsBool,
                UiVisibleCondition.WhenGreater => value.AsNumber > threshold,
                UiVisibleCondition.WhenLess => value.AsNumber < threshold,
                UiVisibleCondition.WhenEqual => Mathf.Abs(value.AsNumber - threshold) < 0.001f,
                _ => value.AsBool
            };
        }
    }
}
