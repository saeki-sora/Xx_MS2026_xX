using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 見た目の欄: 検索・変えた所の数と「すべて戻す」・トゥーンになっていない物の知らせ・よく触る8つ・やりたいこと別の折りたたみ。
    /// 項目は「いま調整している材質」（ふだんは共通の設定）の値を動かす。
    /// </summary>
    public sealed class ToonLookSection : VisualElement
    {
        private readonly ToonContext _context;
        private readonly List<ToonPropertyRow> _rows = new List<ToonPropertyRow>();
        private readonly Dictionary<ToonGroup, Foldout> _folds = new Dictionary<ToonGroup, Foldout>();
        private readonly HashSet<Foldout> _openedBySearch = new HashSet<Foldout>();
        private readonly Label _summary;
        private readonly Button _resetAll;
        private readonly VisualElement _missingNote;
        private readonly Label _missingText;
        private readonly Label _noMatch;
        private readonly VisualElement _topHeader;
        private string _query = "";

        public ToonLookSection(ToonContext context)
        {
            _context = context;
            var sample = context.Common;

            var search = new ToolbarSearchField { tooltip = "項目の名前か見出しで絞り込みます（例: 線、影、色）。" };
            search.AddToClassList("tw-search");
            search.RegisterValueChangedCallback(e => ApplySearch(e.newValue));
            Add(search);

            _summary = new Label();
            _summary.AddToClassList("tw-summary-text");
            _resetAll = StudioUi.Button("すべて戻す", ResetAll, "いま調整している物の、変えた所を全部戻します（Ctrl+Z で取り消せます）。", small: true);
            var summaryRow = new VisualElement();
            summaryRow.AddToClassList("tw-summary");
            summaryRow.Add(_summary);
            summaryRow.Add(_resetAll);
            Add(summaryRow);

            _missingNote = new VisualElement();
            _missingNote.AddToClassList("tw-missing");
            _missingText = new Label();
            _missingText.AddToClassList("tw-missing-text");
            _missingNote.Add(_missingText);
            _missingNote.Add(StudioUi.Button("追加", () => _context.AddSceneMaterials(sprite: false),
                "対象のシーンで使っているのに、まだトゥーンになっていない物のトゥーン材質を作ってオンにします（元の材質・シーンは変えません）。", primary: true, small: true));
            Add(_missingNote);

            _topHeader = StudioUi.Section("よく触る項目", "迷ったらここだけ触れば大丈夫です。残りは下の見出しの中にあります。");
            Add(_topHeader);
            foreach (var item in ToonCatalog.Items)
            {
                if (item.Top)
                {
                    Add(AddRow(item, sample));
                }
            }

            foreach (var group in ToonCatalog.FoldGroups)
            {
                var fold = new Foldout { value = false };
                fold.AddToClassList("tw-fold");
                // 中のスイッチの変化も上がってくるので、見出し自身を開け閉めしたときだけ数える。
                fold.RegisterValueChangedCallback(e =>
                {
                    if (e.target == fold)
                    {
                        _openedBySearch.Remove(fold);
                    }
                });
                foreach (var item in ToonCatalog.Items)
                {
                    if (item.Group == group && !item.Top)
                    {
                        fold.Add(AddRow(item, sample));
                    }
                }

                if (ToonCatalog.IsPerMaterialGroup(group))
                {
                    fold.tooltip = "この物だけの値です（共通の設定からは配られません）。物ごとに調整しているときだけ出ます。";
                }

                _folds[group] = fold;
                Add(fold);
            }

            _noMatch = new Label("合う項目はありません。");
            _noMatch.AddToClassList("tw-muted");
            Add(_noMatch);

            context.ValuesChanged += Refresh;
            context.TargetChanged += Refresh;
            context.TableChanged += RefreshMissing;
            Refresh();
            RefreshMissing();
        }

        private ToonPropertyRow AddRow(ToonItem item, UnityEngine.Material sample)
        {
            var row = new ToonPropertyRow(_context, item, sample);
            _rows.Add(row);
            return row;
        }

        /// <summary>全部の行の値と印・見出しの数・変えた所の数を合わせる。</summary>
        public void Refresh()
        {
            bool common = _context.IsCommon;
            int changed = 0;
            var perGroup = new Dictionary<ToonGroup, int>();
            foreach (var row in _rows)
            {
                row.Pull();
                if (row.Changed)
                {
                    changed++;
                    if (!row.Item.Top)
                    {
                        perGroup.TryGetValue(row.Item.Group, out var n);
                        perGroup[row.Item.Group] = n + 1;
                    }
                }
            }

            foreach (var pair in _folds)
            {
                perGroup.TryGetValue(pair.Key, out var n);
                pair.Value.text = ToonCatalog.GroupLabel(pair.Key) + (n > 0 ? "　（変えた所 " + n + "）" : "");
                pair.Value.EnableInClassList("tw-fold--changed", n > 0);
            }

            _summary.text = changed > 0
                ? "変えた所: " + changed + (common ? "（初期値から）" : "（共通の設定から）")
                : "変えた所: なし";
            _resetAll.SetEnabled(changed > 0);
            ApplySearch(_query);
        }

        private void RefreshMissing()
        {
            int count = _context.Missing.Count;
            _missingNote.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _missingText.text = "トゥーンになっていない物の材質が " + count + " 個あります";
            _missingNote.tooltip = "まだトゥーンになっていない材質: " + string.Join("、", NameList());
        }

        private IEnumerable<string> NameList()
        {
            foreach (var material in _context.Missing)
            {
                yield return material.name;
            }
        }

        // 検索: 合う行だけ残し、合う行がある見出しは開く。消したら、検索で開いた見出しは閉じ直す。
        private void ApplySearch(string query)
        {
            _query = query ?? "";
            bool searching = !string.IsNullOrWhiteSpace(_query);
            bool perMaterial = !_context.IsCommon;
            int visible = 0;
            foreach (var row in _rows)
            {
                bool show = ToonCatalog.Matches(row.Item, _query) && !row.ClassListContains("tw-row--missing");
                row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show && (perMaterial || !row.Item.PerMaterial))
                {
                    visible++;
                }
            }

            bool anyTop = false;
            foreach (var row in _rows)
            {
                anyTop |= row.Item.Top && row.style.display == DisplayStyle.Flex;
            }

            _topHeader.style.display = anyTop ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var pair in _folds)
            {
                var fold = pair.Value;
                bool any = false;
                foreach (var child in fold.Children())
                {
                    any |= child is ToonPropertyRow && child.style.display == DisplayStyle.Flex;
                }

                bool allowed = perMaterial || !ToonCatalog.IsPerMaterialGroup(pair.Key);
                fold.style.display = any && allowed ? DisplayStyle.Flex : DisplayStyle.None;
                if (searching && any && !fold.value)
                {
                    fold.SetValueWithoutNotify(true);
                    _openedBySearch.Add(fold);
                }
                else if (!searching && _openedBySearch.Contains(fold))
                {
                    fold.SetValueWithoutNotify(false);
                    _openedBySearch.Remove(fold);
                }
            }

            _noMatch.style.display = searching && visible == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ResetAll()
        {
            var what = _context.IsCommon ? "全部の物の見え方（共通の設定）を、初期値" : _context.TargetLabel + "の値を、共通の設定の値";
            if (!EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", what + "に戻しますか？\n（Ctrl+Z で取り消せます）", "戻す", "キャンセル"))
            {
                return;
            }

            var baseline = _context.Baseline;
            _context.Write(material =>
            {
                foreach (var row in _rows)
                {
                    if (row.Changed)
                    {
                        ToonValues.Reset(material, baseline, row.Item);
                    }
                }
            }, "すべて戻す");
        }
    }
}
