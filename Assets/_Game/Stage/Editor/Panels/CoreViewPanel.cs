using MS2026.Fortress.Cameras;
using MS2026.Fortress.EditorTools;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>コアのページ: Game ビューを「Play 中と同じ見え方」（全体 / 1P〜4P）にして、見比べながら置き方を決める。</summary>
    public static class CoreViewPanel
    {
        private static readonly (int value, string icon, string label, string tooltip)[] Options =
        {
            (StageGameViewPreview.Off, "○", "見ない", "Game ビューを元のカメラに戻します。"),
            (StageGameViewPreview.Overview, "◎", "全体", "観戦・プレイヤーが決まっていないときの視点（Game シーンからそのまま Play したときもこれ）。"),
            (0, "1", "1P", "1P の PC の画面。"),
            (1, "2", "2P", "2P の PC の画面。"),
            (2, "3", "3P", "3P の PC の画面。"),
            (3, "4", "4P", "4P の PC の画面。"),
        };

        public static VisualElement Build()
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("ゲームと同じ見え方で見る", "Game ビューを Play 中と同じ見え方にします。見るだけで、シーンには残りません。"));
            var note = StudioUi.Note("", NoteKind.Info);
            System.Action refresh = null;
            var playChip = StudioUi.Chip("", ChipKind.Info);
            var sticky = new Toggle("ふつうの Play でも、この席で見る")
            {
                tooltip = "ON の間は、▶ ボタン（Ctrl+P）で Play しても、上で選んだ席（1P〜4P）の視点で始めます（エディタだけ。Unity を閉じると OFF に戻ります）。" +
                          "要塞デザイナー › カメラ の「この視点でプレイ」の固定と同じスイッチです。"
            };
            var playButton = StudioUi.Button("▶ この席でプレイ", () =>
            {
                var seat = StageGameViewPreview.Viewer;
                if (IsSeat(seat))
                {
                    CameraPlayAsViewer.PlayAs(seat);
                }
            }, "上で選んだ席（1P〜4P）のプレイヤーの画面で Play を始めます。", primary: true);

            var segment = StudioUi.Segment(Options, () => StageGameViewPreview.Viewer, viewer =>
            {
                var ok = StageGameViewPreview.Show(viewer);
                UpdateNote(note, ok);
                if (CameraPlayAsViewer.Sticky && IsSeat(viewer))
                {
                    CameraPlayAsViewer.StickyViewer = viewer;
                }

                refresh?.Invoke();
                UpdatePlay(playChip, sticky, playButton);
            }, out refresh);
            card.Add(segment);

            sticky.RegisterValueChangedCallback(e =>
            {
                CameraPlayAsViewer.Sticky = e.newValue;
                if (e.newValue && IsSeat(StageGameViewPreview.Viewer))
                {
                    CameraPlayAsViewer.StickyViewer = StageGameViewPreview.Viewer;
                }

                UpdatePlay(playChip, sticky, playButton);
            });
            card.Add(StudioUi.Row(playButton, StudioUi.Spacer(), playChip));
            card.Add(sticky);
            card.Add(note);
            UpdateNote(note, true);
            UpdatePlay(playChip, sticky, playButton);

            // Play・保存・スクリプトの読み直しで自動で戻ったときも、選んでいる表示を合わせる。
            card.schedule.Execute(() =>
            {
                refresh();
                UpdatePlay(playChip, sticky, playButton);
            }).Every(500);
            return card;
        }

        private static bool IsSeat(int viewer) => viewer >= 0 && viewer < ViewerIndex.PlayerCount;

        // 次に ▶ で Play したときにどの視点になるかを見せる（Game シーンからそのまま Play すると「全体」になるので、1P の見え方とは違う）。
        private static void UpdatePlay(Label chip, Toggle sticky, Button playButton)
        {
            sticky.SetValueWithoutNotify(CameraPlayAsViewer.Sticky);
            playButton.SetEnabled(IsSeat(StageGameViewPreview.Viewer) && !EditorApplication.isPlayingOrWillChangePlaymode);
            var next = CameraPlayAsViewer.Sticky && IsSeat(CameraPlayAsViewer.StickyViewer) ? $"{CameraPlayAsViewer.StickyViewer + 1}P" : "全体";
            chip.text = $"ふつうの Play の視点：{next}";
            StudioUi.SetChipKind(chip, CameraPlayAsViewer.Sticky ? ChipKind.Ok : ChipKind.Warn);
        }

        private static void UpdateNote(VisualElement note, bool ok)
        {
            if (!ok)
            {
                StudioUi.SetNote(note, "見え方を切り替えられませんでした。カメラのリグか見え方プリセットが見つからないか、Play 中です。", NoteKind.Warn);
                return;
            }

            StudioUi.SetNote(note,
                "Game ビューのカメラをその席の目線に動かし、光のにじみ（Bloom）とステージの物のトゥーンも Play 中と同じにします。" +
                "4人は違う向きから見るので、1P〜4P を切り替えて、どの席でも変に見えないか確かめてください。" +
                "Game シーンからそのまま Play すると「全体」の視点（真上から）になり、1P の席（斜めから）とは歯の見え方が変わります。" +
                "1P の見え方で Play するには「▶ この席でプレイ」か、下のスイッチを使ってください（展示の本番は、各PCが自分の席の視点です）。" +
                "保存や Play の前には自動でカメラを元に戻します（カメラの位置は保存されません）。靄・環境の粒など Play 中だけの演出は出ません。",
                NoteKind.Info);
        }
    }
}
