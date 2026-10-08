using System;
using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置ページの上の帯: どの視点で見るか（全体・P1〜P4・4人並べる）、吸着（マス目・角度・端）、ゲーム画面に重ねる物（輪郭・距離）。
    /// 札（チップ）を押して切り替える。オンの札はオレンジ。設定はこのPCだけに保存される。
    /// </summary>
    public sealed class LayoutToolbar : VisualElement
    {
        private static readonly float[] GridSteps = { 0.1f, 0.25f, 0.5f, 1f };
        private static readonly float[] AngleSteps = { 5f, 15f, 45f, 90f };
        private static readonly float[] EdgeDistances = { 0f, 0.1f, 0.2f, 0.5f };

        private readonly List<(Label chip, Func<bool> isOn)> _toggles = new List<(Label, Func<bool>)>();
        private readonly Action _onLayoutChanged;

        public LayoutToolbar(Action onLayoutChanged)
        {
            _onLayoutChanged = onLayoutChanged;

            var view = StudioUi.Row();
            view.Add(Caption("視点"));
            foreach (var viewer in ViewerIndex.All)
            {
                var v = viewer;
                var chip = Toggle(ViewerIndex.Label(v), $"ゲーム画面を{ViewerIndex.LongLabel(v)}のカメラで映します。",
                    () => !StagePlaceSettings.PreviewQuad && StagePlaceSettings.PreviewViewer == v,
                    () =>
                    {
                        StagePlaceSettings.PreviewViewer = v;
                        StagePlaceSettings.PreviewQuad = false;
                        _onLayoutChanged();
                    });
                if (ViewerIndex.IsPlayer(v))
                {
                    chip.style.color = ViewerIndex.Color(v);
                }

                view.Add(chip);
            }

            view.Add(Toggle("4人並べる", "P1〜P4 の画面を2×2に並べます。どの画面でも背景をさわれます。",
                () => StagePlaceSettings.PreviewQuad,
                () =>
                {
                    StagePlaceSettings.PreviewQuad = !StagePlaceSettings.PreviewQuad;
                    _onLayoutChanged();
                }));
            view.Add(Gap());
            view.Add(Caption("重ねる"));
            view.Add(Toggle("輪郭", "ゲーム画面に、敵が通れない範囲の輪郭（壁=オレンジ、壊せる壁=紫、遅くなる地帯=水色）を重ねます。床の上の本当の位置なので、背の高い物は見た目とずれて見えます。",
                () => StagePlaceSettings.PreviewOutlines, () => StagePlaceSettings.PreviewOutlines = !StagePlaceSettings.PreviewOutlines));
            view.Add(Toggle("距離", "選んでいる背景から、近くの砲台・コア・湧き位置までの距離を出します（近すぎると赤）。",
                () => StagePlaceSettings.ShowDistances, () => StagePlaceSettings.ShowDistances = !StagePlaceSettings.ShowDistances));
            Add(view);

            var snap = StudioUi.Row();
            snap.style.marginTop = 4;
            snap.Add(Caption("吸着"));
            snap.Add(Toggle("オン", "動かす・回すときに、マス目・角度・他の物の端にぴたっとそろえます。Ctrl を押している間は逆になります。",
                () => StagePlaceSettings.Snap, () => StagePlaceSettings.Snap = !StagePlaceSettings.Snap));
            snap.Add(Gap());
            AddChoices(snap, "マス目", "動かしたとき根元が乗るマス目の大きさ。矢印キーで動く量もこれ。", GridSteps, v => $"{v:0.##}m",
                () => StagePlaceSettings.GridStep, v => StagePlaceSettings.GridStep = v);
            AddChoices(snap, "角度", "回すときの刻み（ホイール・Q/E キーの1回分も）。", AngleSteps, v => $"{v:0}°",
                () => StagePlaceSettings.AngleStep, v => StagePlaceSettings.AngleStep = v);
            AddChoices(snap, "端", "他の物の端にくっつく・そろう距離。", EdgeDistances, v => v <= 0f ? "なし" : $"{v:0.#}m",
                () => StagePlaceSettings.EdgeSnapDistance, v => StagePlaceSettings.EdgeSnapDistance = v);
            Add(snap);
        }

        public void Refresh()
        {
            foreach (var (chip, isOn) in _toggles)
            {
                var on = isOn();
                StudioUi.SetChipKind(chip, on ? ChipKind.Accent : ChipKind.Plain);
                chip.style.opacity = on ? 1f : 0.75f;
            }
        }

        private void AddChoices(VisualElement row, string caption, string tooltip, float[] options, Func<float, string> format, Func<float> get, Action<float> set)
        {
            row.Add(Caption(caption, tooltip));
            foreach (var option in options)
            {
                var value = option;
                row.Add(Toggle(format(value), tooltip, () => Mathf.Approximately(get(), value), () => set(value)));
            }

            row.Add(Gap());
        }

        private Label Toggle(string text, string tooltip, Func<bool> isOn, Action onClick)
        {
            var chip = StudioUi.Chip(text, ChipKind.Plain, tooltip);
            chip.style.marginBottom = 4;
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                onClick();
                Refresh();
                StageGameCamera.MarkDirty();
                SceneView.RepaintAll();
            });
            _toggles.Add((chip, isOn));
            return chip;
        }

        private static Label Caption(string text, string tooltip = null)
        {
            var label = new Label(text) { tooltip = tooltip };
            label.style.color = new Color(0.62f, 0.64f, 0.68f);
            label.style.marginRight = 6;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement Gap()
        {
            var gap = new VisualElement();
            gap.style.width = 14;
            return gap;
        }
    }
}
