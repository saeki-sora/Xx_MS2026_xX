using System.Collections.Generic;
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

        private static readonly List<UiElementMotion> s_motions = new List<UiElementMotion>();

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
            if (Application.isPlaying && !first)
            {
                if (visible)
                {
                    SetShown(true);
                    PlayShowInside();
                    return;
                }

                if (_motion != null)
                {
                    _motion.Play(UiMotionTrigger.OnHide, () => SetShown(false));
                    return;
                }
            }

            SetShown(visible);
        }

        /// <summary>
        /// t（とその親たち。root まで）の中に、値で今隠れている部品があるか。
        /// 画面を開くときは、隠れている部品の「出るとき」の動きを流さない（流すと透明のはずの部品が先に見えてしまい、
        /// 出番が来たときにもう一度出直して見えた。2026-10-14）。出番が来たら、この部品が中の動きをまとめて再生する。
        /// </summary>
        public static bool IsHiddenByValue(Transform t, Transform root)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.TryGetComponent<UiBindVisible>(out var bind) && bind.isActiveAndEnabled && !bind.IsVisible)
                {
                    return true;
                }

                if (p == root)
                {
                    break;
                }
            }

            return false;
        }

        // 自分と中の部品の「出るとき」の動きを再生する（画面を開いたとき、隠れていた部品の分は飛ばしているため）。
        private void PlayShowInside()
        {
            GetComponentsInChildren(false, s_motions);
            foreach (var motion in s_motions)
            {
                if (motion != null && !IsHiddenByValue(motion.transform, transform))
                {
                    motion.Play(UiMotionTrigger.OnShow);
                }
            }

            s_motions.Clear();
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
