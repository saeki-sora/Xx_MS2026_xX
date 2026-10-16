using System;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>光ページ: 「おすすめの光」のボタン一列と「既定値に戻す」。押すとステージの光の値をまとめて入れる（元に戻せる）。</summary>
    public static class LightPresetPanel
    {
        public static VisualElement Build(StageSet stage, SerializedObject so, Action onApplied)
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("おすすめの光", "押すと太陽とまわりの明るさをまとめて入れます。霧などほかの項目はそのまま。Ctrl+Z で戻せます。"));

            var row = StudioUi.Row();
            row.style.flexWrap = Wrap.Wrap;
            foreach (var preset in StageLightingPresets.All)
            {
                var target = preset;
                row.Add(StudioUi.Button(target.Label, () => Change(stage, so, onApplied, $"光のおすすめ: {target.Label}", s => target.ApplyTo(s)),
                    target.Tooltip + "（「光を当てる」も ON になります）", small: true));
            }

            card.Add(row);
            card.Add(StudioUi.Row(
                StudioUi.Spacer(),
                StudioUi.Button("既定値に戻す", () =>
                    {
                        if (EditorUtility.DisplayDialog("光を既定値に戻す", $"「{stage.Label}」の光の設定を、すべて初めて作ったときの値に戻します。\n（Ctrl+Z で元に戻せます）", "戻す", "やめる"))
                        {
                            Change(stage, so, onApplied, "光を既定値に戻す", StageLightingPresets.ResetToDefaults);
                        }
                    },
                    "光の設定（太陽・まわりの明るさ・霧など）をすべて初めの値に戻します。BGM はそのまま。", small: true)));
            return card;
        }

        private static void Change(StageSet stage, SerializedObject so, Action onApplied, string undoName, Action<StageLightingSettings> change)
        {
            if (stage == null)
            {
                return;
            }

            Undo.RecordObject(stage, undoName);
            change(stage.lighting);
            EditorUtility.SetDirty(stage);
            so.Update();
            onApplied?.Invoke();
        }
    }
}
