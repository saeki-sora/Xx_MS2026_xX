using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// メニュー「Tools/キャラ選択/歯磨き粉の幕に連番を入れる」。フォルダの連番PNG（名前順）を、歯磨き粉の幕の「連番の絵」に入れる。
    /// 画像の取り込み設定（ミップマップなし・端をくり返さない・透明を使う）もそろえる。絵描きさんが書き出し直したときに使う。
    /// </summary>
    internal static class PasteCurtainFramesMenu
    {
        private const string DefaultFolder = "Assets/_Game/UI/Transitions/Frames/Transition1";
        private const string FolderPrefKey = "MS2026.CharacterSelect.CurtainFramesFolder";

        [MenuItem("Tools/キャラ選択/歯磨き粉の幕に連番を入れる", priority = 23)]
        private static void Import()
        {
            var start = EditorPrefs.GetString(FolderPrefKey, DefaultFolder);
            var picked = EditorUtility.OpenFolderPanel("幕の連番のフォルダ（プロジェクトの Assets の中）", start, "");
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            var folder = ToAssetPath(picked);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("歯磨き粉の幕", "プロジェクトの Assets フォルダの中を選んでください（先に連番をプロジェクトへコピーします）。", "OK");
                return;
            }

            EditorPrefs.SetString(FolderPrefKey, folder);
            var paths = Directory.GetFiles(folder, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();
            if (paths.Length < 2)
            {
                EditorUtility.DisplayDialog("歯磨き粉の幕", "PNG が2枚以上あるフォルダを選んでください。", "OK");
                return;
            }

            foreach (var path in paths)
            {
                ConfigureImport(path);
            }

            var settings = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CharacterSelectSceneBuilder.CurtainSettingsPath);
            if (settings == null)
            {
                CharacterSelectSceneBuilder.SelectCurtainSettings();
                settings = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CharacterSelectSceneBuilder.CurtainSettingsPath);
            }

            Undo.RecordObject(settings, "歯磨き粉の幕に連番を入れる");
            settings.frames = paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            EditorUtility.DisplayDialog("歯磨き粉の幕",
                $"{paths.Length} 枚を入れました。\n\n「画面を覆いきるコマ」（今 {settings.coveredFrame}）と「抜け始めるコマ」（今 {settings.revealFrame}）が新しい連番に合っているか、設定で確かめてください。",
                "OK");
        }

        private static void ConfigureImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            if (importer.textureType == TextureImporterType.Default && !importer.mipmapEnabled &&
                importer.wrapMode == TextureWrapMode.Clamp && importer.alphaIsTransparency)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static string ToAssetPath(string absolute)
        {
            var full = Path.GetFullPath(absolute).Replace('\\', '/').TrimEnd('/');
            var assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            if (full == assets)
            {
                return "Assets";
            }

            return full.StartsWith(assets + "/") ? "Assets" + full.Substring(assets.Length) : null;
        }
    }
}
