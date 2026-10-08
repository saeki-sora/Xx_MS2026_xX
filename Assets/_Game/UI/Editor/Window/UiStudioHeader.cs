using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>UIスタジオ上の帯の右側: 今編集している画面・サンプル値の表示切り替え・置き場所が無いときの作成ボタン。</summary>
    public sealed class UiStudioHeader : VisualElement
    {
        private readonly UiStudioContext _context;
        private readonly Label _editing;
        private readonly Toggle _samples;
        private readonly Button _create;
        private readonly Button _closeStage;

        public UiStudioHeader(UiStudioContext context)
        {
            _context = context;
            AddToClassList("sk-row");

            _samples = new Toggle("サンプル値で表示")
            {
                tooltip = "ONなら、Play していなくても、値の一覧のサンプル値で文字・ゲージ・色を表示します（「値」ページで変えられます）。"
            };
            _samples.RegisterValueChangedCallback(e => _context.ShowSamples = e.newValue);
            _samples.style.marginRight = 12;
            Add(_samples);

            _editing = StudioUi.Chip("", ChipKind.Plain);
            Add(_editing);

            _closeStage = StudioUi.Button("編集を終える", () => StageUtility.GoToMainStage(), "Prefab の編集画面を閉じて、シーンに戻ります（変更は自動保存の設定に従います）。", small: true);
            _closeStage.style.marginLeft = 6;
            Add(_closeStage);

            _create = StudioUi.Button("UIの置き場所を作る", () => UiStudioSetup.CreateRoot(), "シーンに「[UI]」を作ります。レイヤーの Canvas・ゲームの値の書き込み役・EventSystem も用意します。", primary: true, small: true);
            _create.style.marginLeft = 6;
            Add(_create);
        }

        public void Refresh()
        {
            _samples.SetValueWithoutNotify(_context.ShowSamples);
            _samples.SetEnabled(!Application.isPlaying);

            var editing = _context.EditingScreen;
            var inStage = PrefabStageUtility.GetCurrentPrefabStage() != null;
            _editing.text = editing != null ? $"編集中: {editing.Label}" : "編集中の画面なし";
            _editing.tooltip = editing != null
                ? "レイヤー・動きのページは、この画面の部品を対象にします。"
                : "「画面」ページで画面を選んで「この画面を編集」を押すと、部品を触れるようになります。";
            StudioUi.SetChipKind(_editing, editing != null ? ChipKind.Accent : ChipKind.Plain);
            _closeStage.style.display = inStage ? DisplayStyle.Flex : DisplayStyle.None;
            _create.style.display = _context.Root == null && !inStage ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
