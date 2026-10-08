using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>点検: 問題の一覧（重い順）。表示は StudioKit の共通部品（UIスタジオと同じ）。</summary>
    public sealed class StageCheckPage : StagePageBase
    {
        private StudioIssueList _list;

        public StageCheckPage(StageStudioContext context) : base(context)
        {
        }

        public override string Icon => "✓";
        public override string Label => "点検";
        public override string Tooltip => "道が塞がっていないか、砲台にかぶっていないか…などを自動で点検します。";

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
            root.Add(StudioUi.Lead("ステージを自動で点検します（数秒ごとに自動で更新）。赤は遊べなくなる問題、黄色は見た目や遊び心地の問題、青はお知らせです。"));
            _list = new StudioIssueList(Select, () =>
            {
                Context.MarkPropsDirty();
                Context.RecheckNow();
            }, () => Context.RecheckNow(), "このまま遊べます。最後に上の帯の「ステージに保存」を忘れずに。");
            root.Add(_list);
            return root;
        }

        public override void Refresh() => _list.Show(Context.Issues);

        private void Select(Object target)
        {
            if (target is StageProp prop)
            {
                Context.Select(prop);
            }

            EditorGUIUtility.PingObject(target);
            if (target is Component component)
            {
                StageStudioContext.Frame(component);
            }
        }
    }
}
