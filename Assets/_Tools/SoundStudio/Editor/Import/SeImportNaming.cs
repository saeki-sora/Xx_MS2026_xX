using System.IO;
using System.Linq;
using System.Text;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// wav を落としたときの英字の名前と、D-Drive の素材フォルダの中の置き場所を決める。
    /// D-Drive はフォルダ名も番号札の名前に入れる（<c>Se/Title/Start.wav</c> → <c>SEID.TitleStart</c>）ので、名前の頭にフォルダ名を重ねない。
    /// </summary>
    public static class SeImportNaming
    {
        private static readonly string[] SoundExtensions = { ".wav", ".mp3", ".ogg", ".aiff", ".aif" };

        public static bool IsSoundFile(string path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return SoundExtensions.Contains(ext);
        }

        /// <summary>英字と数字だけの名前（単語の頭を大文字）。名前に英字が無ければ元のファイル名から作る。どちらも無ければ <c>Sound</c>。</summary>
        public static string SuggestName(string label, string sourceFileName)
        {
            var name = Pascal(label);
            if (name.Length == 0)
            {
                name = Pascal(Path.GetFileNameWithoutExtension(sourceFileName ?? string.Empty));
            }

            if (name.Length == 0)
            {
                return "Sound";
            }

            // 番号札の名前は数字で始められない。
            return char.IsDigit(name[0]) ? "S" + name : name;
        }

        /// <summary>素材の置き場所: <c>&lt;root&gt;/Se/&lt;場面&gt;/&lt;名前&gt;&lt;拡張子&gt;</c>（BGM は Bgm）。</summary>
        public static string MakeFileName(string sourceRoot, SoundPageId page, string name, string extension, bool isBgm)
        {
            var folder = SoundPageIds.SeFolder(page);
            var clean = SuggestName(name, null);
            if (clean.Length > folder.Length && clean.StartsWith(folder, System.StringComparison.Ordinal))
            {
                clean = clean.Substring(folder.Length);
                if (char.IsDigit(clean[0]))
                {
                    clean = "S" + clean;
                }
            }

            var ext = string.IsNullOrEmpty(extension) ? ".wav" : extension.ToLowerInvariant();
            return sourceRoot.TrimEnd('/') + "/" + (isBgm ? "Bgm" : "Se") + "/" + folder + "/" + clean + ext;
        }

        private static string Pascal(string text)
        {
            var builder = new StringBuilder();
            var upper = true;
            foreach (var c in text ?? string.Empty)
            {
                var ascii = c < 128 && char.IsLetterOrDigit(c);
                if (!ascii)
                {
                    upper = true;
                    continue;
                }

                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }

            return builder.ToString();
        }
    }
}
