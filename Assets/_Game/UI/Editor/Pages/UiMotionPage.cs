using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>動き: 選んだ部品の動きの一覧／定番の動きのギャラリー（クリックで試す）／画面全体の出る・消える。</summary>
    public sealed class UiMotionPage : UiPageBase
    {
        private MotionEntriesPanel _entries;
        private PresetGalleryPanel _gallery;
        private VisualElement _screenHost;
        private UiScreen _boundScreen;

        public UiMotionPage(UiStudioContext context) : base(context)
        {
        }

        public override string Icon => "〰";
        public override string Label => "動き";
        public override string Tooltip => "部品が出る・消える・押された・値が変わったときの動きを付けて、その場で試します。";

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle("動き"));
            root.Add(StudioUi.Lead("部品を選んで、定番の動きをクリックすると、Playしなくてもその場で動きます。気に入ったら「付ける」。動きの中身は D-Drive なので、あとから種類や長さを差し替えられます。"));

            root.Add(StudioUi.Section("選んだ部品の動き"));
            _entries = new MotionEntriesPanel(Context);
            root.Add(_entries);

            root.Add(StudioUi.Section("定番の動き（クリックで試す）"));
            _gallery = new PresetGalleryPanel(Context, () => _entries.Refresh());
            root.Add(_gallery);

            root.Add(StudioUi.Section("画面全体が出る・消えるときの動き", "画面のルートの動き。子の部品の「出たとき／消えるとき」の動きと同時に始まります。"));
            _screenHost = new VisualElement();
            root.Add(_screenHost);
            return root;
        }

        public override void Refresh()
        {
            _entries.Refresh();
            _gallery.Refresh();
            var screen = Context.EditingScreen;
            if (screen == _boundScreen)
            {
                return;
            }

            _boundScreen = screen;
            _screenHost.Clear();
            _screenHost.Unbind();
            if (screen == null)
            {
                _screenHost.Add(StudioUi.Note("編集中の画面がありません。", NoteKind.Info));
                return;
            }

            var so = new SerializedObject(screen);
            var card = StudioUi.Card();
            card.Add(new PropertyField(so.FindProperty("showMotion"), "出るときの動き"));
            card.Add(new PropertyField(so.FindProperty("hideMotion"), "消えるときの動き"));
            card.Add(StudioUi.Row(
                StudioUi.Button("▶ 出る動きを試す", () => UiMotionPreview.PlayScreen(screen, true), "画面全体と子の部品の「出たとき」をまとめて再生します。", primary: true, small: true),
                StudioUi.Button("▶ 消える動きを試す", () => UiMotionPreview.PlayScreen(screen, false), "画面全体と子の部品の「消えるとき」をまとめて再生します。", small: true),
                StudioUi.Button("止める", UiMotionPreview.Stop, "試している動きを止めて元に戻します。", small: true)));
            card.Bind(so);
            _screenHost.Add(card);
        }
    }
}
