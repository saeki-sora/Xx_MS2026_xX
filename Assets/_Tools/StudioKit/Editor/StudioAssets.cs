using System;
using System.IO;
using UnityEditor;

namespace MS2026.StudioKit
{
    /// <summary>ツールが作るアセットの置き場所（フォルダ）を安全に用意する。</summary>
    public static class StudioAssets
    {
        /// <summary>フォルダが無ければ、親から順に作る（"Assets/A/B/C" の A・B・C を足りない分だけ）。</summary>
        public static void EnsureFolder(string folder)
        {
            folder = Normalize(folder);
            if (string.IsNullOrEmpty(folder) || folder == "Assets" || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            if (!folder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Assets フォルダの中ではありません: {folder}");
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// parent の中の、まだ使われていない名前のパス（"名前"、"名前 1"…）を返す。parent が無ければ先に作る。
        /// （AssetDatabase.GenerateUniqueAssetPath は親フォルダが無いと空文字を返すため。）
        /// </summary>
        public static string UniquePath(string parent, string fileName)
        {
            EnsureFolder(parent);
            return AssetDatabase.GenerateUniqueAssetPath($"{Normalize(parent)}/{fileName}");
        }

        /// <summary>ファイル名に使えない文字を _ に置き換える（空なら fallback）。</summary>
        public static string SafeFileName(string name, string fallback = "New")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        private static string Normalize(string path) => path?.Replace('\\', '/').TrimEnd('/');
    }
}
