using System;
using System.Collections.Generic;
using DDrive.Runtime.Ui;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// D-Drive の定番の動き（59種）を分類ごとに並べる。クリックで選んだ部品にその場で試し、
    /// 気に入ったら「付ける」で、選んだきっかけの動きとして足す。
    /// </summary>
    public sealed class PresetGalleryPanel : VisualElement
    {
        private static readonly List<UiMotionTrigger> Triggers = new List<UiMotionTrigger>((UiMotionTrigger[])Enum.GetValues(typeof(UiMotionTrigger)));

        private readonly UiStudioContext _context;
        private readonly Action _onAdded;
        private readonly Label _picked;
        private readonly PopupField<UiMotionTrigger> _trigger;
        private readonly Button _add;
        private UiPreset _last = UiPreset.None;

        public PresetGalleryPanel(UiStudioContext context, Action onAdded)
        {
            _context = context;
            _onAdded = onAdded;

            var bar = StudioUi.Card();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.flexWrap = Wrap.Wrap;
            _picked = StudioUi.Styled(new Label("下の動きをクリックすると、選んだ部品で試せます。"), "sk-card-body");
            _picked.style.flexGrow = 1;
            bar.Add(_picked);
            _trigger = new PopupField<UiMotionTrigger>(Triggers, UiMotionTrigger.OnShow, t => t.DisplayName(), t => t.DisplayName())
            {
                tooltip = "付けるときのきっかけ。"
            };
            _trigger.style.width = 150;
            bar.Add(_trigger);
            _add = StudioUi.Button("付ける", Add, "試した動きを、選んだきっかけで、選んだ部品に付けます。", primary: true, small: true);
            bar.Add(_add);
            Add(bar);

            foreach (UiPresetNames.Group group in Enum.GetValues(typeof(UiPresetNames.Group)))
            {
                Add(StudioUi.Styled(new Label(UiPresetNames.GroupLabel(group)), "sk-section-title"));
                var row = StudioUi.Row();
                row.style.marginBottom = 8;
                foreach (UiPreset preset in Enum.GetValues(typeof(UiPreset)))
                {
                    if (preset == UiPreset.None || UiPresetNames.GroupOf(preset) != group)
                    {
                        continue;
                    }

                    var p = preset;
                    var chip = StudioUi.Chip(UiPresetNames.Name(preset), ChipKind.Plain, $"{preset}: クリックで試す");
                    chip.style.marginBottom = 4;
                    chip.RegisterCallback<ClickEvent>(_ => Try(p));
                    row.Add(chip);
                }

                Add(row);
            }
        }

        public void Refresh()
        {
            _add.SetEnabled(_context.SelectedElement != null && _last != UiPreset.None);
        }

        private void Try(UiPreset preset)
        {
            _last = preset;
            _trigger.SetValueWithoutNotify(UiPresetNames.SuggestedTrigger(preset));
            var target = _context.SelectedElement;
            if (target == null)
            {
                _picked.text = $"「{UiPresetNames.Name(preset)}」を選びました。試すには先に部品を選んでください。";
                return;
            }

            _picked.text = $"「{UiPresetNames.Name(preset)}」（{preset}）を「{target.name}」で再生中";
            UiMotionPreview.Play(UiMotion.FromPreset(preset), target);
        }

        private void Add()
        {
            var target = _context.SelectedElement;
            if (target == null || _last == UiPreset.None)
            {
                return;
            }

            MotionEntriesPanel.AddEntry(target, _trigger.value, _last);
            _picked.text = $"「{target.name}」の「{_trigger.value.DisplayName()}」に「{UiPresetNames.Name(_last)}」を付けました。";
            _onAdded?.Invoke();
        }
    }
}
