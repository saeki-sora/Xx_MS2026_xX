using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.StudioKit
{
    /// <summary>StudioShell の左のナビに並ぶ1ページ。</summary>
    public interface IStudioPage
    {
        string Icon { get; }
        string Label { get; }
        string Tooltip { get; }

        /// <summary>ページの中身（最初に開いたときに1回だけ呼ばれる）。</summary>
        VisualElement Build();

        /// <summary>ページが表示されている間、定期的に呼ばれる（重い処理は変化があったときだけ行う）。</summary>
        void Refresh();

        /// <summary>ナビに出す数字（問題の数など）。0なら出さない。</summary>
        int Badge { get; }

        /// <summary>数字を赤（エラー）ではなく黄色（注意）で出すか。</summary>
        bool BadgeIsWarning { get; }
    }

    /// <summary>
    /// ツール画面の骨組み: 上の帯（ロゴ・題名・右側の操作）／左のナビ／中身／下の帯（マウスを乗せた項目の説明）。
    /// 下の帯は、マウスの下にある部品の tooltip を自動で表示するので、各部品に tooltip を付けるだけで説明が出る。
    /// </summary>
    public sealed class StudioShell : VisualElement
    {
        public const string StylePath = "Assets/_Tools/StudioKit/Editor/StudioKit.uss";

        private readonly List<IStudioPage> _pages = new List<IStudioPage>();
        private readonly List<VisualElement> _navItems = new List<VisualElement>();
        private readonly List<Label> _badges = new List<Label>();
        private readonly Dictionary<IStudioPage, VisualElement> _built = new Dictionary<IStudioPage, VisualElement>();
        private readonly VisualElement _nav;
        private readonly ScrollView _content;
        private readonly Label _status;
        private readonly string _defaultHint;

        public StudioShell(string logo, string title, string subtitle, string defaultHint)
        {
            AddToClassList("sk-root");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sheet != null)
            {
                styleSheets.Add(sheet);
            }
            else
            {
                Debug.LogWarning($"[StudioKit] スタイルシートが見つかりません: {StylePath}");
            }

            var header = StudioUi.Styled(new VisualElement(), "sk-header");
            header.Add(StudioUi.Styled(new Label(logo), "sk-logo"));
            var titles = new VisualElement();
            titles.Add(StudioUi.Styled(new Label(title), "sk-title"));
            titles.Add(StudioUi.Styled(new Label(subtitle), "sk-subtitle"));
            header.Add(titles);
            HeaderSlot = StudioUi.Styled(new VisualElement(), "sk-header-slot");
            header.Add(HeaderSlot);
            Add(header);

            var body = StudioUi.Styled(new VisualElement(), "sk-body");
            _nav = StudioUi.Styled(new VisualElement(), "sk-nav");
            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.AddToClassList("sk-content");
            body.Add(_nav);
            body.Add(_content);
            Add(body);

            var statusBar = StudioUi.Styled(new VisualElement(), "sk-statusbar");
            statusBar.Add(StudioUi.Styled(new Label("?"), "sk-status-icon"));
            _defaultHint = defaultHint;
            _status = StudioUi.Styled(new Label(defaultHint), "sk-status-text");
            statusBar.Add(_status);
            Add(statusBar);

            RegisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            RegisterCallback<PointerLeaveEvent>(_ => _status.text = _defaultHint);
        }

        /// <summary>上の帯の右側（ステージの選択・保存ボタンなどを置く場所）。</summary>
        public VisualElement HeaderSlot { get; }

        public IStudioPage Current { get; private set; }

        public void AddPage(IStudioPage page)
        {
            var index = _pages.Count;
            _pages.Add(page);

            var item = StudioUi.Styled(new VisualElement(), "sk-nav-item");
            item.tooltip = page.Tooltip;
            item.Add(StudioUi.Styled(new Label(page.Icon), "sk-nav-icon"));
            item.Add(StudioUi.Styled(new Label(page.Label), "sk-nav-label"));
            var badge = StudioUi.Styled(new Label(), "sk-nav-badge");
            badge.AddToClassList("sk-nav-badge--hidden");
            item.Add(badge);
            item.RegisterCallback<ClickEvent>(_ => Show(_pages[index]));
            _nav.Add(item);
            _navItems.Add(item);
            _badges.Add(badge);
        }

        public void Show(IStudioPage page)
        {
            Current = page;
            for (var i = 0; i < _pages.Count; i++)
            {
                _navItems[i].EnableInClassList("sk-nav-item--active", _pages[i] == page);
            }

            if (!_built.TryGetValue(page, out var root))
            {
                root = page.Build();
                root.AddToClassList("sk-page");
                _built[page] = root;
            }

            _content.Clear();
            _content.Add(root);
            _content.scrollOffset = Vector2.zero;
            page.Refresh();
        }

        public void ShowPage<T>() where T : IStudioPage
        {
            foreach (var page in _pages)
            {
                if (page is T)
                {
                    Show(page);
                    return;
                }
            }
        }

        /// <summary>表示中のページの更新とナビの数字の更新（ウィンドウが定期的に呼ぶ）。</summary>
        public void Tick()
        {
            Current?.Refresh();
            for (var i = 0; i < _pages.Count; i++)
            {
                var count = _pages[i].Badge;
                _badges[i].text = count > 99 ? "99+" : count.ToString();
                _badges[i].EnableInClassList("sk-nav-badge--hidden", count <= 0);
                _badges[i].EnableInClassList("sk-nav-badge--warn", _pages[i].BadgeIsWarning);
            }
        }

        /// <summary>作り直しが必要になったページ（ステージを替えたときなど）を捨てる。次に開いたときに作り直す。</summary>
        public void InvalidateAll()
        {
            _built.Clear();
            if (Current != null)
            {
                Show(Current);
            }
        }

        private void OnPointerOver(PointerOverEvent evt)
        {
            for (var element = evt.target as VisualElement; element != null && element != this; element = element.parent)
            {
                if (!string.IsNullOrEmpty(element.tooltip))
                {
                    _status.text = element.tooltip;
                    return;
                }
            }

            _status.text = _defaultHint;
        }
    }
}
