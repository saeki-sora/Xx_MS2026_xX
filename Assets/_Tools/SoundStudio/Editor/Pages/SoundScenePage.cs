using System.Collections.Generic;
using System.Linq;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// 1つの場面のページ: 上に BGM の行、その下にタイミングの行を小見出しごとに並べ、一番下に選んだ行の細かい設定を出す。
    /// </summary>
    public sealed class SoundScenePage : IStudioPage
    {
        private readonly SoundStudioContext _context;
        private readonly SoundPageId _page;
        private VisualElement _list;
        private SoundDetailPanel _detail;
        private int _shownVersion = -1;
        private readonly Dictionary<string, SoundRow> _rows = new Dictionary<string, SoundRow>();

        public SoundScenePage(SoundStudioContext context, SoundPageId page)
        {
            _context = context;
            _page = page;
            _context.SelectionChanged += OnSelectionChanged;
        }

        public string Icon => Icons(_page);
        public string Label => SoundPageIds.Label(_page);

        public string Tooltip => _page == SoundPageId.Other
            ? "名前の表にまだ載っていない音の欄（欄の説明を名前にして出している）。"
            : SoundPageIds.Label(_page) + " の場面で鳴る音。数字は、まだ音が入っていない欄の数。";

        public int Badge => SoundStudioContext.CountMissing(_context.Slots.Where(s => s.Page == _page));
        public bool BadgeIsWarning => true;

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.Add(StudioUi.PageTitle(Label));
            root.Add(StudioUi.Lead(_page == SoundPageId.Other
                ? "ほかのページに分けていない欄です。"
                : "行を押すと、下に細かい設定が出ます。▶ で聞けます。"));
            _list = new VisualElement();
            root.Add(_list);
            _detail = new SoundDetailPanel(_context);
            root.Add(_detail);
            return root;
        }

        public void Refresh()
        {
            if (_list == null)
            {
                return;
            }

            if (_shownVersion != _context.Version)
            {
                _shownVersion = _context.Version;
                Rebuild();
            }

            _detail.Refresh();
        }

        /// <summary>鳴った行を光らせる（Play 中）。</summary>
        public void Flash(SoundSlot slot)
        {
            if (slot != null && _rows.TryGetValue(slot.Key, out var row))
            {
                row.Flash();
            }
        }

        private void Rebuild()
        {
            _list.Clear();
            _rows.Clear();
            var slots = _context.SlotsOn(_page).ToList();
            if (slots.Count == 0)
            {
                _list.Add(StudioUi.Empty("♪", "出す行がありません", _context.MissingOnly || !string.IsNullOrEmpty(_context.Search)
                    ? "絞り込みを外すと出ます。"
                    : "この場面にはまだ音の欄がありません。"));
                _detail.Refresh();
                return;
            }

            // 同じ欄を複数の物が持つとき（4人分のレーザーなど）は、行の後ろに持ち主の名前を出す。
            var shared = new HashSet<string>(slots.GroupBy(s => s.DeclaringType?.FullName + "|" + s.FieldName)
                .Where(g => g.Count() > 1).Select(g => g.Key));

            var bgm = slots.Where(s => s.Kind == SoundSlotKind.Bgm || (s.Candidate?.IsBgm ?? false)).ToList();
            if (bgm.Count > 0)
            {
                AddGroup("BGM", bgm, shared);
            }

            foreach (var group in slots.Except(bgm).GroupBy(s => s.Group))
            {
                AddGroup(group.Key, group.ToList(), shared);
            }

            OnSelectionChanged();
        }

        private void AddGroup(string title, List<SoundSlot> slots, HashSet<string> shared)
        {
            _list.Add(StudioUi.Section(title));
            var box = new VisualElement();
            box.AddToClassList("ss-rows");
            foreach (var slot in slots.OrderBy(s => s.Kind == SoundSlotKind.Candidate))
            {
                var showOwner = shared.Contains(slot.DeclaringType?.FullName + "|" + slot.FieldName);
                var row = new SoundRow(_context, slot, showOwner);
                _rows[slot.Key] = row;
                box.Add(row);
            }

            _list.Add(box);
        }

        private void OnSelectionChanged()
        {
            foreach (var pair in _rows)
            {
                pair.Value.SetSelected(_context.Selected != null && pair.Key == _context.Selected.Key);
            }

            _detail?.Show(_context.Selected != null && _context.Selected.Page == _page ? _context.Selected : null);
        }

        private static string Icons(SoundPageId page)
        {
            switch (page)
            {
                case SoundPageId.Title: return "題";
                case SoundPageId.Lobby: return "集";
                case SoundPageId.CharacterSelect: return "選";
                case SoundPageId.HowToPlay: return "習";
                case SoundPageId.Roulette: return "抽";
                case SoundPageId.StageIntro: return "降";
                case SoundPageId.Match: return "戦";
                case SoundPageId.Boss: return "王";
                case SoundPageId.Result: return "果";
                default: return "他";
            }
        }
    }
}
