using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>コアのページ: Game シーンの保存（保存していない変更があるかの表示つき）。</summary>
    public static class CoreSavePanel
    {
        private const string ChipName = "core-save-chip";

        public static VisualElement Build(CoreCrystalController core)
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("保存"));
            var chip = StudioUi.Chip("", ChipKind.Plain);
            chip.name = ChipName;
            card.Add(StudioUi.Row(
                StudioUi.Button("Game シーンを保存", () => CoreToothSetup.SaveCoreScene(core), "コアの変更を Game シーンに保存します（ステージの保存とは別です）。", primary: true),
                StudioUi.Spacer(),
                chip));
            Update(chip, core);
            return card;
        }

        public static void Refresh(VisualElement root, CoreCrystalController core)
        {
            var chip = root.Q<Label>(ChipName);
            if (chip != null)
            {
                Update(chip, core);
            }
        }

        private static void Update(Label chip, CoreCrystalController core)
        {
            var dirty = core != null && core.gameObject.scene.isDirty;
            chip.text = dirty ? "保存していない変更があります" : "保存済み";
            StudioUi.SetChipKind(chip, dirty ? ChipKind.Warn : ChipKind.Ok);
        }
    }
}
