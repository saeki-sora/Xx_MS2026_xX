using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>コアのページ: 歯の汚れ・ふちの光・ひびの値と、トゥーンの塗り（材質）を開くボタン。</summary>
    public static class CoreLookPanel
    {
        public static VisualElement Build(CoreCrystalController core)
        {
            var card = StudioUi.Card();
            var so = new SerializedObject(core);

            card.Add(StudioUi.Section("汚れ・ふちの光・ひび"));
            card.Add(Field(so, "toothLook.enabled", "出す"));
            card.Add(Field(so, "toothLook.stainColor", "しみの色"));
            card.Add(Field(so, "toothLook.stainDeepColor", "しみの色（HP が少ないとき）"));
            card.Add(Field(so, "toothLook.stainScale", "しみの細かさ"));
            card.Add(Field(so, "toothLook.stainFromRoot", "根元から汚れる量"));
            card.Add(Field(so, "toothLook.rimStrength", "ふちの光の強さ"));
            card.Add(Field(so, "toothLook.rimWidth", "ふちの光の幅"));
            card.Add(Field(so, "toothLook.rimSoftness", "ふちの光のぼかし"));
            card.Add(Field(so, "toothLook.inkColor", "ひびの線の色"));
            card.Add(Field(so, "toothLook.crackScale", "ひびの細かさ"));
            card.Add(Field(so, "toothLook.crackWidth", "ひびの線の太さ"));

            card.Add(StudioUi.Section("色と速さ（宝石の見た目と共通）", "要塞デザイナーのコアの宝石の設定と同じ値です。どちらで変えても同じです。"));
            card.Add(Field(so, "gemLook.glowColor", "ふちの光の色（HP が多いとき）"));
            card.Add(Field(so, "gemLook.hurtColor", "ふちの光の色（HP が少ないとき）"));
            card.Add(Field(so, "gemLook.calmPulsePerSecond", "脈の速さ（HP が多いとき・回/秒）"));
            card.Add(Field(so, "gemLook.hurtPulsePerSecond", "脈の速さ（HP が少ないとき・回/秒）"));
            card.Add(Field(so, "gemLook.crackStartHealth01", "ひびが入り始める HP"));
            card.Add(Field(so, "gemLook.crackColor", "ひびから漏れる光の色"));
            card.Add(Field(so, "gemLook.hitFlashSeconds", "ダメージで白く光る秒数"));

            card.Add(StudioUi.Section("トゥーンの塗り", "影の色・明暗の境目・輪郭線などは歯の材質にあります。項目はステージの物のトゥーンと同じです。"));
            card.Add(StudioUi.Row(StudioUi.Button("歯の材質を開く", () =>
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(CoreToothSetup.MaterialPath);
                if (material != null)
                {
                    Selection.activeObject = material;
                    EditorGUIUtility.PingObject(material);
                }
            }, "Inspector に歯の材質を出します（影の色・輪郭線の色と太さ など）。", small: true)));
            card.Add(StudioUi.Note("汚れの進み方（HP に対するカーブ）は、コアの「CoreDirtVisual」のカーブを使います。", NoteKind.Info));

            card.Bind(so);
            card.TrackSerializedObjectValue(so, _ => SceneView.RepaintAll());
            return card;
        }

        private static PropertyField Field(SerializedObject so, string path, string label)
        {
            var property = so.FindProperty(path);
            return new PropertyField(property, label) { tooltip = property?.tooltip };
        }
    }
}
