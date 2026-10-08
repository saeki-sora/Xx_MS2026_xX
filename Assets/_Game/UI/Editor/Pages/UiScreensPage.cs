using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>画面: 雛形から作る／一覧（レイヤーごと）／選んだ画面の設定と試す。</summary>
    public sealed class UiScreensPage : UiPageBase
    {
        private ScreenListPanel _list;
        private ScreenDetailPanel _detail;

        public UiScreensPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "▢";
        public override string Label => "画面";
        public override string Tooltip => "画面（ロビー・HUD・ポップアップ…）を雛形から作り、開き方・動きを決めます。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("画面"));
            root.Add(StudioUi.Lead("雛形を押すと、そのまま動く画面ができて一覧に入ります。左で選ぶと右に設定、ダブルクリックで編集。"));

            root.Add(StudioUi.Section("雛形から新しい画面を作る"));
            var grid = StudioUi.Styled(new VisualElement(), "sk-grid");
            foreach (var (template, icon, title, body) in UiTemplateFactory.All)
            {
                var t = template;
                grid.Add(StudioUi.ClickCard($"{icon}  {title}", body, () => Create(t), "押すと、この雛形で画面を作って一覧に入れます（Assets/_Game/UI/Screens/ に保存）。"));
            }

            root.Add(grid);

            root.Add(StudioUi.Section("画面の一覧"));
            var split = StudioUi.Styled(new VisualElement(), "sk-split");
            _list = new ScreenListPanel(Context);
            _detail = new ScreenDetailPanel(Context);
            split.Add(_list);
            split.Add(_detail);
            root.Add(split);
            return root;
        }

        public override void Refresh()
        {
            _list.Refresh();
            _detail.Refresh();
        }

        private void Create(UiTemplateFactory.Template template)
        {
            var baseId = template switch
            {
                UiTemplateFactory.Template.Lobby => "Lobby",
                UiTemplateFactory.Template.Hud => "Hud",
                UiTemplateFactory.Template.Popup => "Confirm",
                _ => "Screen"
            };

            var screen = UiTemplateFactory.Create(template, UiTemplateFactory.UniqueId(baseId));
            Context.MarkScreensDirty();
            Context.SelectScreen(screen);
        }
    }
}
