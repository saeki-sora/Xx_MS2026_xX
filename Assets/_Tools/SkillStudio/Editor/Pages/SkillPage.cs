using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 色（＝キャラ）1つぶんのページ。上: スキル名・設定ファイル・試し撃ち・保存して比べる。
    /// その下の切り替えで「レベルデザイン」（溜まり方・強さ・時間・範囲）と「見た目」（演出の付け外し・色・揺れ・効果音）を出し分ける。
    /// 欄の分け方は設定ファイル側の [SkillTab] で決まるので、スキルに設定を足してもこのページは直さなくてよい。
    /// </summary>
    public sealed class SkillPage : IStudioPage
    {
        private readonly SkillStudioContext _context;
        private readonly SkillColor _color;
        private VisualElement _root;
        private VisualElement _content;
        private Label _title;
        private Label _subtitle;
        private SkillTestPanel _test;
        private SkillSnapshotPanel _snapshots;
        private System.Action _refreshSegment;
        private CharacterSkill _builtFor;
        private SkillTab _builtTab;

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
            _title = StudioUi.PageTitle("");
            _subtitle = StudioUi.Lead("");
            _root.Add(_title);
            _root.Add(_subtitle);

            var tools = StudioUi.Row(
                StudioUi.Button("設定ファイルを選ぶ", SelectAsset, "このスキルの設定ファイルをプロジェクトで選ぶ（インスペクターでも同じ値を触れる）。", small: true));
            tools.style.marginBottom = 8;
            _root.Add(tools);

            var top = StudioUi.Row();
            top.style.alignItems = Align.FlexStart;
            _test = new SkillTestPanel(_context, _color);
            _snapshots = new SkillSnapshotPanel(_context, _color);
            _test.style.flexGrow = 1;
            _test.style.flexBasis = 0;
            _test.style.marginRight = 8;
            _snapshots.style.flexGrow = 1;
            _snapshots.style.flexBasis = 0;
            top.Add(_test);
            top.Add(_snapshots);
            _root.Add(top);

            var segment = StudioUi.Segment(new (SkillTab, string, string, string)[]
                {
                    (SkillTab.Level, "⚖", "レベルデザイン", "強さ・時間・範囲・溜まり方など、ゲームの手ごたえに関わる数字。"),
                    (SkillTab.Look, "✦", "見た目", "演出の付け外し・強さ、色・模様、揺れ、効果音。")
                },
                () => _context.TabOf(_color),
                tab =>
                {
                    _context.SetTab(_color, tab);
                    _refreshSegment?.Invoke();
                    RebuildContent();
                },
                out _refreshSegment);
            segment.style.marginTop = 6;
            segment.style.marginBottom = 10;
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
            _subtitle.text = skill != null
                ? $"{AssetDatabase.GetAssetPath(skill)}　（値を変えると設定ファイルにすぐ入ります。Ctrl+Z で戻せます）"
                : "このキャラのスキルの設定ファイルが見つかりません（キャラとスキルの対応表に登録されていない）。";
            _test.Refresh();
            _snapshots.Refresh();
            if (skill != _builtFor || _context.TabOf(_color) != _builtTab)
            {
                RebuildContent();
            }
        }

        private void RebuildContent()
        {
            if (_content == null)
            {
                return;
            }

            _content.Clear();
            var skill = _context.SkillOf(_color);
            var tab = _context.TabOf(_color);
            _builtFor = skill;
            _builtTab = tab;
            if (skill == null)
            {
                _content.Add(StudioUi.Empty("?", "スキルが見つかりません",
                    $"キャラとスキルの対応表（CharacterSkillTable）に、characterId「{SkillColors.CharacterId(_color)}」の行を足してください。"));
                return;
            }

            var serializedObject = new SerializedObject(skill);
            if (tab == SkillTab.Level)
            {
                _content.Add(new SkillFieldsCard(serializedObject, "溜まり方",
                    "スキルが使えるようになる条件。敵を倒して溜めるゲージと、スマッシュボール。", SkillTabs.ChargeFields));
            }

            var groups = SkillTabs.Groups(skill.GetType(), tab);
            if (groups.Count == 0 && tab == SkillTab.Look)
            {
                _content.Add(StudioUi.Note("このスキルには、見た目の欄がまだありません。", NoteKind.Info));
            }

            var first = true;
            foreach (var group in groups)
            {
                if (SkillTabs.IsDialGroup(group))
                {
                    _content.Add(new SkillDialsPanel(serializedObject, group));
                    continue;
                }

                _content.Add(new SkillGroupCard(serializedObject, group, first));
                first = false;
            }

            if (tab == SkillTab.Level)
            {
                _content.Add(new SkillFieldsCard(serializedObject, "普段のレーザーとの関係",
                    "スキル中・スキルのあとに、普段のレーザーを出すかどうか。", SkillTabs.LaserFields));
            }
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
