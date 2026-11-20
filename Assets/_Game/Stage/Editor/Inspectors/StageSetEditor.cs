using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージのまとめ（StageSet）を日本語の項目名で並べる。
    /// よく触る「ステージ」の項目だけを最初に見せ、光と空中の粒は折りたたみに入れる。
    /// </summary>
    [CustomEditor(typeof(StageSet))]
    public sealed class StageSetEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = StageInspectorUi.Root();
            var set = (StageSet)target;

            root.Add(StudioUi.Note("背景オブジェクト（stage_frame・床など）1つ1つの見た目は、ここではなく「プレハブを開く」から各オブジェクトを選んで変えます。"));

            root.Add(StudioUi.Section("ステージ"));
            StageInspectorUi.AddFields(root, serializedObject,
                ("displayName", "表示名"),
                ("stagePrefab", "背景オブジェクトのプレハブ"));

            var open = new Button(() =>
            {
                if (set.stagePrefab != null)
                {
                    AssetDatabase.OpenAsset(set.stagePrefab);
                }
            }) { text = "プレハブを開く", tooltip = "背景オブジェクトのプレハブをプレハブ編集モードで開きます。" };
            open.SetEnabled(set.stagePrefab != null);
            root.Add(open);

            StageInspectorUi.AddFields(root, serializedObject,
                ("cameraPreset", "カメラの見え方（4人分の視点）"),
                ("lookProfile", "反応の共通設定（透け・熱・揺れなど）"),
                ("bgm", "BGM"));

            var info = StageInspectorUi.Folded("メモ・一覧の絵", "StageSet.Info");
            StageInspectorUi.AddFields(info, serializedObject,
                ("memo", "メモ"),
                ("thumbnail", "一覧に出す絵"));
            root.Add(info);

            var light = StageInspectorUi.Folded("光（太陽・環境光・霧・明るさ）", "StageSet.Lighting");
            StageInspectorUi.AddFields(light, serializedObject,
                ("lighting.apply", "この光の設定を使う"),
                ("lighting.sunColor", "太陽の色"),
                ("lighting.sunIntensity", "太陽の明るさ"),
                ("lighting.sunDirection", "太陽の向き（度。0=画面の上から）"),
                ("lighting.sunElevation", "太陽の高さ（度。90=真上）"),
                ("lighting.shadowStrength", "影の濃さ"),
                ("lighting.highlightFocus", "照り返しの集まり方"),
                ("lighting.skyColor", "上からの環境光の色"),
                ("lighting.equatorColor", "横からの環境光の色"),
                ("lighting.groundColor", "下からの環境光の色"),
                ("lighting.fog", "霧を出す"),
                ("lighting.fogColor", "霧の色"),
                ("lighting.fogStart", "霧が始まる距離"),
                ("lighting.fogEnd", "霧が濃くなりきる距離"),
                ("lighting.toneMapping", "白飛びを抑える"),
                ("lighting.exposure", "画面全体の明るさ（露出）"));
            root.Add(light);

            var motes = StageInspectorUi.Folded("空中をただよう粒（0と1の文字・泡・湯気）", "StageSet.Motes");
            StageInspectorUi.AddFields(motes, serializedObject,
                ("ambientMotes.enabled", "粒を出す"),
                ("ambientMotes.dataCount", "データの粒（0と1の文字）の数"),
                ("ambientMotes.bubbleCount", "泡の数"),
                ("ambientMotes.steamCount", "湯気の数"),
                ("ambientMotes.dataColor", "データの粒の色"),
                ("ambientMotes.bubbleColor", "泡の色"),
                ("ambientMotes.steamColor", "湯気の色"),
                ("ambientMotes.dataSize", "データの粒の文字の高さ（m）"),
                ("ambientMotes.bubbleSize", "泡の大きさ（最小〜最大, m）"),
                ("ambientMotes.steamSize", "湯気の大きさ（最小〜最大, m）"),
                ("ambientMotes.height", "床からの高さ（最小〜最大, m）"),
                ("ambientMotes.riseSpeed", "上へ流れる速さ（m/秒）"),
                ("ambientMotes.sway", "左右に揺れる大きさ（m）"),
                ("ambientMotes.lifeSeconds", "見えている秒数（最小〜最大）"));
            root.Add(motes);

            root.Bind(serializedObject);
            return root;
        }
    }
}
