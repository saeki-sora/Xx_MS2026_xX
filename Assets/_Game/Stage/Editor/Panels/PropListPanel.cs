using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>背景オブジェクトの一覧（検索・役割で絞り込み・問題のある物に印）。クリックで選択、Ctrl/Shift+クリックで追加選択。</summary>
    public sealed class PropListPanel : VisualElement
    {
        private readonly StageStudioContext _context;
        private readonly TextField _search;
        private readonly VisualElement _filters;
        private readonly VisualElement _rows;
        private readonly HashSet<StagePropRole> _hidden = new HashSet<StagePropRole>();
        private int _lastSignature;

        public PropListPanel(StageStudioContext context)
        {
            _context = context;
            AddToClassList("sk-list");

            _search = new TextField { tooltip = "名前で絞り込みます。" };
            _search.AddToClassList("sk-search");
            _search.RegisterValueChangedCallback(_ => Rebuild());
            Add(_search);

            _filters = StudioUi.Styled(new VisualElement(), "sk-row");
            _filters.style.marginBottom = 6;
            foreach (var role in StageRoleStyle.All)
            {
                var chip = StudioUi.Chip($"{StageRoleStyle.Icon(role)} {role.DisplayName()}", ChipKind.Plain, $"クリックで「{role.DisplayName()}」を一覧に出す／隠す。");
                chip.style.color = StageRoleStyle.Color(role);
                chip.style.marginBottom = 4;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_hidden.Remove(role))
                    {
                        _hidden.Add(role);
                    }

                    chip.style.opacity = _hidden.Contains(role) ? 0.35f : 1f;
                    Rebuild();
                });
                _filters.Add(chip);
            }

            Add(_filters);
            _rows = new VisualElement();
            Add(_rows);

            // 画面に出ている間だけ選択の変化を受け取る（ページを作り直したときに古い一覧が残らないように）。
            RegisterCallback<AttachToPanelEvent>(_ => _context.SelectionChanged += Rebuild);
            RegisterCallback<DetachFromPanelEvent>(_ => _context.SelectionChanged -= Rebuild);
        }

        /// <summary>中身が変わったときだけ作り直す。</summary>
        public void Refresh()
        {
            var signature = ComputeSignature();
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            _rows.Clear();
            var filter = _search.value?.Trim() ?? string.Empty;
            var issuesByTarget = new Dictionary<Object, StudioIssueSeverity>();
            foreach (var issue in _context.Issues)
            {
                if (issue.Target != null && (!issuesByTarget.TryGetValue(issue.Target, out var s) || issue.Severity < s))
                {
                    issuesByTarget[issue.Target] = issue.Severity;
                }
            }

            foreach (var prop in _context.Props)
            {
                if (prop == null || _hidden.Contains(prop.role) ||
                    (filter.Length > 0 && prop.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                _rows.Add(BuildRow(prop, issuesByTarget));
            }

            if (_rows.childCount == 0)
            {
                _rows.Add(StudioUi.Styled(new Label(_context.Props.Count == 0 ? "まだありません。「取り込み」から入れてください。" : "条件に合う物がありません。"), "sk-muted"));
            }
        }

        private VisualElement BuildRow(StageProp prop, Dictionary<Object, StudioIssueSeverity> issues)
        {
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.EnableInClassList("sk-list-item--selected", ContainsSelected(prop));
            row.tooltip = $"{prop.role.DisplayName()}: {StageRoleStyle.Explain(prop.role)}\nダブルクリックでシーンビューに映します。";

            var dot = StudioUi.Styled(new VisualElement(), "sk-list-dot");
            dot.style.backgroundColor = StageRoleStyle.Color(prop.role);
            row.Add(dot);
            row.Add(StudioUi.Styled(new Label(prop.name), "sk-list-label"));

            if (issues.TryGetValue(prop, out var severity))
            {
                row.Add(StudioUi.Chip(severity == StudioIssueSeverity.Error ? "×" : severity == StudioIssueSeverity.Warning ? "!" : "i",
                    severity == StudioIssueSeverity.Error ? ChipKind.Error : severity == StudioIssueSeverity.Warning ? ChipKind.Warn : ChipKind.Info,
                    "この物に点検の指摘があります（「点検」ページで確認）。"));
            }

            row.RegisterCallback<ClickEvent>(e =>
            {
                _context.Select(prop, e.ctrlKey || e.commandKey || e.shiftKey);
                if (e.clickCount == 2)
                {
                    StageStudioContext.Frame(prop);
                }
            });
            return row;
        }

        private bool ContainsSelected(StageProp prop)
        {
            foreach (var selected in _context.SelectedProps)
            {
                if (selected == prop)
                {
                    return true;
                }
            }

            return false;
        }

        private int ComputeSignature()
        {
            unchecked
            {
                var hash = _context.Props.Count;
                foreach (var prop in _context.Props)
                {
                    hash = hash * 31 + (prop != null ? prop.GetInstanceID() * 7 + (int)prop.role + prop.name.GetHashCode() : 0);
                }

                foreach (var issue in _context.Issues)
                {
                    hash = hash * 17 + (issue.Target != null ? issue.Target.GetInstanceID() : 0) + (int)issue.Severity;
                }

                return hash;
            }
        }
    }
}
