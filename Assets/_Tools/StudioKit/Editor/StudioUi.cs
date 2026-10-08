using System;
using UnityEngine.UIElements;

namespace MS2026.StudioKit
{
    public enum NoteKind
    {
        Info,
        Warn,
        Error,
        Ok
    }

    public enum ChipKind
    {
        Plain,
        Ok,
        Warn,
        Error,
        Info,
        Accent
    }

    /// <summary>
    /// StudioKit の見た目の部品を作る小さな工場。どの部品にも tooltip を付けられ、
    /// マウスを乗せると画面下の帯（StudioShell）に説明が出る。
    /// </summary>
    public static class StudioUi
    {
        public static Label PageTitle(string text) => Styled(new Label(text), "sk-page-title");

        public static Label Lead(string text) => Styled(new Label(text), "sk-page-lead");

        public static VisualElement Section(string title, string tooltip = null)
        {
            var row = Styled(new VisualElement(), "sk-section");
            row.tooltip = tooltip;
            row.Add(Styled(new Label(title), "sk-section-title"));
            row.Add(Styled(new VisualElement(), "sk-section-line"));
            return row;
        }

        public static VisualElement Card(string title = null, string body = null)
        {
            var card = Styled(new VisualElement(), "sk-card");
            if (!string.IsNullOrEmpty(title))
            {
                card.Add(Styled(new Label(title), "sk-card-title"));
            }

            if (!string.IsNullOrEmpty(body))
            {
                card.Add(Styled(new Label(body), "sk-card-body"));
            }

            return card;
        }

        /// <summary>押せるカード（選択肢の一覧など）。</summary>
        public static VisualElement ClickCard(string title, string body, Action onClick, string tooltip = null)
        {
            var card = Card(title, body);
            card.AddToClassList("sk-card--clickable");
            card.tooltip = tooltip;
            card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            return card;
        }

        public static Button Button(string text, Action onClick, string tooltip = null, bool primary = false, bool small = false)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList("sk-button");
            button.EnableInClassList("sk-button--primary", primary);
            button.EnableInClassList("sk-button--small", small);
            return button;
        }

        public static Label Chip(string text, ChipKind kind = ChipKind.Plain, string tooltip = null)
        {
            var chip = Styled(new Label(text), "sk-chip");
            chip.tooltip = tooltip;
            SetChipKind(chip, kind);
            return chip;
        }

        public static void SetChipKind(VisualElement chip, ChipKind kind)
        {
            chip.EnableInClassList("sk-chip--ok", kind == ChipKind.Ok);
            chip.EnableInClassList("sk-chip--warn", kind == ChipKind.Warn);
            chip.EnableInClassList("sk-chip--error", kind == ChipKind.Error);
            chip.EnableInClassList("sk-chip--info", kind == ChipKind.Info);
            chip.EnableInClassList("sk-chip--accent", kind == ChipKind.Accent);
        }

        public static VisualElement Note(string text, NoteKind kind = NoteKind.Info)
        {
            var note = Styled(new VisualElement(), "sk-note");
            var icon = Styled(new Label(), "sk-note-icon");
            var label = Styled(new Label(text), "sk-note-text");
            note.Add(icon);
            note.Add(label);
            SetNote(note, text, kind);
            return note;
        }

        /// <summary>Note の文言と種類を後から変える。</summary>
        public static void SetNote(VisualElement note, string text, NoteKind kind)
        {
            note.EnableInClassList("sk-note--warn", kind == NoteKind.Warn);
            note.EnableInClassList("sk-note--error", kind == NoteKind.Error);
            note.EnableInClassList("sk-note--ok", kind == NoteKind.Ok);
            note.Q<Label>(className: "sk-note-icon").text = kind switch
            {
                NoteKind.Warn => "!",
                NoteKind.Error => "×",
                NoteKind.Ok => "✓",
                _ => "i"
            };
            note.Q<Label>(className: "sk-note-text").text = text;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = Styled(new VisualElement(), "sk-row");
            foreach (var child in children)
            {
                row.Add(child);
            }

            return row;
        }

        public static VisualElement Spacer() => Styled(new VisualElement(), "sk-spacer");

        public static VisualElement Stat(string value, string label, string tooltip = null)
        {
            var stat = Styled(new VisualElement(), "sk-stat");
            stat.tooltip = tooltip;
            stat.Add(Styled(new Label(value) { name = "value" }, "sk-stat-value"));
            stat.Add(Styled(new Label(label), "sk-stat-label"));
            return stat;
        }

        public static void SetStat(VisualElement stat, string value) => stat.Q<Label>("value").text = value;

        /// <summary>まだ何も無いときの案内（大きなアイコン＋説明＋ボタン）。</summary>
        public static VisualElement Empty(string icon, string title, string body, string buttonText = null, Action onClick = null)
        {
            var root = Styled(new VisualElement(), "sk-empty");
            root.Add(Styled(new Label(icon), "sk-empty-icon"));
            root.Add(Styled(new Label(title), "sk-empty-title"));
            root.Add(Styled(new Label(body), "sk-empty-body"));
            if (!string.IsNullOrEmpty(buttonText))
            {
                root.Add(Button(buttonText, onClick, primary: true));
            }

            return root;
        }

        /// <summary>番号付きの手順（はじめにのページ用）。</summary>
        public static VisualElement Step(int number, string title, string body, out Label numberLabel, params VisualElement[] actions)
        {
            var card = Card();
            var row = Styled(new VisualElement(), "sk-step");
            numberLabel = Styled(new Label(number.ToString()), "sk-step-number");
            var text = Styled(new VisualElement(), "sk-step-body");
            text.Add(Styled(new Label(title), "sk-card-title"));
            text.Add(Styled(new Label(body), "sk-card-body"));
            if (actions.Length > 0)
            {
                var buttons = Row(actions);
                buttons.style.marginTop = 8;
                text.Add(buttons);
            }

            row.Add(numberLabel);
            row.Add(text);
            card.Add(row);
            return card;
        }

        public static void SetStepDone(Label numberLabel, bool done, int number)
        {
            numberLabel.EnableInClassList("sk-step-number--done", done);
            numberLabel.text = done ? "✓" : number.ToString();
        }

        /// <summary>アイコン付きの大きめの選択肢を横に並べる（1つだけ選べる）。</summary>
        public static VisualElement Segment<T>(
            (T value, string icon, string label, string tooltip)[] options,
            Func<T> current,
            Action<T> onSelect,
            out Action refresh)
        {
            var root = Styled(new VisualElement(), "sk-segment");
            var items = new VisualElement[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var item = Styled(new VisualElement(), "sk-segment-item");
                item.tooltip = option.tooltip;
                item.Add(Styled(new Label(option.icon), "sk-segment-icon"));
                item.Add(Styled(new Label(option.label), "sk-segment-label"));
                item.RegisterCallback<ClickEvent>(_ => onSelect(option.value));
                items[i] = item;
                root.Add(item);
            }

            refresh = () =>
            {
                var value = current();
                for (var i = 0; i < options.Length; i++)
                {
                    items[i].EnableInClassList("sk-segment-item--active", Equals(options[i].value, value));
                }
            };
            refresh();
            return root;
        }

        public static T Styled<T>(T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
