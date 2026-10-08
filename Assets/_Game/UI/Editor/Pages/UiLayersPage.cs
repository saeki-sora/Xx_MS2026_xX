using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>レイヤー: 画面の重なりの段（上）と、編集中の画面の部品のレイヤー（下、Photoshop 風）。</summary>
    public sealed class UiLayersPage : UiPageBase
    {
        private LayerStackPanel _stack;
        private ElementLayersPanel _elements;

        public UiLayersPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "☰";
        public override string Label => "レイヤー";
        public override string Tooltip => "画面の重なりの段と、画面の中の部品の重なり順・表示・ロック・グループを整理します。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("レイヤー"));
            root.Add(StudioUi.Lead("上は「画面の重なり」（どの画面がどの画面の手前に出るか）、下は「編集中の画面の部品」（Photoshop のレイヤーと同じく、上ほど手前）。"));

            root.Add(StudioUi.Section("画面の重なり（上ほど手前）", "段（レイヤー）ごとに描く順番が決まっています。同じ段の中では、後から開いた画面が手前に出ます。"));
            _stack = new LayerStackPanel(Context);
            root.Add(_stack);

            root.Add(StudioUi.Section("部品（上ほど手前）", "◎=編集中だけ隠す ●=ゲームでも表示 ⊠=選べなくする（ロック） ↑↓=重なり順。クリックで選択、Ctrlで追加選択、ダブルクリックでシーンに映す。"));
            _elements = new ElementLayersPanel(Context);
            root.Add(_elements);
            return root;
        }

        public override void Refresh()
        {
            _stack.Refresh();
            _elements.Refresh();
        }
    }
}
