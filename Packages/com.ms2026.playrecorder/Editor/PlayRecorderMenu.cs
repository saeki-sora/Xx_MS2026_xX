using System.IO;
using UnityEditor;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>メニュー Tools › 録画しながらプレイ（ツールバーにボタンが出ない Unity でも使える入口）。</summary>
    internal static class PlayRecorderMenu
    {
        const string Root = "Tools/" + PlayRecorderUi.ToolName + "/";

        // Ctrl+Shift+Alt+R（Mac は Cmd+Shift+Option+R）
        [MenuItem(Root + "録画プレイ（開始・停止） %#&r", priority = 0)]
        static void Toggle() => PlayRecorderController.Toggle();

        [MenuItem(Root + "録画フォルダを開く", priority = 20)]
        static void OpenFolder() => PlayRecorderUi.OpenOutputFolder();

        [MenuItem(Root + "最後の録画を再生", priority = 21)]
        static void PlayLast() => EditorUtility.OpenWithDefaultApp(PlayRecorderSettings.LastOutputPath);

        [MenuItem(Root + "最後の録画を再生", true)]
        static bool PlayLastValidate()
        {
            string p = PlayRecorderSettings.LastOutputPath;
            return !string.IsNullOrEmpty(p) && File.Exists(p);
        }

        [MenuItem(Root + "ツールバーにボタンを表示する", priority = 30)]
        static void ShowButton() => PlayRecorderToolbar.ShowButton();

        [MenuItem(Root + "設定…", priority = 40)]
        static void Settings() => PlayRecorderUi.OpenSettings();

        [MenuItem(Root + "ほかのプロジェクトに入れる…", priority = 60)]
        static void Install() => PlayRecorderInstaller.InstallToOtherProject();

        [MenuItem(Root + "配布用 zip を作る…", priority = 61)]
        static void Zip() => PlayRecorderInstaller.ExportZip();
    }
}
