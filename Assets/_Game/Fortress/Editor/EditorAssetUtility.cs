using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// エディタツール間で重複しがちな「フォルダが無ければ作る」「アセットが無ければ作る」を共通化する。
    /// Phase1TestSceneBuilderやFortressDesignerWindowの各タブから利用する。
    /// </summary>
    public static class EditorAssetUtility
    {
        /// <summary>指定パスのアセットを読み込む。無ければ新規作成して返す。</summary>
        public static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>指定パスのフォルダが無ければ、親フォルダを遡って再帰的に作成する。</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
