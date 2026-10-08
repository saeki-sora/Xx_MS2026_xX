using System;
using System.Collections.Generic;
using DDrive.Runtime.Ui;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>選んだ部品の「動き」の一覧（きっかけ・動き・長さ・遅れ・効果音…）。行ごとに ▶ で試せる。</summary>
    public sealed class MotionEntriesPanel : VisualElement
    {
        private static readonly List<UiMotionTrigger> Triggers = new List<UiMotionTrigger>((UiMotionTrigger[])Enum.GetValues(typeof(UiMotionTrigger)));
        private static readonly List<UiPreset> Presets = new List<UiPreset>((UiPreset[])Enum.GetValues(typeof(UiPreset)));
        private static readonly List<UiMotionSource> Sources = new List<UiMotionSource> { UiMotionSource.Preset, UiMotionSource.Tween };

        private readonly UiStudioContext _context;
        private int _signature;

        public MotionEntriesPanel(UiStudioContext context)
        {
            _context = context;
        }

        /// <summary>選んだ部品に動きを1つ足す（動きの部品が無ければ付ける）。</summary>
        public static void AddEntry(RectTransform target, UiMotionTrigger trigger, UiPreset preset)
        {
            var motion = target.GetComponent<UiElementMotion>();
            if (motion == null)
            {
                motion = Undo.AddComponent<UiElementMotion>(target.gameObject);
            }

            Undo.RecordObject(motion, "動きを足す");
            motion.entries.Add(new UiElementMotion.Entry { trigger = trigger, motion = UiMotion.FromPreset(preset) });
            EditorUtility.SetDirty(motion);
        }

        public void Refresh()
        {
            var target = _context.SelectedElement;
            var motion = target != null ? target.GetComponent<UiElementMotion>() : null;
            var signature = Signature(target, motion);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            Clear();
            this.Unbind();
            if (target == null)
            {
                Add(_context.EditingScreen == null
                    ? UiPageBase.NoEditingScreen()
                    : StudioUi.Empty("◎", "部品を選んでください", "「レイヤー」ページかシーンビューで部品を選ぶと、ここにその部品の動きが出ます。"));
                return;
            }

            Add(StudioUi.Styled(new Label($"「{target.name}」の動き"), "sk-card-title"));
            if (motion == null || motion.entries.Count == 0)
            {
                Add(StudioUi.Note("まだ動きがありません。下の「定番の動き」で試して付けるか、ボタンで足してください。", NoteKind.Info));
            }

            if (motion != null)
            {
                var so = new SerializedObject(motion);
                var entries = so.FindProperty("entries");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    Add(BuildEntry(so, entries, i, motion, target));
                }

                this.Bind(so);
            }

            Add(StudioUi.Row(StudioUi.Button("＋ 動きを足す", () =>
            {
                AddEntry(target, UiMotionTrigger.OnShow, UiPreset.PopIn);
                _signature = 0;
            }, "この部品に動きを1つ足します（最初は「出たとき・ポンと出る」）。", small: true)));
        }

        private VisualElement BuildEntry(SerializedObject so, SerializedProperty entries, int index, UiElementMotion motion, RectTransform target)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            var trigger = (UiMotionTrigger)entry.FindPropertyRelative("trigger").enumValueIndex;
            var source = (UiMotionSource)entry.FindPropertyRelative("motion.source").enumValueIndex;
            var card = StudioUi.Card();

            var triggerField = new PopupField<UiMotionTrigger>("きっかけ", Triggers, trigger, t => t.DisplayName(), t => t.DisplayName())
            {
                tooltip = "いつ動くか。"
            };
            triggerField.RegisterValueChangedCallback(e => SetEnum(so, entry.FindPropertyRelative("trigger"), (int)e.newValue));
            card.Add(triggerField);

            var sourceField = new PopupField<UiMotionSource>("動きの種類", Sources, source, s => s == UiMotionSource.Preset ? "定番の動き" : "UI Tween で作った動き", s => s == UiMotionSource.Preset ? "定番の動き" : "UI Tween で作った動き")
            {
                tooltip = "定番の動き（59種）から選ぶか、D-Drive の UI Tween エディタで作った動きを使うか。"
            };
            sourceField.RegisterValueChangedCallback(e => SetEnum(so, entry.FindPropertyRelative("motion.source"), (int)e.newValue));
            card.Add(sourceField);

            if (source == UiMotionSource.Preset)
            {
                var presetProperty = entry.FindPropertyRelative("motion.preset.Preset");
                var preset = (UiPreset)presetProperty.enumValueIndex;
                var presetField = new PopupField<UiPreset>("動き", Presets, preset, UiPresetNames.Label, p => $"【{UiPresetNames.GroupLabel(UiPresetNames.GroupOf(p))}】{UiPresetNames.Label(p)}")
                {
                    tooltip = "定番の動き。下の「定番の動き」の一覧で、クリックして見比べられます。"
                };
                presetField.RegisterValueChangedCallback(e => SetEnum(so, presetProperty, (int)e.newValue));
                card.Add(presetField);
                card.Add(Field(entry, "motion.preset.Duration", "長さ（秒・0=標準）"));
                card.Add(Field(entry, "motion.preset.Distance", "強さ・距離（0=標準）"));
            }
            else
            {
                card.Add(Field(entry, "motion.tween", "UI Tween"));
            }

            card.Add(Field(entry, "motion.delay", "遅らせる（秒）"));
            card.Add(Field(entry, "motion.se", "効果音"));
            if (trigger == UiMotionTrigger.OnValueUp || trigger == UiMotionTrigger.OnValueDown)
            {
                card.Add(Field(entry, "valueKey", "見る値"));
            }

            if (trigger == UiMotionTrigger.Manual)
            {
                card.Add(Field(entry, "name", "名前"));
            }

            card.Add(Field(entry, "target", "動かす部品（空=この部品）"));

            var index2 = index;
            card.Add(StudioUi.Row(
                StudioUi.Button("▶ 試す", () =>
                {
                    var data = motion.entries[index2];
                    UiMotionPreview.Play(data.motion, data.target != null ? data.target : target);
                }, "この動きをその場で再生します（Playしていなくても動きます）。", primary: true, small: true),
                StudioUi.Button("消す", () =>
                {
                    Undo.RecordObject(motion, "動きを消す");
                    motion.entries.RemoveAt(index2);
                    EditorUtility.SetDirty(motion);
                    _signature = 0;
                }, "この動きを消します。", small: true)));
            return card;
        }

        private void SetEnum(SerializedObject so, SerializedProperty property, int value)
        {
            so.Update();
            property.enumValueIndex = value;
            so.ApplyModifiedProperties();
            _signature = 0;
        }

        private static PropertyField Field(SerializedProperty entry, string path, string label)
        {
            var property = entry.FindPropertyRelative(path);
            return new PropertyField(property, label) { tooltip = property?.tooltip };
        }

        private static int Signature(RectTransform target, UiElementMotion motion)
        {
            unchecked
            {
                var hash = target != null ? target.GetInstanceID() : 0;
                if (motion == null)
                {
                    return hash;
                }

                hash = hash * 31 + motion.entries.Count;
                foreach (var entry in motion.entries)
                {
                    hash = hash * 31 + (int)entry.trigger * 7 + (int)entry.motion.source;
                }

                return hash;
            }
        }
    }
}
