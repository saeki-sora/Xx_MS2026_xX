using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// ほかのプロジェクト・ほかの PC へ配るための機能。
    /// 「ほかのプロジェクトに入れる」＝フォルダを選ぶだけで、そのプロジェクトの Packages フォルダへ丸ごとコピー。
    /// 「配布用 zip を作る」＝別の PC へ渡す用。展開して Packages フォルダへ入れるだけ（入れ方.txt 付き）。
    /// </summary>
    internal static class PlayRecorderInstaller
    {
        public const string PackageName = "com.ms2026.playrecorder";
        const string LastTargetKey = "MS2026.PlayRecorder.LastInstallTarget";

        const string HowToText =
            "録画しながらプレイ（Play Recorder）の入れ方\r\n" +
            "\r\n" +
            "1. この zip を展開します（右クリック → すべて展開）。\r\n" +
            "2. 中の「" + PackageName + "」フォルダを、入れたい Unity プロジェクトの「Packages」フォルダへドラッグします。\r\n" +
            "   （Packages フォルダは Assets フォルダと同じ場所にあります）\r\n" +
            "3. Unity に戻ると、上のツールバー（再生ボタンの横）に「● 録画プレイ」ボタンが出ます。\r\n" +
            "\r\n" +
            "・Unity 2021.3 以降で動きます。Unity Recorder などのインストールは不要です。\r\n" +
            "・ボタンが見当たらないときは、メニューの Tools › 録画しながらプレイ からも使えます。\r\n" +
            "・録画はプロジェクトのフォルダの中の「Recordings」フォルダに保存されます。\r\n" +
            "・やめるときは Packages の中の「" + PackageName + "」フォルダを消すだけです。\r\n";

        /// <summary>このツール自身のフォルダ（Packages に入っていても Assets に入っていても見つける）。</summary>
        public static string PackageRoot()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PlayRecorderInstaller).Assembly);
            if (info != null && Directory.Exists(info.resolvedPath)) return info.resolvedPath;

            foreach (var guid in AssetDatabase.FindAssets("MS2026.PlayRecorder.Editor"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) continue;
                string editorDir = Path.GetDirectoryName(Path.GetFullPath(path));
                string root = Path.GetDirectoryName(editorDir);
                return File.Exists(Path.Combine(root, "package.json")) ? root : editorDir;
            }
            return null;
        }

        public static void InstallToOtherProject()
        {
            string src = PackageRoot();
            if (src == null)
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName, "このツールのフォルダが見つかりませんでした。", "OK");
                return;
            }

            string start = EditorPrefs.GetString(LastTargetKey, Path.GetDirectoryName(PlayRecorderSettings.ProjectRoot));
            string target = EditorUtility.OpenFolderPanel("録画ボタンを入れたい Unity プロジェクトのフォルダを選んでください（Assets フォルダがある場所）", start, "");
            if (string.IsNullOrEmpty(target)) return;

            // Assets や Packages を選んでしまっても受け付ける
            string name = Path.GetFileName(target.TrimEnd('/', '\\'));
            if (name == "Assets" || name == "Packages") target = Path.GetDirectoryName(target.TrimEnd('/', '\\'));

            if (!Directory.Exists(Path.Combine(target, "Assets")) || !Directory.Exists(Path.Combine(target, "ProjectSettings")))
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName,
                    "選んだフォルダは Unity プロジェクトではないようです。\n「Assets」と「ProjectSettings」が入っているフォルダを選んでください。", "OK");
                return;
            }
            if (string.Equals(Path.GetFullPath(target).TrimEnd('\\', '/'), PlayRecorderSettings.ProjectRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName, "このプロジェクトにはもう入っています。", "OK");
                return;
            }
            EditorPrefs.SetString(LastTargetKey, Path.GetDirectoryName(target));

            string dst = Path.Combine(target, "Packages", PackageName);
            if (Directory.Exists(dst))
            {
                if (!EditorUtility.DisplayDialog(PlayRecorderUi.ToolName,
                        "そのプロジェクトにはもう入っています。\nこのプロジェクトの版で上書きしますか？", "上書きする", "やめる"))
                    return;
                Directory.Delete(dst, true);
            }

            try
            {
                CopyDirectory(src, dst);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName, "コピーできませんでした。\n" + e.Message, "OK");
                return;
            }

            string versionNote = OldUnityNote(target);
            EditorUtility.DisplayDialog(PlayRecorderUi.ToolName,
                $"入れました。\n{dst}\n\nそのプロジェクトを Unity で開くと（開いているなら Unity に切り替えると）、ツールバーの再生ボタンの横に「● 録画プレイ」が出ます。{versionNote}",
                "OK");
        }

        public static void ExportZip()
        {
            string src = PackageRoot();
            if (src == null)
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName, "このツールのフォルダが見つかりませんでした。", "OK");
                return;
            }
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path = EditorUtility.SaveFilePanel("配布用 zip の保存先", desktop, "録画しながらプレイ_PlayRecorder.zip", "zip");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (File.Exists(path)) File.Delete(path);
                using (var fs = new FileStream(path, FileMode.CreateNew))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    AddText(zip, "入れ方.txt", HowToText);
                    foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(src.Length).TrimStart('\\', '/').Replace('\\', '/');
                        var entry = zip.CreateEntry(PackageName + "/" + rel, System.IO.Compression.CompressionLevel.Optimal);
                        using (var es = entry.Open())
                        using (var fsIn = File.OpenRead(file))
                            fsIn.CopyTo(es);
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(PlayRecorderUi.ToolName, "zip を作れませんでした。\n" + e.Message, "OK");
                return;
            }
            EditorUtility.RevealInFinder(path);
            Debug.Log($"[録画プレイ] 配布用 zip を作りました: {path}\n渡した相手は、展開して中のフォルダを Unity プロジェクトの Packages フォルダへ入れるだけです（入れ方.txt 同梱）。");
        }

        static void AddText(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name);
            using (var w = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(true)))
                w.Write(text);
        }

        static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }

        /// <summary>相手の Unity が 2021.3 より古ければ注意書きを返す。</summary>
        static string OldUnityNote(string projectRoot)
        {
            try
            {
                string pv = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
                if (!File.Exists(pv)) return "";
                foreach (var line in File.ReadAllLines(pv))
                {
                    if (!line.StartsWith("m_EditorVersion:")) continue;
                    string v = line.Substring("m_EditorVersion:".Length).Trim();
                    var parts = v.Split('.');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    {
                        bool old = major < 2021 || (major == 2021 && minor < 3);
                        if (old) return $"\n\n※そのプロジェクトは Unity {v} です。このツールは 2021.3 以降向けなので、動かない可能性があります。";
                    }
                }
            }
            catch { /* 読めなくても入れる */ }
            return "";
        }
    }
}
