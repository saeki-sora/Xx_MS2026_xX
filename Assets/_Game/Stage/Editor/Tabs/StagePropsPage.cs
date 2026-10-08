using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>オブジェクト: 左に背景オブジェクトの一覧、右に選んだ物の詳細。</summary>
    public sealed class StagePropsPage : StagePageBase
    {
        private VisualElement _notice;
        private VisualElement _split;
        private PropListPanel _list;
        private PropDetailPanel _detail;

        public StagePropsPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "▤";
        public override string Label => "オブジェクト";
        public override string Tooltip => "背景オブジェクトの役割・通れない範囲・見た目の反応・モデルを調整します。";

        public override int Badge
        {
            get
            {
                var count = 0;
                foreach (var issue in Context.Issues)
                {
                    if (issue.Target is StageProp && issue.Severity == StudioIssueSeverity.Error)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("背景オブジェクト"));
            root.Add(StudioUi.Lead("一覧で選ぶと右に設定が出ます。Ctrl（Shift）を押しながらクリックすると複数選べて、まとめて役割を変えられます。シーンビューで選んでも同じです。"));
            _notice = new VisualElement();
            root.Add(_notice);

            _split = StudioUi.Styled(new VisualElement(), "sk-split");
            _list = new PropListPanel(Context);
            _detail = new PropDetailPanel(Context);
            _split.Add(_list);
            _split.Add(_detail);
            root.Add(_split);
            return root;
        }

        public override void Refresh()
        {
            var missing = UpdateMissingNotice(_notice);
            _split.style.display = missing ? DisplayStyle.None : DisplayStyle.Flex;
            if (!missing)
            {
                _list.Refresh();
                _detail.Refresh();
            }
        }
    }
}
