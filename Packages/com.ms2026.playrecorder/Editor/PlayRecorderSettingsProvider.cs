using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>Edit › Preferences › 録画しながらプレイ の画面。</summary>
    internal static class PlayRecorderSettingsProvider
    {
        static readonly double[] PreviewDurations = { 30, 60, 120, 180, 300, 600, 1200 };

        static readonly GUIContent LimitLabel = new GUIContent("上限サイズ (MB)",
            "書き出す動画の大きさの上限。1MB = 1,000,000 バイトで数えます。録画の長さに合わせて画質を自動で決め、必ずこの大きさ以内に収めます。");
        static readonly GUIContent MaxHeightLabel = new GUIContent("最大の解像度",
            "短い録画でもこれより大きくはしません。小さくするほど、長い録画でも画質の落ち方がゆるやかになります。");
        static readonly GUIContent MaxFpsLabel = new GUIContent("最大の fps",
            "1 秒あたりのコマ数の上限。60 にすると短い録画がなめらかになりますが、そのぶん早く解像度が下がります。");
        static readonly GUIContent AudioLabel = new GUIContent("音も録る",
            "スピーカーに出ている音（AudioListener の音）を一緒に録ります。長い録画で音が上限を圧迫するときは、自動でモノラル → 音なしにします。");
        static readonly GUIContent FolderLabel = new GUIContent("保存先フォルダ",
            "プロジェクトのフォルダ（Assets と同じ場所）からの相対パス、またはフルパス。初期値の Recordings は Unity 標準の .gitignore で Git に入らない場所です。");
        static readonly GUIContent RevealLabel = new GUIContent("書き出したらフォルダを開く");
        static readonly GUIContent FlipLabel = new GUIContent("上下反転の補正",
            "録画が上下さかさまになったときだけ変えてください。自動 = グラフィックス API に合わせて補正します。");
        static readonly string[] FlipNames = { "自動", "反転する", "反転しない" };

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider(PlayRecorderUi.PreferencesPath, SettingsScope.User)
            {
                label = PlayRecorderUi.ToolName,
                guiHandler = _ => Draw(),
                keywords = new[] { "録画", "Recorder", "動画", "MP4", "20MB" },
            };
        }

        static void Draw()
        {
            EditorGUIUtility.labelWidth = 200;
            EditorGUILayout.HelpBox(
                "ツールバーの「● 録画プレイ」を押すと、プレイしながらゲーム画面と音を録画します。プレイを止めると MP4 に自動で書き出します。\n" +
                "長く録るほど解像度・fps を自動で下げて、上限サイズに必ず収めます（途中で録画を切ることはありません）。\n" +
                "設定はこの PC の Unity 全体で共通です。", MessageType.Info);
            EditorGUILayout.Space();

            PlayRecorderSettings.LimitMB = EditorGUILayout.FloatField(LimitLabel, PlayRecorderSettings.LimitMB);
            PlayRecorderSettings.MaxHeight = IntPopup(MaxHeightLabel, PlayRecorderSettings.MaxHeight, PlayRecorderSettings.MaxHeightChoices, "p");
            PlayRecorderSettings.MaxFps = IntPopup(MaxFpsLabel, PlayRecorderSettings.MaxFps, PlayRecorderSettings.MaxFpsChoices, " fps");
            PlayRecorderSettings.RecordAudio = EditorGUILayout.Toggle(AudioLabel, PlayRecorderSettings.RecordAudio);

            using (new EditorGUILayout.HorizontalScope())
            {
                PlayRecorderSettings.OutputFolder = EditorGUILayout.TextField(FolderLabel, PlayRecorderSettings.OutputFolder);
                if (GUILayout.Button("選ぶ…", GUILayout.Width(60)))
                {
                    string picked = EditorUtility.OpenFolderPanel("録画の保存先", PlayRecorderSettings.OutputFolderAbsolute, "");
                    if (!string.IsNullOrEmpty(picked)) PlayRecorderSettings.OutputFolder = ToProjectRelative(picked);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("開く", GUILayout.Width(50))) PlayRecorderUi.OpenOutputFolder();
            }
            PlayRecorderSettings.RevealWhenDone = EditorGUILayout.Toggle(RevealLabel, PlayRecorderSettings.RevealWhenDone);
            PlayRecorderSettings.Flip = (FlipMode)EditorGUILayout.Popup(FlipLabel, (int)PlayRecorderSettings.Flip, FlipNames);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("録画の長さと画質の目安（16:9 の画面のとき）", EditorStyles.boldLabel);
            foreach (var sec in PreviewDurations)
            {
                var plan = PlayRecorderUi.EstimatedPlan(sec);
                EditorGUILayout.LabelField($"  {MovieEncodeJob.FormatTime(sec)} 録画",
                    $"{plan.Width}x{plan.Height}  {plan.Fps}fps  （映像 {plan.BitsPerSecond / 1000} kbps）");
            }
            EditorGUILayout.LabelField(" ", "※実際の大きさは書き出し後に確かめ、超えていたら自動で縮め直します。", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("ほかのプロジェクト・ほかの PC へ", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ほかのプロジェクトに入れる…")) PlayRecorderInstaller.InstallToOtherProject();
                if (GUILayout.Button("配布用 zip を作る…")) PlayRecorderInstaller.ExportZip();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("既定値に戻す", GUILayout.Width(120))) PlayRecorderSettings.ResetToDefaults();
        }

        static int IntPopup(GUIContent label, int value, int[] choices, string suffix)
        {
            var names = new GUIContent[choices.Length];
            for (int i = 0; i < choices.Length; i++) names[i] = new GUIContent(choices[i] + suffix);
            return EditorGUILayout.IntPopup(label, value, names, choices);
        }

        static string ToProjectRelative(string absolute)
        {
            string root = PlayRecorderSettings.ProjectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            string a = Path.GetFullPath(absolute).Replace('\\', '/');
            return a.StartsWith(root, System.StringComparison.OrdinalIgnoreCase) ? a.Substring(root.Length) : a;
        }
    }
}
