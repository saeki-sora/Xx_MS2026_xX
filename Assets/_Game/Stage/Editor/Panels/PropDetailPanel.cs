using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>選んだ背景オブジェクトの詳細（区画を縦に並べるだけ）。複数選んでいるときは、まとめて変える操作を出す。</summary>
    public sealed class PropDetailPanel : VisualElement
    {
        private readonly StageStudioContext _context;
        private readonly List<PropSection> _sections = new List<PropSection>
        {
            new PropRoleSection(),
            new PropFootprintSection(),
            new PropLookSection(),
            new PropModelSection()
        };

        private readonly VisualElement _single;
        private readonly VisualElement _multi;
        private readonly VisualElement _empty;
        private readonly TextField _name;
        private readonly Label _multiTitle;
        private StageProp _bound;

        public PropDetailPanel(StageStudioContext context)
        {
            _context = context;
            AddToClassList("sk-detail");

            _empty = StudioUi.Empty("◎", "背景オブジェクトを選んでください", "左の一覧か、シーンビュー・ヒエラルキーで選ぶと、ここに設定が出ます。");
            Add(_empty);

            _single = new VisualElement();
            _name = new TextField("名前") { tooltip = "背景オブジェクトの名前（ヒエラルキーに出る名前）。" };
            _name.RegisterValueChangedCallback(e =>
            {
                if (_bound != null && e.newValue != _bound.name)
                {
                    Undo.RecordObject(_bound.gameObject, "名前を変更");
                    _bound.name = e.newValue;
                    _context.MarkPropsDirty();
                }
            });
            _single.Add(_name);
            foreach (var section in _sections)
            {
                _single.Add(section.Root);
            }

            Add(_single);

            _multi = new VisualElement();
            _multiTitle = StudioUi.Styled(new Label(), "sk-page-title");
            _multi.Add(_multiTitle);
            _multi.Add(BuildBatchActions());
            Add(_multi);

            RegisterCallback<AttachToPanelEvent>(_ => { _context.SelectionChanged += Rebind; Rebind(); });
            RegisterCallback<DetachFromPanelEvent>(_ => _context.SelectionChanged -= Rebind);
            Rebind();
        }

        public void Refresh()
        {
            if (_bound != _context.Primary)
            {
                Rebind();
            }

            if (_bound == null)
            {
                return;
            }

            if (_name.panel?.focusController?.focusedElement != _name)
            {
                _name.SetValueWithoutNotify(_bound.name);
            }

            foreach (var section in _sections)
            {
                section.Refresh();
            }
        }

        private void Rebind()
        {
            var count = _context.SelectedProps.Count;
            _bound = count == 1 ? _context.Primary : null;
            _empty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _single.style.display = count == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _multi.style.display = count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _multiTitle.text = $"{count} 個を選択中";

            if (_bound != null)
            {
                _name.SetValueWithoutNotify(_bound.name);
            }

            foreach (var section in _sections)
            {
                section.Bind(_bound);
            }
        }

        private VisualElement BuildBatchActions()
        {
            var card = StudioUi.Card("まとめて変える", "選んでいる物すべてに同じ操作をします。");
            var roles = StudioUi.Styled(new VisualElement(), "sk-row");
            foreach (var role in StageRoleStyle.All)
            {
                var button = StudioUi.Button($"{StageRoleStyle.Icon(role)} {role.DisplayName()}にする", () => SetRoleAll(role), StageRoleStyle.Explain(role), small: true);
                button.style.color = StageRoleStyle.Color(role);
                roles.Add(button);
            }

            card.Add(roles);
            card.Add(StudioUi.Row(
                StudioUi.Button("通れない範囲を作り直す", () => ForEach(p => StagePropComposer.RebuildFootprint(p)), "選んでいる物すべての輪郭を作り直します。", small: true),
                StudioUi.Button("専用シェーダーに切り替え", () => ForEach(ConvertMaterials), "選んでいる物すべてのマテリアルを専用シェーダーのコピーに替えます。", small: true)));
            return card;
        }

        private void SetRoleAll(StagePropRole role)
        {
            ForEach(p => StagePropComposer.SetRole(p, role, message => Debug.LogWarning($"[StageStudio] {p.name}: {message}")));
            ForEach(RefreshConvertedMaterials);
        }

        private void ForEach(System.Action<StageProp> action)
        {
            foreach (var prop in new List<StageProp>(_context.SelectedProps))
            {
                if (prop != null)
                {
                    action(prop);
                }
            }

            _context.RecheckNow();
        }

        private static void ConvertMaterials(StageProp prop)
        {
            var root = StageSceneService.FindRoot();
            StageMaterialConverter.Convert(prop, StageAssetFactory.FolderOf(root != null ? root.current : null));
        }

        /// <summary>役割を変えたとき、既に専用シェーダーの物だけ、床用／通常用のコピーを付け替える（未切り替えの物は勝手に替えない）。</summary>
        private static void RefreshConvertedMaterials(StageProp prop)
        {
            if (StageMaterialConverter.CountUnconverted(prop) == 0)
            {
                ConvertMaterials(prop);
            }
        }
    }
}
