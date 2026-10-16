using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 光ページ: 「光を当てる」が OFF のとき、光の値を灰色にして「今は効いていない」と知らせる。
    /// OFF のまま値を動かしても何も変わらず、気づけなかったため（2026-10-16）。
    /// </summary>
    public static class LightOffNotice
    {
        private const string ApplyPath = "lighting.apply";

        private const string OffText =
            "「光を当てる」が OFF なので、下の光の値は今は使われていません。シーンに残っている太陽（[Stage] Sun）と、シーンのまわりの明るさがそのまま使われます。ON にするとすぐ反映されます。";

        /// <summary>カードの中身を作り終えてから呼ぶ（それまでに足された光の項目が対象）。</summary>
        public static void Attach(VisualElement card, SerializedObject so)
        {
            var apply = so.FindProperty(ApplyPath);
            if (apply == null)
            {
                return;
            }

            var note = StudioUi.Note(OffText, NoteKind.Warn);
            card.Insert(Mathf.Min(1, card.childCount), note);

            void Show(bool on)
            {
                note.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
                card.Query<PropertyField>().ForEach(field =>
                {
                    if (IsLightingValue(field.bindingPath))
                    {
                        field.SetEnabled(on);
                    }
                });
            }

            Show(apply.boolValue);
            note.TrackPropertyValue(apply, property => Show(property.boolValue));
        }

        private static bool IsLightingValue(string path) =>
            !string.IsNullOrEmpty(path) && path.StartsWith("lighting.") && path != ApplyPath;
    }
}
