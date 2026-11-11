using System.Linq;
using System.Text;
using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// ページの上の細い帯: 試し撃ち（今すぐ撃つ・撃ち続ける）と、保存して比べる（A・B・切り替え・保存した一覧）。
    /// 毎回は触らない物なので、大きな枠にせず1行に収める（使いやすさの資料の T8: 作業する項目を主役にする）。
    /// </summary>
    public sealed class SkillToolbar : VisualElement
    {
        private readonly SkillStudioContext _context;
        private readonly SkillColor _color;
        private readonly Button _fire;
        private readonly Toggle _repeat;
        private readonly Button _play;
        private readonly Button _attach;
        private readonly Label _status;
        private readonly Label _abState;

        public SkillToolbar(SkillStudioContext context, SkillColor color)
        {
            _context = context;
            _color = color;
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            style.alignItems = Align.Center;
            style.marginBottom = 10;

            var test = new Label("試し撃ち") { tooltip = "Play 中に、このスキルを撃って見た目や強さを確かめる。値を変えるとその場で変わる。" };
            test.style.opacity = 0.6f;
            test.style.marginRight = 6;
            Add(test);

            _play = StudioUi.Button("Play を始める", () => EditorApplication.isPlaying = true, "今開いているシーンで Play を始める（試し撃ちは Play 中だけ使える）。", small: true);
            Add(_play);
            _fire = StudioUi.Button("今すぐ撃つ", FireAll, "このスキルを持っている砲台が、溜まっていなくても今すぐ撃つ。", primary: true, small: true);
            Add(_fire);
            _repeat = new Toggle("撃ち続ける")
            {
                tooltip = "ON の間、撃ち終わるたびにすぐ次を撃つ（見た目を調整しながら見続けるとき用）。Play を止めると OFF に戻る。"
            };
            _repeat.style.marginLeft = 6;
            _repeat.style.marginRight = 6;
            _repeat.RegisterValueChangedCallback(e =>
            {
                if (e.newValue)
                {
                    _context.AutoRefire.Add(_color);
                }
                else
                {
                    _context.AutoRefire.Remove(_color);
                }
            });
            Add(_repeat);
            _attach = StudioUi.Button("砲台に付ける ▾", ShowAttachMenu, "このスキルを持っている砲台がないとき、Play の間だけ P1〜P4 のどれかに付けて試す。", small: true);
            Add(_attach);
            _status = new Label();
            _status.style.opacity = 0.6f;
            _status.style.marginLeft = 4;
            Add(_status);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            var compare = new Label("比べる") { tooltip = "今の値を A と B に取っておき、切り替えて見比べる。Play 中ならその場で見た目が変わる。読み込みは Ctrl+Z で戻せる。" };
            compare.style.opacity = 0.6f;
            compare.style.marginRight = 6;
            Add(compare);
            Add(StudioUi.Button("A に保存", () => Save("A"), "今の値を A として取っておく（前の A は一覧に残る）。", small: true));
            Add(StudioUi.Button("B に保存", () => Save("B"), "今の値を B として取っておく（前の B は一覧に残る）。", small: true));
            Add(StudioUi.Button("A ⇄ B", ToggleAB, "A と B を交互に読み込む。", small: true));
            Add(StudioUi.Button("保存した一覧 ▾", ShowSavedMenu, "今の値を保存する・保存した値を読み込む（全部／レベルデザインだけ／見た目だけ）・印を付ける・消す。", small: true));
            _abState = new Label();
            _abState.style.opacity = 0.6f;
            _abState.style.marginLeft = 6;
            Add(_abState);
        }

        public void Refresh()
        {
            var playing = Application.isPlaying;
            var skill = _context.SkillOf(_color);
            var slots = SkillStudioContext.SlotsUsing(skill);
            _play.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            _fire.style.display = playing && slots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _repeat.style.display = playing && slots.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _attach.style.display = playing && slots.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _repeat.SetValueWithoutNotify(_context.AutoRefire.Contains(_color));

            var status = new StringBuilder();
            foreach (var slot in slots)
            {
                status.Append($"P{slot.PlayerIndex + 1} {StateText(slot.State)}　");
            }

            _status.text = status.ToString();
            var last = _context.Snapshots.LastMark(skill);
            _abState.text = last != null ? $"今: {last}" : "";
        }

        private void FireAll()
        {
            foreach (var slot in SkillStudioContext.SlotsUsing(_context.SkillOf(_color)))
            {
                SkillStudioContext.FireNow(slot);
            }
        }

        private void ShowAttachMenu()
        {
            var skill = _context.SkillOf(_color);
            var menu = new GenericMenu();
            for (var p = 0; p < 4; p++)
            {
                var slot = PlayerSkillSlot.Find(p);
                var label = new GUIContent($"P{p + 1} に付ける（Play を止めると元に戻る）");
                if (slot == null || skill == null)
                {
                    menu.AddDisabledItem(label);
                }
                else
                {
                    menu.AddItem(label, false, () => slot.SetSkill(skill));
                }
            }

            menu.ShowAsContext();
        }

        private void Save(string mark)
        {
            _context.Snapshots.Save(_context.SkillOf(_color), "", mark);
            Refresh();
        }

        private void ToggleAB()
        {
            if (!_context.Snapshots.ToggleAB(_context.SkillOf(_color)))
            {
                EditorUtility.DisplayDialog("A ⇄ B", "先に「A に保存」「B に保存」で2つ取っておいてください。", "OK");
            }

            Refresh();
        }

        private void ShowSavedMenu()
        {
            var skill = _context.SkillOf(_color);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("今の値を保存する"), false, () => _context.Snapshots.Save(skill, ""));
            var entries = _context.Snapshots.EntriesOf(skill).Take(20).ToList();
            if (entries.Count > 0)
            {
                menu.AddSeparator("");
            }

            foreach (var entry in entries)
            {
                var target = entry;
                var title = (string.IsNullOrEmpty(entry.mark) ? "" : $"[{entry.mark}] ") + entry.name + "　" + entry.savedAt;
                title = title.Replace("/", "／");
                menu.AddItem(new GUIContent(title + "/全部読み込む"), false, () => _context.Snapshots.Apply(target, SnapshotScope.All));
                menu.AddItem(new GUIContent(title + "/レベルデザインだけ読み込む"), false, () => _context.Snapshots.Apply(target, SnapshotScope.Level));
                menu.AddItem(new GUIContent(title + "/見た目だけ読み込む"), false, () => _context.Snapshots.Apply(target, SnapshotScope.Look));
                menu.AddSeparator(title + "/");
                menu.AddItem(new GUIContent(title + "/A の印を付ける"), entry.mark == "A", () => _context.Snapshots.SetMark(target, "A"));
                menu.AddItem(new GUIContent(title + "/B の印を付ける"), entry.mark == "B", () => _context.Snapshots.SetMark(target, "B"));
                menu.AddSeparator(title + "/");
                menu.AddItem(new GUIContent(title + "/消す"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("保存した値を消す", $"「{target.name}」を一覧から消しますか？（Ctrl+Z で戻せます）", "消す", "やめる"))
                    {
                        _context.Snapshots.Delete(target);
                    }
                });
            }

            menu.ShowAsContext();
        }

        private static string StateText(PlayerSkillState state) => state switch
        {
            PlayerSkillState.Empty => "溜まっていない",
            PlayerSkillState.Ready => "使える",
            PlayerSkillState.Active => "撃っている",
            _ => "スキルなし"
        };
    }
}
