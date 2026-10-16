using System;
using System.Collections.Generic;
using System.Linq;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>光ページ: ほかのステージの光の設定を、このステージへまるごとコピーする（BGM はコピーしない）。</summary>
    public static class LightCopyPanel
    {
        public static VisualElement Build(StageSet stage, SerializedObject so, Action onApplied)
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("ほかのステージから光をコピー", "別のステージで作った光（太陽・まわりの明るさ・霧など）を、このステージにまるごと入れます。BGM はそのまま。"));

            var others = FindStages().Where(s => s != stage).ToList();
            if (others.Count == 0)
            {
                card.Add(StudioUi.Note("ほかのステージがまだありません。", NoteKind.Info));
                return card;
            }

            var popup = new PopupField<StageSet>("コピー元", others, 0, Format, Format)
            {
                tooltip = "光をもらうステージ。"
            };
            popup.style.flexGrow = 1;
            var copy = StudioUi.Button("この光をコピー", () => Copy(stage, popup.value, so, onApplied),
                "選んだステージの光の設定を、このステージに上書きします（Ctrl+Z で戻せます）。", small: true);
            var row = StudioUi.Row(popup, copy);
            row.style.alignItems = Align.Center;
            card.Add(row);
            return card;
        }

        /// <summary>光の設定をまるごと写す（後から増えた項目も含む）。</summary>
        public static void CopyLighting(StageLightingSettings from, StageLightingSettings to)
        {
            if (from == null || to == null)
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);
        }

        private static void Copy(StageSet stage, StageSet source, SerializedObject so, Action onApplied)
        {
            if (stage == null || source == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("光をコピー", $"「{source.Label}」の光の設定を「{stage.Label}」に上書きします。\n（Ctrl+Z で元に戻せます）", "コピーする", "やめる"))
            {
                return;
            }

            Undo.RecordObject(stage, $"光をコピー（{source.Label}から）");
            CopyLighting(source.lighting, stage.lighting);
            EditorUtility.SetDirty(stage);
            so.Update();
            onApplied?.Invoke();
        }

        private static IEnumerable<StageSet> FindStages() =>
            AssetDatabase.FindAssets("t:" + nameof(StageSet))
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageSet>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(s => s != null)
                .OrderBy(s => s.Label, StringComparer.Ordinal);

        private static string Format(StageSet stage) => stage != null ? stage.Label : "（なし）";
    }
}
