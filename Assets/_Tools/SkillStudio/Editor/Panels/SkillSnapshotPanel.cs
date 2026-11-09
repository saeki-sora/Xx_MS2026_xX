using System.Linq;
using System.Text;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 保存して比べる。今の値を A・B として取っておき、「A と B を切り替える」で交互に読み込んで見比べる。
    /// 名前を付けて保存した物は一覧から読み込める（全部／レベルデザインだけ／見た目だけ）。読み込みは Ctrl+Z で元に戻せる。
    /// 保存先は Assets/_Tools/SkillStudio/Data/SkillStudioSnapshots.asset（チームで共有される）。
    /// </summary>
    public sealed class SkillSnapshotPanel : VisualElement
    {
        private readonly SkillStudioContext _context;
        private readonly SkillColor _color;
        private readonly Label _abState;
        private readonly TextField _name;
        private readonly VisualElement _list;
        private string _lastSignature;

        public SkillSnapshotPanel(SkillStudioContext context, SkillColor color)
        {
            _context = context;
            _color = color;

            var card = StudioUi.Card("保存して比べる", "今の値を A と B に取っておき、切り替えて見比べます。Play 中に切り替えると、その場で見た目が変わります。");
            card.style.marginBottom = 10;
            Add(card);

            var abRow = StudioUi.Row(
                StudioUi.Button("今の値を A に", () => Save("A"), "今の値を A として保存する（前の A は印が外れて一覧に残る）。", small: true),
                StudioUi.Button("今の値を B に", () => Save("B"), "今の値を B として保存する（前の B は印が外れて一覧に残る）。", small: true),
                StudioUi.Button("A と B を切り替える", ToggleAB, "A と B を交互に読み込む（最後に読み込んだのが A なら B）。", primary: true, small: true));
            abRow.style.marginTop = 6;
            card.Add(abRow);
            _abState = new Label();
            _abState.style.marginTop = 4;
            _abState.style.opacity = 0.8f;
            card.Add(_abState);

            var saveRow = StudioUi.Row();
            saveRow.style.marginTop = 8;
            _name = new TextField { tooltip = "保存する名前（空なら日時）。" };
            _name.style.flexGrow = 1;
            saveRow.Add(_name);
            saveRow.Add(StudioUi.Button("名前を付けて保存", () => Save(""), "今の値を、この名前で一覧に保存する。", small: true));
            card.Add(saveRow);

            _list = new VisualElement();
            _list.style.marginTop = 6;
            card.Add(_list);
        }

        public void Refresh()
        {
            var skill = _context.SkillOf(_color);
            var entries = _context.Snapshots.EntriesOf(skill).ToList();
            var a = _context.Snapshots.Marked(skill, "A");
            var b = _context.Snapshots.Marked(skill, "B");
            var last = _context.Snapshots.LastMark(skill);
            _abState.text = $"A: {(a != null ? a.name : "まだ無い")}　／　B: {(b != null ? b.name : "まだ無い")}" +
                            (last != null ? $"　（今: {last}）" : "");

            var signature = new StringBuilder();
            foreach (var entry in entries)
            {
                signature.Append(entry.name).Append('|').Append(entry.mark).Append('|').Append(entry.savedAt).Append(';');
            }

            if (signature.ToString() == _lastSignature)
            {
                return;
            }

            _lastSignature = signature.ToString();
            _list.Clear();
            foreach (var entry in entries.Take(30))
            {
                var target = entry;
                var row = StudioUi.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 2;
                if (!string.IsNullOrEmpty(entry.mark))
                {
                    row.Add(StudioUi.Chip(entry.mark, ChipKind.Accent, "比べる印"));
                }

                var label = new Label($"{entry.name}　{entry.savedAt}");
                label.style.flexGrow = 1;
                label.style.overflow = Overflow.Hidden;
                row.Add(label);
                row.Add(StudioUi.Button("読み込む", () => _context.Snapshots.Apply(target, SnapshotScope.All), "この値をすべて読み込む（Ctrl+Z で戻せる）。", small: true));
                row.Add(StudioUi.Button("…", () => ShowMenu(target), "レベルデザインだけ・見た目だけ読み込む、A/B の印、消す。", small: true));
                _list.Add(row);
            }
        }

        private void Save(string mark)
        {
            var skill = _context.SkillOf(_color);
            _context.Snapshots.Save(skill, _name.value, mark);
            _name.SetValueWithoutNotify("");
            Refresh();
        }

        private void ToggleAB()
        {
            var skill = _context.SkillOf(_color);
            if (!_context.Snapshots.ToggleAB(skill))
            {
                EditorUtility.DisplayDialog("A と B を切り替える", "先に「今の値を A に」「今の値を B に」で2つ保存してください。", "OK");
            }

            Refresh();
        }

        private void ShowMenu(SkillSnapshotStore.Entry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("レベルデザインだけ読み込む"), false, () => _context.Snapshots.Apply(entry, SnapshotScope.Level));
            menu.AddItem(new GUIContent("見た目だけ読み込む"), false, () => _context.Snapshots.Apply(entry, SnapshotScope.Look));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("A の印を付ける"), entry.mark == "A", () => _context.Snapshots.SetMark(entry, "A"));
            menu.AddItem(new GUIContent("B の印を付ける"), entry.mark == "B", () => _context.Snapshots.SetMark(entry, "B"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("消す"), false, () =>
            {
                if (EditorUtility.DisplayDialog("保存した設定を消す", $"「{entry.name}」を一覧から消しますか？（Ctrl+Z で戻せます）", "消す", "やめる"))
                {
                    _context.Snapshots.Delete(entry);
                }
            });
            menu.ShowAsContext();
        }
    }
}
