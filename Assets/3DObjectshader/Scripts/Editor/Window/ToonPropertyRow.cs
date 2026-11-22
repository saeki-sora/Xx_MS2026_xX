using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 項目1つの行: 名前・部品（スライダー／色／スイッチ）・↺（初期値に戻す）。
    /// 初期値から変えた行は名前を青くして印を付ける。親スイッチが切れている行は薄くする（触ることはできる）。
    /// 説明は tooltip に入れて、窓の下の帯に出す。
    /// </summary>
    public sealed class ToonPropertyRow : VisualElement
    {
        private readonly ToonContext _context;
        private readonly Label _label;
        private readonly Button _reset;
        private Slider _slider;
        private FloatField _number;
        private ColorField _color;
        private Toggle _toggle;
        private bool _isInt;

        public ToonItem Item { get; }

        public bool Changed { get; private set; }

        public ToonPropertyRow(ToonContext context, ToonItem item, Material sample)
        {
            _context = context;
            Item = item;
            AddToClassList("tw-row");
            tooltip = item.Label + "：" + item.Tip;

            _label = new Label(item.Label);
            _label.AddToClassList("tw-row-label");
            Add(_label);

            var control = new VisualElement();
            control.AddToClassList("tw-row-control");
            Add(control);
            BuildControl(control, sample);

            _reset = new Button(Reset) { text = "↺", tooltip = "初期値に戻す（物ごとの調整中は、共通の設定の値に戻す）" };
            _reset.AddToClassList("tw-row-reset");
            Add(_reset);
        }

        private void BuildControl(VisualElement control, Material sample)
        {
            if (IsToggle)
            {
                _toggle = new Toggle();
                _toggle.AddToClassList("tw-toggle");
                _toggle.RegisterValueChangedCallback(e => _context.Write(m => ToonValues.SetToggle(m, Item, e.newValue), Item.Label));
                control.Add(_toggle);
                return;
            }

            if (sample != null && ToonValues.IsColor(sample, Item))
            {
                _color = new ColorField { showAlpha = false, hdr = ToonValues.IsHdr(sample, Item), showEyeDropper = true };
                _color.AddToClassList("tw-color");
                _color.RegisterValueChangedCallback(e => _context.Write(m => m.SetColor(Item.Id, e.newValue), Item.Label));
                control.Add(_color);
                return;
            }

            var range = sample != null ? ToonValues.Range(sample, Item) : new Vector2(0f, 1f);
            _isInt = sample != null && ToonValues.IsInt(sample, Item);
            _slider = new Slider(range.x, range.y);
            _slider.AddToClassList("tw-slider");
            _slider.RegisterValueChangedCallback(e => SetFloat(e.newValue));
            _number = new FloatField();
            _number.AddToClassList("tw-number");
            _number.RegisterValueChangedCallback(e => SetFloat(e.newValue));
            control.Add(_slider);
            control.Add(_number);
        }

        private bool IsToggle => Item.Kind == ToonKind.Toggle || Item.Kind == ToonKind.TwoSided;

        private void SetFloat(float value)
        {
            if (_isInt)
            {
                value = Mathf.Round(value);
            }

            _context.Write(m => m.SetFloat(Item.Id, value), Item.Label);
        }

        private void Reset()
        {
            var baseline = _context.Baseline;
            _context.Write(m => ToonValues.Reset(m, baseline, Item), Item.Label + "を戻す");
        }

        /// <summary>材質の今の値を読み直し、変えた所の印・薄さを合わせる。</summary>
        public void Pull()
        {
            var material = _context.Target;
            bool has = ToonValues.Has(material, Item);
            EnableInClassList("tw-row--missing", !has);
            if (!has)
            {
                Changed = false;
                return;
            }

            if (_toggle != null)
            {
                _toggle.SetValueWithoutNotify(ToonValues.GetToggle(material, Item));
            }
            else if (_color != null)
            {
                _color.SetValueWithoutNotify(material.GetColor(Item.Id));
            }
            else
            {
                var value = material.GetFloat(Item.Id);
                _slider.SetValueWithoutNotify(value);
                _number.SetValueWithoutNotify((float)System.Math.Round(value, _isInt ? 0 : 3));
            }

            Changed = ToonValues.IsChanged(material, _context.Baseline, Item);
            EnableInClassList("tw-row--changed", Changed);
            _reset.visible = Changed;

            bool off = !string.IsNullOrEmpty(Item.Parent) && material.HasProperty(Item.Parent) && material.GetFloat(Item.Parent) < 0.5f;
            EnableInClassList("tw-row--off", off);
            if (off)
            {
                tooltip = Item.Label + "：" + Item.Tip + "（今は親のスイッチが切れているので効いていません）";
            }
            else
            {
                tooltip = Item.Label + "：" + Item.Tip;
            }
        }
    }
}
