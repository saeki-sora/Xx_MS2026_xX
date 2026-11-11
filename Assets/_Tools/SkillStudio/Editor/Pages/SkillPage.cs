using System.Collections.Generic;
using System.Linq;
using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 色（＝キャラ）1つぶんのページ（2026-11-09 作り直し。使いやすさの資料 Docs/Reference/Tool_UI_Analysis.md に合わせた）。
    /// 上から: 題名 → 細い帯（試し撃ち・比べる） → 「レベルデザイン／見た目」の切り替え → よく触る項目（最初に見えるのはここだけ）
    /// → 「詳しい設定」（押すと開く。中はやりたいことのまとまりごとに折りたたみ、1段だけ）。
    /// よく触る項目は設定ファイル側の [SkillQuick]、欄の分け方は [SkillTab]、付け外しの置き場所は [SkillDialFor] で決まるので、
    /// スキルに設定を足してもこのページは直さなくてよい。
    /// 初期値から変えた項目には左に青い印と右に「↺」（T4）。右上の検索で、両方の欄から名前で探せる（T5。Ctrl+F）。
    /// </summary>
    public sealed class SkillPage : IStudioPage
    {
        private readonly SkillStudioContext _context;
        private readonly SkillColor _color;
        private VisualElement _root;
        private VisualElement _content;
        private Label _title;
        private Button _selectAsset;
        private SkillToolbar _toolbar;
        private System.Action _refreshSegment;
        private VisualElement _segment;
        private ToolbarSearchField _search;
        private string _query = "";
        private string _builtQuery = "";
        private CharacterSkill _builtFor;
        private SkillTab _builtTab;
        private readonly List<SkillDetailSection> _sections = new List<SkillDetailSection>();
        private Button _detailToggle;
        private int _detailCount;

        public SkillPage(SkillStudioContext context, SkillColor color)
        {
            _context = context;
            _color = color;
        }

        public string Icon => SkillColors.Label(_color);

        public string Label => SkillColors.Label(_color) + "のスキル";

        public string Tooltip => $"{SkillColors.Label(_color)}のキャラのスキルの、レベルデザインと見た目を調整する。";

        public int Badge => 0;

        public bool BadgeIsWarning => false;

        public VisualElement Build()
        {
            _root = new VisualElement();

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            _title = StudioUi.PageTitle("");
            _title.style.flexGrow = 1;
            _search = new ToolbarSearchField
            {
                placeholderText = "項目を探す（Ctrl+F）",
                tooltip = "項目を名前で探す（レベルデザイン・見た目の両方から）。ひらがな・カタカナどちらでもよい。空白で区切ると全部の言葉を含む物だけ。Ctrl+F でここに入る。"
            };
            _search.style.width = 190;
            _search.style.marginRight = 6;
            _search.SetValueWithoutNotify(_query);
            _search.RegisterValueChangedCallback(e =>
            {
                _query = e.newValue ?? "";
                RebuildContent();
            });
            _selectAsset = StudioUi.Button("設定ファイル", SelectAsset, "", small: true);
            titleRow.Add(_title);
            titleRow.Add(_search);
            titleRow.Add(_selectAsset);
            _root.Add(titleRow);
            _root.focusable = true;
            _root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.F && (e.ctrlKey || e.commandKey))
                {
                    _search.Focus();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape && !string.IsNullOrEmpty(_query))
                {
                    _search.value = "";
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            _toolbar = new SkillToolbar(_context, _color);
            _root.Add(_toolbar);

            var segment = StudioUi.Segment(new (SkillTab, string, string, string)[]
                {
                    (SkillTab.Level, "⚖", "レベルデザイン", "強さ・時間・範囲・溜まり方など、ゲームの手ごたえに関わる数字。"),
                    (SkillTab.Look, "✦", "見た目", "色・光・演出・揺れ・効果音。")
                },
                () => _context.TabOf(_color),
                tab =>
                {
                    _context.SetTab(_color, tab);
                    _refreshSegment?.Invoke();
                    RebuildContent();
                },
                out _refreshSegment);
            segment.style.marginBottom = 12;
            _segment = segment;
            _root.Add(segment);

            _content = new VisualElement();
            _root.Add(_content);
            RebuildContent();
            return _root;
        }

        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }

            var skill = _context.SkillOf(_color);
            _title.text = $"{SkillColors.Label(_color)}のスキル" + (skill != null ? $"「{skill.DisplayName}」" : "");
            _selectAsset.tooltip = skill != null
                ? $"この設定ファイルをプロジェクトで選ぶ（{AssetDatabase.GetAssetPath(skill)}）。値はここで変えるとすぐ入り、Ctrl+Z で戻せる。"
                : "設定ファイルが見つからない。";
            _toolbar.Refresh();
            if (skill != _builtFor || _context.TabOf(_color) != _builtTab || _query != _builtQuery)
            {
                RebuildContent();
            }

            RefreshChanged();
        }

        /// <summary>まとまりの見出しの「変えた N」と、「詳しい設定」ボタンの数を今の値に合わせる。</summary>
        private void RefreshChanged()
        {
            var changed = 0;
            foreach (var section in _sections)
            {
                changed += section.RefreshChanged();
            }

            if (_detailToggle != null)
            {
                var open = _context.IsOpen($"{_color}/{_builtTab}/details");
                var suffix = changed > 0 ? $"・変えた {changed}" : "";
                _detailToggle.text = open ? "▼ 詳しい設定を閉じる" : $"▶ 詳しい設定（{_detailCount} のまとまり{suffix}）";
            }
        }

        private void RebuildContent()
        {
            if (_content == null)
            {
                return;
            }

            _content.Clear();
            _sections.Clear();
            _detailToggle = null;
            var skill = _context.SkillOf(_color);
            var tab = _context.TabOf(_color);
            _builtFor = skill;
            _builtTab = tab;
            _builtQuery = _query;
            var searching = !string.IsNullOrWhiteSpace(_query);
            if (_segment != null)
            {
                _segment.style.display = searching ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (skill == null)
            {
                _content.Add(StudioUi.Empty("?", "スキルが見つかりません",
                    $"キャラとスキルの対応表（CharacterSkillTable）に、characterId「{SkillColors.CharacterId(_color)}」の行を足してください。"));
                return;
            }

            var serializedObject = new SerializedObject(skill);
            var type = skill.GetType();
            var keyPrefix = $"{_color}/{tab}/";

            if (searching)
            {
                var hits = SkillSearch.Find(type, _query);
                _content.Add(StudioUi.Section(hits.Count > 0 ? $"「{_query.Trim()}」で見つかった項目  {hits.Count}" : $"「{_query.Trim()}」に合う項目はありません",
                    "レベルデザイン・見た目の両方から探している。Esc か ✕ で元の画面に戻る。"));
                if (hits.Count > 0)
                {
                    _content.Add(new SkillSearchResults(serializedObject, hits));
                }
                else
                {
                    _content.Add(StudioUi.Note("言葉を短くしてみてください（例: 「稲妻」「ビリビリ」「揺れ」「ダメージ」）。漢字の読みでは探せません。", NoteKind.Info));
                }

                return;
            }

            // よく触る項目（最初に見えるのはここだけ）
            var quick = SkillTabs.QuickItems(type, tab);
            _content.Add(StudioUi.Section("よく触る項目", "迷ったらまずここを触る。ほかの値は下の「詳しい設定」にある。"));
            if (quick.Count > 0)
            {
                _content.Add(new SkillQuickList(serializedObject, quick));
            }
            else
            {
                _content.Add(StudioUi.Note("この欄には、よく触る項目の印（[SkillQuick]）が付いた値がまだありません。下の「詳しい設定」から触ってください。", NoteKind.Info));
            }

            // 詳しい設定（押すと開く。中はまとまりごとに折りたたみ）
            var groups = SkillTabs.Groups(type, tab).Where(g => !SkillTabs.IsFullyDistributed(g)).ToList();
            _detailCount = groups.Count + (tab == SkillTab.Level ? 2 : 0);
            var detailKey = keyPrefix + "details";
            var details = new VisualElement();
            var toggle = StudioUi.Button("", null, "ほかの値を、まとまりごとに折りたたんで並べる。「変えた N」は、中で初期値から変えた項目の数。", small: true);
            _detailToggle = toggle;
            void ApplyDetails()
            {
                var open = _context.IsOpen(detailKey);
                details.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshChanged();
            }

            toggle.clicked += () =>
            {
                _context.SetOpen(detailKey, !_context.IsOpen(detailKey));
                ApplyDetails();
            };
            toggle.style.alignSelf = Align.FlexStart;
            toggle.style.marginBottom = 6;
            _content.Add(toggle);
            _content.Add(details);

            if (tab == SkillTab.Level)
            {
                AddSection(details, SkillDetailSection.ForFields(_context, keyPrefix + "charge", serializedObject, "溜まり方",
                    SkillTabs.ChargeFields, "スキルが使えるようになる条件（敵を倒して溜めるゲージ・スマッシュボール）。"));
            }

            foreach (var group in groups)
            {
                AddSection(details, SkillDetailSection.ForGroup(_context, keyPrefix, serializedObject, group));
            }

            if (tab == SkillTab.Level)
            {
                AddSection(details, SkillDetailSection.ForFields(_context, keyPrefix + "laser", serializedObject, "普段のレーザーとの関係",
                    SkillTabs.LaserFields, "スキル中・スキルのあとに、普段のレーザーを出すかどうか。"));
            }

            ApplyDetails();
        }

        private void AddSection(VisualElement parent, SkillDetailSection section)
        {
            _sections.Add(section);
            parent.Add(section);
        }

        private void SelectAsset()
        {
            var skill = _context.SkillOf(_color);
            if (skill == null)
            {
                return;
            }

            Selection.activeObject = skill;
            EditorGUIUtility.PingObject(skill);
        }
    }
}
