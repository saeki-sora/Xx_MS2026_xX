using System.Text;
using MS2026.Skills;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// 試し撃ち（Play 中）。このスキルを持っている砲台ごとに「今すぐ撃つ」「使えるようにする」「止める」、
    /// 「撃ち終わったらすぐもう一度」（見た目を調整しながら撃ち続ける）。だれも持っていなければ、P1〜P4 に一時的に付けて試せる。
    /// Play 中に変えた値は設定ファイルに残る（Play を止めても戻らない）。
    /// </summary>
    public sealed class SkillTestPanel : VisualElement
    {
        private readonly SkillStudioContext _context;
        private readonly SkillColor _color;
        private readonly VisualElement _note;
        private readonly VisualElement _rows;
        private readonly Toggle _autoRefire;
        private readonly Button _playButton;
        private string _lastSignature;

        public SkillTestPanel(SkillStudioContext context, SkillColor color)
        {
            _context = context;
            _color = color;

            var card = StudioUi.Card("試し撃ち", null);
            card.style.marginBottom = 10;
            Add(card);

            _note = StudioUi.Note("Play 中に使えます。値を変えると、その場で見た目や強さが変わります。", NoteKind.Info);
            card.Add(_note);

            _playButton = StudioUi.Button("Play を始める", () => EditorApplication.isPlaying = true, "今開いているシーンで Play を始める。", primary: true, small: true);
            _playButton.style.marginTop = 6;
            card.Add(_playButton);

            _autoRefire = new Toggle("撃ち終わったらすぐもう一度撃つ")
            {
                tooltip = "ON の間、このスキルを持っている砲台が、撃ち終わるたびにすぐ次を撃つ（カットインがある設定ならカットインも毎回出る）。見た目を調整しながら見続けるとき用。Play を止めると OFF に戻る。"
            };
            _autoRefire.RegisterValueChangedCallback(e =>
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
            _autoRefire.style.marginTop = 6;
            card.Add(_autoRefire);

            _rows = new VisualElement();
            _rows.style.marginTop = 4;
            card.Add(_rows);
        }

        public void Refresh()
        {
            var playing = Application.isPlaying;
            var skill = _context.SkillOf(_color);
            _playButton.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            _autoRefire.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            _autoRefire.SetValueWithoutNotify(_context.AutoRefire.Contains(_color));
            StudioUi.SetNote(_note, playing
                    ? "値を変えると、その場で見た目や強さが変わります（Play を止めても値は残ります）。"
                    : "Play 中に使えます。値を変えると、その場で見た目や強さが変わります。",
                playing ? NoteKind.Ok : NoteKind.Info);

            var slots = SkillStudioContext.SlotsUsing(skill);
            var signature = BuildSignature(playing, slots);
            if (signature == _lastSignature)
            {
                return;
            }

            _lastSignature = signature;
            _rows.Clear();
            if (!playing || skill == null)
            {
                return;
            }

            if (slots.Count == 0)
            {
                _rows.Add(StudioUi.Note("今のシーンには、このスキルを持っている砲台がありません。下のボタンで一時的に付けて試せます（Play を止めると元に戻ります）。", NoteKind.Warn));
                var row = StudioUi.Row();
                row.style.marginTop = 4;
                for (var p = 0; p < 4; p++)
                {
                    var index = p;
                    var slot = PlayerSkillSlot.Find(index);
                    var button = StudioUi.Button($"P{index + 1} に付ける", () => slot?.SetSkill(skill),
                        $"P{index + 1} の砲台のスキルを、Play の間だけこのスキルにする。", small: true);
                    button.SetEnabled(slot != null);
                    row.Add(button);
                }

                _rows.Add(row);
                return;
            }

            foreach (var slot in slots)
            {
                var target = slot;
                var row = StudioUi.Row();
                row.style.alignItems = Align.Center;
                row.style.marginTop = 4;
                var label = new Label($"P{slot.PlayerIndex + 1}　{StateText(slot.State)}");
                label.style.width = 150;
                row.Add(label);
                row.Add(StudioUi.Button("今すぐ撃つ", () => SkillStudioContext.FireNow(target), "溜まっていなくても、今すぐこのスキルを撃つ。", primary: true, small: true));
                row.Add(StudioUi.Button("使えるようにする", () => target.GrantCharge(), "溜まった状態にする（次に握ったときに撃つ）。", small: true));
                row.Add(StudioUi.Button("止める", () => target.CancelActive(), "撃っている最中のスキルをすぐ止める。", small: true));
                _rows.Add(row);
            }
        }

        private static string BuildSignature(bool playing, System.Collections.Generic.List<PlayerSkillSlot> slots)
        {
            var builder = new StringBuilder(playing ? "P" : "E");
            foreach (var slot in slots)
            {
                builder.Append(slot.PlayerIndex).Append(':').Append((int)slot.State).Append(';');
            }

            return builder.ToString();
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
