using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>ステージ背景スタジオのページの共通部分（状態の受け取りと、置き場所が無いときの案内）。</summary>
    public abstract class StagePageBase : IStudioPage
    {
        protected StagePageBase(StageStudioContext context)
        {
            Context = context;
        }

        protected StageStudioContext Context { get; }

        public abstract string Icon { get; }
        public abstract string Label { get; }
        public abstract string Tooltip { get; }

        public virtual int Badge => 0;
        public virtual bool BadgeIsWarning => false;

        public abstract VisualElement Build();

        public virtual void Refresh()
        {
        }

        private int _noticeState = -1;
        private VisualElement _noticeContainer;

        /// <summary>
        /// container に「ステージが無い」案内を出し分ける（状態が変わったときだけ作り直す）。戻り値は「ステージが無い」か。
        /// ページが作り直されて container が新しくなったときも描き直す（古い案内が残らないように）。
        /// </summary>
        protected bool UpdateMissingNotice(VisualElement container)
        {
            var state = Context.Root == null ? 0 : Context.Root.instance == null ? 1 : 2;
            if (state != _noticeState || container != _noticeContainer)
            {
                _noticeState = state;
                _noticeContainer = container;
                container.Clear();
                var notice = MissingStageNotice();
                if (notice != null)
                {
                    container.Add(notice);
                }
            }

            return state != 2;
        }

        /// <summary>ステージがシーンに出ていないときの案内（出ていれば null）。</summary>
        protected VisualElement MissingStageNotice()
        {
            if (Context.Root == null)
            {
                return StudioUi.Empty("◇", "シーンにステージの置き場所がありません",
                    "背景はシーンの「[Stage]」の下に置きます。まず置き場所を作り、上の帯でステージを選んでください。",
                    "置き場所を作る", () => { StageSceneService.CreateRoot(); Context.RecheckNow(); });
            }

            if (Context.Root.instance == null)
            {
                return StudioUi.Empty("◇", "ステージがシーンに出ていません",
                    "上の帯でステージを選んで「シーンに出す」を押すか、「＋ 新しいステージ」で作ってください。");
            }

            return null;
        }
    }
}
