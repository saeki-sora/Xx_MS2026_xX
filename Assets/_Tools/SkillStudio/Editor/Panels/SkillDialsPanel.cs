using System.Reflection;
using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 「演出の付け外し・強さ」の表。演出1つ = 1行（スイッチ・名前・強さのメーター・数字）。
    /// 中身が全部 <see cref="SkillEffectDial"/> のまとまりなら、どのスキルでもこの表で出る（今は赤の極太レーザー）。
    /// まとめて「全部出す」「全部標準に戻す」「全部消す」もできる。
    /// </summary>
    public sealed class SkillDialsPanel : VisualElement
    {
        private readonly SerializedObject _serializedObject;
        private readonly FieldInfo _group;

        public SkillDialsPanel(SerializedObject serializedObject, FieldInfo group)
        {
            _serializedObject = serializedObject;
            _group = group;

            var card = StudioUi.Card(SkillTabs.LabelOf(group),
                "演出ごとに、出す／出さないと強さ（1 = 標準、2 = 2倍）を決めます。細かい色や数は下のそれぞれの欄で調整します。");
            card.tooltip = SkillTabs.TooltipOf(group);
            card.style.marginBottom = 10;
            Add(card);

            var buttons = StudioUi.Row(
                StudioUi.Button("全部出す", () => SetAll(true, null), "すべての演出を出す（強さはそのまま）。", small: true),
                StudioUi.Button("全部標準の強さに", () => SetAll(true, 1f), "すべての演出を出して、強さを 1（標準）に戻す。", small: true),
                StudioUi.Button("全部消す", () => SetAll(false, null), "すべての演出を出さない（強さはそのまま残る）。", small: true));
            buttons.style.marginTop = 6;
            buttons.style.marginBottom = 6;
            card.Add(buttons);

            var groupProperty = serializedObject.FindProperty(group.Name);
            foreach (var field in group.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType != typeof(SkillEffectDial))
                {
                    continue;
                }

                var header = field.GetCustomAttribute<HeaderAttribute>();
                if (header != null)
                {
                    var section = StudioUi.Section(header.header);
                    section.style.marginTop = 8;
                    card.Add(section);
                }

                var dial = groupProperty.FindPropertyRelative(field.Name);
                if (dial != null)
                {
                    card.Add(BuildRow(dial, SkillTabs.LabelOf(field), SkillTabs.TooltipOf(field)));
                }
            }
        }

        private static VisualElement BuildRow(SerializedProperty dial, string label, string tooltip)
        {
            var enabled = dial.FindPropertyRelative(nameof(SkillEffectDial.enabled));
            var amount = dial.FindPropertyRelative(nameof(SkillEffectDial.amount));

            var row = StudioUi.Row();
            row.tooltip = tooltip;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            var toggle = new Toggle { tooltip = "ON で出す、OFF で出さない。" };
            toggle.BindProperty(enabled);
            toggle.style.marginRight = 6;

            var name = new Label(label);
            name.style.width = 190;
            name.style.flexShrink = 0;

            var slider = new Slider(0f, SkillEffectDial.MaxAmount) { tooltip = tooltip };
            slider.BindProperty(amount);
            slider.style.flexGrow = 1;
            slider.style.minWidth = 120;

            var value = new FloatField { tooltip = "強さ（1 = 標準）。" };
            value.BindProperty(amount);
            value.style.width = 52;
            value.style.marginLeft = 6;

            void Refresh(bool on)
            {
                slider.SetEnabled(on);
                value.SetEnabled(on);
                name.style.opacity = on ? 1f : 0.5f;
            }

            toggle.RegisterValueChangedCallback(e => Refresh(e.newValue));
            row.schedule.Execute(() => Refresh(toggle.value));

            row.Add(toggle);
            row.Add(name);
            row.Add(slider);
            row.Add(value);
            return row;
        }

        private void SetAll(bool enabled, float? amount)
        {
            _serializedObject.Update();
            var group = _serializedObject.FindProperty(_group.Name);
            foreach (var field in _group.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType != typeof(SkillEffectDial))
                {
                    continue;
                }

                var dial = group.FindPropertyRelative(field.Name);
                dial.FindPropertyRelative(nameof(SkillEffectDial.enabled)).boolValue = enabled;
                if (amount.HasValue)
                {
                    dial.FindPropertyRelative(nameof(SkillEffectDial.amount)).floatValue = amount.Value;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }
    }
}
