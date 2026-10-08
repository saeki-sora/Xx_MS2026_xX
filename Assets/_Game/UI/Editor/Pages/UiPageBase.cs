using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>UIスタジオのページの共通部分。</summary>
    public abstract class UiPageBase : IStudioPage
    {
        protected UiPageBase(UiStudioContext context)
        {
            Context = context;
        }

        protected UiStudioContext Context { get; }

        public abstract string Icon { get; }
        public abstract string Label { get; }
        public abstract string Tooltip { get; }

        public virtual int Badge => 0;
        public virtual bool BadgeIsWarning => false;

        public abstract VisualElement Build();

        public virtual void Refresh()
        {
        }

        /// <summary>編集中の画面が無いときの案内。</summary>
        internal static VisualElement NoEditingScreen() =>
            StudioUi.Empty("▢", "編集中の画面がありません",
                "「画面」ページで画面を選び「この画面を編集」を押すか、シーンに置いた画面を選んでください。");
    }
}
