using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.StudioKit
{
    /// <summary>
    /// 点検結果の一覧（件数の帯＋問題のカード）。直し方がある問題には「直す」、対象がある問題には「選ぶ」ボタンを付ける。
    /// 中身が変わったときだけ作り直す（マウスを乗せている途中でちらつかないように）。
    /// </summary>
    public sealed class StudioIssueList : VisualElement
    {
        private readonly Action<UnityEngine.Object> _select;
        private readonly Action _afterFix;
        private readonly VisualElement _errors;
        private readonly VisualElement _warnings;
        private readonly VisualElement _infos;
        private readonly VisualElement _list;
        private readonly string _emptyBody;
        private string _shownKey;

        /// <param name="select">「選ぶ」を押したとき（対象を選んでシーンに映すなど）。</param>
        /// <param name="afterFix">「直す」の後（点検のやり直しなど）。</param>
        /// <param name="recheck">「今すぐ点検」を押したとき。</param>
        public StudioIssueList(Action<UnityEngine.Object> select, Action afterFix, Action recheck, string emptyBody)
        {
            _select = select;
            _afterFix = afterFix;
            _emptyBody = emptyBody;

            _errors = StudioUi.Stat("0", "要対応", "遊べなくなる・正しく動かない問題の数。");
            _warnings = StudioUi.Stat("0", "注意", "見た目や遊び心地に関わる問題の数。");
            _infos = StudioUi.Stat("0", "お知らせ", "知っておくとよいことの数。");
            var summary = StudioUi.Row(_errors, _warnings, _infos, StudioUi.Spacer(),
                StudioUi.Button("今すぐ点検", recheck, "すぐに点検をやり直します。", small: true));
            summary.style.marginBottom = 10;
            Add(summary);

            _list = new VisualElement();
            Add(_list);
        }

        public static (int errors, int warnings) Count(IReadOnlyList<StudioIssue> issues)
        {
            int errors = 0, warnings = 0;
            foreach (var issue in issues)
            {
                if (issue.Severity == StudioIssueSeverity.Error) errors++;
                else if (issue.Severity == StudioIssueSeverity.Warning) warnings++;
            }

            return (errors, warnings);
        }

        public void Show(IReadOnlyList<StudioIssue> issues)
        {
            var key = Key(issues);
            if (key == _shownKey)
            {
                return;
            }

            _shownKey = key;
            int errors = 0, warnings = 0, infos = 0;
            _list.Clear();
            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case StudioIssueSeverity.Error: errors++; break;
                    case StudioIssueSeverity.Warning: warnings++; break;
                    default: infos++; break;
                }

                _list.Add(BuildIssue(issue));
            }

            StudioUi.SetStat(_errors, errors.ToString());
            StudioUi.SetStat(_warnings, warnings.ToString());
            StudioUi.SetStat(_infos, infos.ToString());
            if (issues.Count == 0)
            {
                _list.Add(StudioUi.Empty("✓", "問題は見つかりませんでした", _emptyBody));
            }
        }

        private VisualElement BuildIssue(StudioIssue issue)
        {
            var kind = issue.Severity == StudioIssueSeverity.Error ? NoteKind.Error
                : issue.Severity == StudioIssueSeverity.Warning ? NoteKind.Warn : NoteKind.Info;
            var note = StudioUi.Note(issue.Title, kind);
            var title = note.Q<Label>(className: "sk-note-text");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;
            title.RemoveFromHierarchy();
            column.Add(title);
            column.Add(StudioUi.Styled(new Label(issue.Detail), "sk-card-body"));

            var buttons = StudioUi.Row();
            buttons.style.marginTop = 4;
            if (issue.Fix != null)
            {
                buttons.Add(StudioUi.Button(issue.FixLabel, () =>
                {
                    issue.Fix();
                    _shownKey = null;
                    _afterFix?.Invoke();
                }, "この問題を自動で直します（元に戻すは Ctrl+Z）。", primary: true, small: true));
            }

            if (issue.Target != null && _select != null)
            {
                buttons.Add(StudioUi.Button("選ぶ", () => _select(issue.Target), "問題のある物を選び、シーンに映します。", small: true));
            }

            if (buttons.childCount > 0)
            {
                column.Add(buttons);
            }

            note.Add(column);
            return note;
        }

        private static string Key(IReadOnlyList<StudioIssue> issues)
        {
            var builder = new StringBuilder();
            foreach (var issue in issues)
            {
                builder.Append((int)issue.Severity).Append(issue.Title).Append('|');
            }

            return builder.ToString();
        }
    }
}
