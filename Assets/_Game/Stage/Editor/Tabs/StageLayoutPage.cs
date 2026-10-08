using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置: ゲームの画面（P1〜P4・全体、または4人並べて）を見ながら、その画面の中で背景を直接つかんで動かす。
    /// 右の欄で向き・大きさ・高さを細かく合わせ、下の一覧からも選べる。シーンビュー側の表示（目線の固定・ずれ・配置ツール）もここで切り替える。
    /// 部品: LayoutToolbar（上の帯）／StagePreviewView（ゲーム画面）／PropAdjustPanel（右の欄）／PropListPanel（一覧）。
    /// </summary>
    public sealed class StageLayoutPage : StagePageBase
    {
        private VisualElement _notice;
        private VisualElement _body;
        private LayoutToolbar _toolbar;
        private VisualElement _previews;
        private PropAdjustPanel _adjust;
        private PropListPanel _list;
        private readonly List<(Label chip, System.Func<bool> isOn)> _sceneToggles = new List<(Label, System.Func<bool>)>();
        private Button _lockButton;
        private bool _builtQuad;
        private bool _previewsBuilt;

        public StageLayoutPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "✥";
        public override string Label => "配置";
        public override string Tooltip => "ゲームの画面を見ながら、背景をつかんで動かし、向き・大きさ・高さを合わせます。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("配置"));
            root.Add(StudioUi.Lead("ゲームの画面の中で、背景をクリックして選び、そのままドラッグで動かします。ホイールで回す、Shift+ホイールで大きさ、Alt+ホイールで高さ。右の欄では名前を左右にドラッグして細かく合わせられます。"));
            _notice = new VisualElement();
            root.Add(_notice);

            _body = new VisualElement();
            root.Add(_body);

            _toolbar = new LayoutToolbar(RebuildPreviews);
            _body.Add(_toolbar);

            var main = StudioUi.Styled(new VisualElement(), "sk-row");
            main.style.alignItems = Align.FlexStart;
            main.style.flexWrap = Wrap.Wrap;
            main.style.marginTop = 6;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            left.style.flexBasis = 380;
            left.style.minWidth = 340;
            left.style.marginRight = 12;
            _previews = new VisualElement();
            left.Add(_previews);
            left.Add(KeyHelp());
            main.Add(left);

            var right = new VisualElement();
            right.style.width = 300;
            right.style.flexShrink = 0;
            _adjust = new PropAdjustPanel(Context, StageStudioWindow.OpenPage<StagePropsPage>);
            right.Add(_adjust);
            right.Add(StudioUi.Section("背景の一覧", "クリックで選びます（ゲーム画面で隠れて選びにくい物もここから）。Ctrl / Shift で複数。"));
            _list = new PropListPanel(Context);
            _list.style.width = StyleKeyword.Auto;
            _list.style.marginRight = 0;
            right.Add(_list);
            main.Add(right);
            _body.Add(main);

            _body.Add(StudioUi.Section("シーンビュー", "Unity のシーンビュー側でも、同じように背景をつかんで動かせます。その表示をここで切り替えます。"));
            _body.Add(SceneOptions());
            _previewsBuilt = false;
            return root;
        }

        public override void Refresh()
        {
            var missing = UpdateMissingNotice(_notice);
            _body.style.display = missing ? DisplayStyle.None : DisplayStyle.Flex;
            if (missing)
            {
                return;
            }

            if (!_previewsBuilt || _builtQuad != StagePlaceSettings.PreviewQuad)
            {
                RebuildPreviews();
            }

            _toolbar.Refresh();
            _adjust.Refresh();
            _list.Refresh();
            RefreshSceneOptions();
        }

        private void RebuildPreviews()
        {
            _previewsBuilt = true;
            _builtQuad = StagePlaceSettings.PreviewQuad;
            _previews.Clear();
            if (!_builtQuad)
            {
                _previews.Add(new StagePreviewView(Context, () => StagePlaceSettings.PreviewViewer));
                return;
            }

            for (var row = 0; row < 2; row++)
            {
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                for (var col = 0; col < 2; col++)
                {
                    var viewer = row * 2 + col;
                    var view = new StagePreviewView(Context, () => viewer);
                    view.style.flexBasis = 0;
                    view.style.marginRight = col == 0 ? 6 : 0;
                    line.Add(view);
                }

                _previews.Add(line);
            }
        }

        private static VisualElement KeyHelp()
        {
            var text = "クリック: 選ぶ（Shift で追加・外す）　ドラッグ: 動かす（Ctrl で吸着が逆）　ホイール: 回す　Shift+ホイール: 大きさ　Alt+ホイール: 高さ\n" +
                       "矢印: 少しずらす（Shift で細かく）　Q / E: 回す　- / +: 大きさ　PageUp / PageDown: 高さ　F: シーンビューに映す　Esc: やめる・選択を外す　Ctrl+Z: 元に戻す";
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 10;
            label.style.color = new Color(0.62f, 0.64f, 0.68f);
            label.style.marginBottom = 8;
            return label;
        }

        // ── シーンビュー側の表示 ─────────────────
        private VisualElement SceneOptions()
        {
            var root = new VisualElement();

            var lockRow = StudioUi.Row();
            _lockButton = StudioUi.Button("シーンビューをこの目線にする", ToggleLock,
                "シーンビューを、今ゲーム画面で見ている視点のゲームのカメラと同じ見え方に固定します（4人並べているときは P1）。シーンビューを回す・ずらすと外れます。", small: true);
            lockRow.Add(_lockButton);
            lockRow.Add(SceneToggle("背景を選んだら配置ツールにする", "シーンビューで背景を選んだら、自動で「ステージ配置」ツール（つかんで動かす・輪で回す・四角で大きさ・↕で高さ）にします。",
                () => StagePlaceSettings.AutoTool, () => StagePlaceSettings.AutoTool = !StagePlaceSettings.AutoTool));
            root.Add(lockRow);

            var parallax = StudioUi.Row();
            parallax.style.marginTop = 4;
            var caption = new Label("見た目のずれをシーンビューに出す") { tooltip = "選んだ視点から見て、背景が床のどこに重なって見えるかを、その視点の色の影と矢印でシーンビューに描きます。" };
            caption.style.color = new Color(0.62f, 0.64f, 0.68f);
            caption.style.marginRight = 6;
            parallax.Add(caption);
            parallax.Add(SceneToggle("なし", "シーンビューにずれを出しません。",
                () => StagePlaceSettings.ParallaxViewer == StagePlaceSettings.ParallaxOff, () => StagePlaceSettings.ParallaxViewer = StagePlaceSettings.ParallaxOff));
            foreach (var viewer in ViewerIndex.All)
            {
                var v = viewer;
                var chip = SceneToggle(ViewerIndex.Label(v), $"{ViewerIndex.LongLabel(v)}から見たずれを出します。",
                    () => StagePlaceSettings.ParallaxViewer == v, () => StagePlaceSettings.ParallaxViewer = v);
                if (ViewerIndex.IsPlayer(v))
                {
                    chip.style.color = ViewerIndex.Color(v);
                }

                parallax.Add(chip);
            }

            parallax.Add(SceneToggle("選んでいない物にも", "選んでいる物だけでなく、全部の背景にずれを出します。",
                () => StagePlaceSettings.ParallaxForAll, () => StagePlaceSettings.ParallaxForAll = !StagePlaceSettings.ParallaxForAll));
            root.Add(parallax);
            return root;
        }

        private Label SceneToggle(string text, string tooltip, System.Func<bool> isOn, System.Action onClick)
        {
            var chip = StudioUi.Chip(text, ChipKind.Plain, tooltip);
            chip.style.marginBottom = 4;
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                onClick();
                RefreshSceneOptions();
                SceneView.RepaintAll();
            });
            _sceneToggles.Add((chip, isOn));
            return chip;
        }

        private void RefreshSceneOptions()
        {
            foreach (var (chip, isOn) in _sceneToggles)
            {
                var on = isOn();
                StudioUi.SetChipKind(chip, on ? ChipKind.Accent : ChipKind.Plain);
                chip.style.opacity = on ? 1f : 0.75f;
            }

            _lockButton.text = StageSceneViewSync.IsLocked
                ? $"シーンビューの固定を外す（今: {ViewerIndex.Label(StageSceneViewSync.LockedViewer)}）"
                : "シーンビューをこの目線にする";
        }

        private void ToggleLock()
        {
            if (StageSceneViewSync.IsLocked)
            {
                StageSceneViewSync.Unlock();
            }
            else
            {
                StageSceneViewSync.Lock(StagePlaceSettings.PreviewQuad ? 0 : StagePlaceSettings.PreviewViewer);
            }

            RefreshSceneOptions();
        }
    }
}
