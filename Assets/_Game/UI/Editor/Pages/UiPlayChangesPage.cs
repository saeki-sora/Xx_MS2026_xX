using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play中の変更: Play 中に変えた UI の値の一覧。選んで Prefab／シーンに残すか、捨てる。
    /// 記録そのものは UiPlayChangeRecorder（このページを開いていなくても動く）。
    /// </summary>
    public sealed class UiPlayChangesPage : UiPageBase
    {
        private Toggle _record;
        private VisualElement _status;
        private Button _apply;
        private Label _result;
        private PlayChangeListPanel _list;

        public UiPlayChangesPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "◉";
        public override string Label => "Play中の変更";
        public override string Tooltip => "Play 中に変えた UI の値を一覧にし、止めたあとに Prefab やシーンへ残せます。";

        public override int Badge => UiPlayChangeStore.Count;
        public override bool BadgeIsWarning => true;

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("Play中の変更"));
            root.Add(StudioUi.Lead(
                "Unity は Play を止めると、Play 中に変えた値を元に戻します。UIスタジオは、Play 中に UI の部品を変えたら（インスペクター・シーンビュー・このスタジオのどこでも）記録し、" +
                "止めたときに「残す／一覧で選ぶ／捨てる」をたずねます。ゲームが動かした値（動き・ゲージ・文字）は記録しません。"));

            _record = new Toggle("Play中の変更を記録する")
            {
                tooltip = "ONなら、Play 中に変えた UI の値を記録し、止めたときにたずねます（このパソコンだけの設定）。"
            };
            _record.RegisterValueChangedCallback(e => UiPlayChangeRecorder.Enabled = e.newValue);
            root.Add(_record);

            _status = StudioUi.Note("", NoteKind.Info);
            root.Add(_status);

            _apply = StudioUi.Button("選んだ物を残す", Apply, "チェックした変更を、Prefab ファイル／シーンに書き戻します（Play を止めてから）。", primary: true);
            root.Add(StudioUi.Row(
                _apply,
                StudioUi.Button("選んだ物を捨てる", DiscardSelected, "チェックした変更を一覧から外します（書き戻しません）。", small: true),
                StudioUi.Button("全部選ぶ", () => SetAll(true), "残せる変更すべてにチェックを付けます。", small: true),
                StudioUi.Button("全部外す", () => SetAll(false), "すべてのチェックを外します。", small: true),
                StudioUi.Button("全部捨てる", DiscardAll, "一覧を空にします（書き戻しません）。", small: true)));

            _result = StudioUi.Styled(new Label(), "sk-muted");
            _result.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_result);

            root.Add(StudioUi.Section("変更の一覧", "書き戻し先（Prefab ファイル・シーン）ごとに並びます。同じ欄を何度も変えたときは、最初の値と最後の値の1行にまとまります。"));
            _list = new PlayChangeListPanel();
            root.Add(_list);

            root.Add(StudioUi.Section("残せる物・残せない物"));
            root.Add(StudioUi.Note(
                "残せる: 文字・数・色・位置・大きさ・ON/OFF・画像やフォントの差し替え・動き（足す・消す・中身）・画面の設定・部品の名前・部品を足したとき（「動き」「濃さ」など）。\n" +
                "残せない: Play 中に作られた部品（ロビーの部屋の行など）の値・部品の追加や削除・並べ替え・グループ化（このスタジオでは Play 中は押せません）。",
                NoteKind.Info));
            return root;
        }

        public override void Refresh()
        {
            _record.SetValueWithoutNotify(UiPlayChangeRecorder.Enabled);
            var playing = EditorApplication.isPlaying;
            var count = UiPlayChangeStore.Count;
            if (playing)
            {
                StudioUi.SetNote(_status,
                    UiPlayChangeRecorder.Enabled
                        ? $"Play 中: 記録しています（{count} か所）。止めると、残すかどうかをたずねます。"
                        : "Play 中ですが、記録は OFF です。止めると、Play 中の変更は消えます。",
                    UiPlayChangeRecorder.Enabled ? NoteKind.Ok : NoteKind.Warn);
            }
            else
            {
                StudioUi.SetNote(_status,
                    count > 0 ? $"まだ決めていない変更が {count} か所あります。チェックして「選んだ物を残す」か、捨ててください。" : "Play して UI を変えると、ここに記録されます。",
                    count > 0 ? NoteKind.Warn : NoteKind.Info);
            }

            _apply.SetEnabled(!playing && UiPlayChangeStore.ApplicableCount > 0);
            _list.Refresh();
        }

        private void Apply()
        {
            var report = UiPlayChangeApplier.ApplyKept();
            report.Log();
            _result.text = report.Problems.Count > 0 && report.Applied == 0 && report.Failed == 0 ? report.Problems[0] : report.Summary;
            Context.MarkScreensDirty();
        }

        private static void DiscardSelected() => UiPlayChangeStore.RemoveAll(c => c.keep && c.CanApply);

        private static void DiscardAll()
        {
            if (UiPlayChangeStore.Count > 0 && EditorUtility.DisplayDialog("全部捨てる", $"Play 中の変更 {UiPlayChangeStore.Count} か所を、書き戻さずに一覧から外します。", "捨てる", "やめる"))
            {
                UiPlayChangeStore.Clear();
            }
        }

        private static void SetAll(bool keep)
        {
            foreach (var change in UiPlayChangeStore.Items)
            {
                change.keep = keep && change.CanApply;
            }

            UiPlayChangeStore.Touch();
        }
    }
}
