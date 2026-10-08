using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>点検: UIの問題の一覧。表示は StudioKit の共通部品（ステージ背景スタジオと同じ）。</summary>
    public sealed class UiCheckPage : UiPageBase
    {
        private StudioIssueList _list;

        public UiCheckPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "✓";
        public override string Label => "点検";
        public override string Tooltip => "置き場所・名前の重なり・値の打ち間違い・押せない部品などを自動で点検します。";

        public override int Badge
        {
            get
            {
                var (errors, warnings) = StudioIssueList.Count(Context.Issues);
                return errors > 0 ? errors : warnings;
            }
        }

        public override bool BadgeIsWarning => StudioIssueList.Count(Context.Issues).errors == 0;

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("点検"));
            root.Add(StudioUi.Lead("UIを自動で点検します（数秒ごとに自動で更新）。赤は動かない問題、黄色は見た目や操作の問題、青はお知らせです。"));
            _list = new StudioIssueList(Select, () => Context.RecheckNow(), () => Context.RecheckNow(),
                "このまま動きます。Play して、起動時に開く画面とボタンの動きを確かめてください。");
            root.Add(_list);
            return root;
        }

        public override void Refresh() => _list.Show(Context.Issues);

        private void Select(Object target)
        {
            if (target is UiScreen screen)
            {
                Context.SelectScreen(screen);
            }

            EditorGUIUtility.PingObject(target);
            Selection.activeObject = target is Component component ? component.gameObject : target;
        }
    }
}
