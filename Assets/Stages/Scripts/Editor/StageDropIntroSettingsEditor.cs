using UnityEditor;
using UnityEngine;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// 試合の始まりの演出（ステージの物が上から落ちてくる）の設定のインスペクター。
    /// 説明・Play 中に「もう一度落としてみる」ボタン、メニュー「Tools › ステージ › 開始の演出（落ちてくる）の設定を開く」。
    /// </summary>
    [CustomEditor(typeof(StageDropIntroSettings))]
    internal sealed class StageDropIntroSettingsEditor : Editor
    {
        private const string AssetPath = "Assets/Stages/Resources/" + StageDropIntroSettings.ResourcePath + ".asset";

        [MenuItem("Tools/ステージ/開始の演出（落ちてくる）の設定を開く", false, 25)]
        private static void Open()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StageDropIntroSettings>(AssetPath);
            if (settings == null)
            {
                settings = CreateInstance<StageDropIntroSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Stage] 開始の演出の設定を作りました: " + AssetPath);
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "ゲームのシーンに入って幕が消えたら、障害物（ばらばらの順）→ コア → 1P〜4P の順に、画面の上から落ちてきて、着地でつぶれて戻ります。\n" +
                "全ステージ共通の設定です。ステージごとに切るときは、そのステージ（Stages/Data の Stage_N）の「Play Drop Intro」を OFF に。" +
                "1 つの物だけ落とさないときは、その物に「開始の演出で落とさない」（StageDropIntroSkip）を付けてください。床は落ちません。\n" +
                "演出が終わるまで WAVE 1 は始まりません（全員のPCの演出が終わってから）。",
                MessageType.None);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                var label = EditorApplication.isPlaying ? "もう一度落としてみる（Play 中）" : "もう一度落としてみる（Play 中だけ）";
                if (GUILayout.Button(new GUIContent(label, "今のステージの物を、今の設定でもう一度落とす。試しなので、WAVE の進み・HUD・レーザーには影響しない。値を変えたらこのボタンで見比べられる。"), GUILayout.Height(28f)))
                {
                    if (!StageDropIntro.PlayTest())
                    {
                        Debug.LogWarning("[Stage] ステージが読み込まれていないので、落とせません（ステージの目印 StageLayout が見つからない）。");
                    }
                }
            }

            var active = StageDropIntro.Active;
            if (EditorApplication.isPlaying && active != null && active.IsRunning)
            {
                EditorGUILayout.LabelField("いま落としている物", active.TargetCount + " 個");
                Repaint();
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
