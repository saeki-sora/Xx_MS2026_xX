using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 動きページの「この画面の動きの一覧」。編集中の画面の中の全部の動きを、きっかけごとに遅れの小さい順で並べる
    /// （どの部品がどんな順番で出てくるかが一目で分かる）。行をクリックでその部品を選び、▶ でその場で試す。
    /// </summary>
    public sealed class ScreenMotionOverviewPanel : VisualElement
    {
        private readonly UiStudioContext _context;
        private int _signature = -1;

        public ScreenMotionOverviewPanel(UiStudioContext context)
        {
            _context = context;
        }

        public void Refresh()
        {
            var screen = _context.EditingScreen;
            var signature = Signature(screen);
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            Rebuild(screen);
        }

        private void Rebuild(UiScreen screen)
        {
            Clear();
            if (screen == null)
            {
                Add(StudioUi.Styled(new Label("編集中の画面がありません。"), "sk-muted"));
                return;
            }

            var rows = new List<(UiMotionTrigger trigger, float delay, UiElementMotion owner, UiElementMotion.Entry entry)>();
            foreach (var motion in screen.GetComponentsInChildren<UiElementMotion>(true))
            {
                foreach (var entry in motion.entries)
                {
                    if (entry != null)
                    {
                        rows.Add((entry.trigger, entry.motion.delay, motion, entry));
                    }
                }
            }

            if (rows.Count == 0)
            {
                Add(StudioUi.Styled(new Label("この画面の部品には、まだ動きがありません。部品を選んで上の「定番の動き」から付けられます。"), "sk-muted"));
                return;
            }

            rows.Sort((a, b) => a.trigger != b.trigger ? a.trigger.CompareTo(b.trigger) : a.delay.CompareTo(b.delay));
            var current = (UiMotionTrigger)(-1);
            foreach (var row in rows)
            {
                if (row.trigger != current)
                {
                    current = row.trigger;
                    var header = StudioUi.Styled(new Label(current.DisplayName()), "sk-section-title");
                    header.style.marginTop = 6;
                    Add(header);
                }

                Add(BuildRow(row.owner, row.entry));
            }
        }

        private VisualElement BuildRow(UiElementMotion owner, UiElementMotion.Entry entry)
        {
            var target = entry.target != null ? entry.target : (RectTransform)owner.transform;
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.EnableInClassList("sk-list-item--selected", Selection.activeGameObject == owner.gameObject);
            row.tooltip = "クリックでこの部品を選びます（上の「選んだ部品の動き」で中身を変えられます）。";

            var name = StudioUi.Styled(new Label(owner.name), "sk-list-label");
            row.Add(name);
            row.Add(StudioUi.Styled(new Label(Describe(entry, target, owner)), "sk-list-meta"));
            var play = StudioUi.Button("▶", () => UiMotionPreview.Play(entry.motion, target), "この動きをその場で試します。", small: true);
            row.Add(play);
            row.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button)
                {
                    return;
                }

                Selection.activeGameObject = owner.gameObject;
                _signature = -1;
            });
            return row;
        }

        private static string Describe(UiElementMotion.Entry entry, RectTransform target, UiElementMotion owner)
        {
            var motion = entry.motion;
            var what = motion.IsEmpty ? "（なし）"
                : motion.source == UiMotionSource.Preset ? UiPresetNames.Name(motion.preset.Preset)
                : "UI Tween";
            var delay = motion.delay > 0f ? $"  {motion.delay:0.##}秒後" : "";
            var other = target != null && target.gameObject != owner.gameObject ? $"  →「{target.name}」を動かす" : "";
            var key = (entry.trigger == UiMotionTrigger.OnValueUp || entry.trigger == UiMotionTrigger.OnValueDown) && !string.IsNullOrEmpty(entry.valueKey) ? $"  ［{entry.valueKey}］" : "";
            var called = entry.trigger == UiMotionTrigger.Manual && !string.IsNullOrEmpty(entry.name) ? $"  名前「{entry.name}」" : "";
            return what + delay + key + called + other;
        }

        private static int Signature(UiScreen screen)
        {
            if (screen == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = screen.GetInstanceID();
                foreach (var motion in screen.GetComponentsInChildren<UiElementMotion>(true))
                {
                    hash = hash * 31 + motion.GetInstanceID();
                    foreach (var entry in motion.entries)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        hash = hash * 31 + (int)entry.trigger;
                        hash = hash * 31 + entry.motion.delay.GetHashCode();
                        hash = hash * 31 + (int)entry.motion.source * 7 + (int)entry.motion.preset.Preset;
                    }
                }

                return hash + (Selection.activeGameObject != null ? Selection.activeGameObject.GetInstanceID() : 0);
            }
        }
    }
}
