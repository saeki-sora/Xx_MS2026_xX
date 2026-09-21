using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace MS2026.SpriteAnim.Editor
{
    /// <summary>
    /// SpriteAnimationSet 内のアニメーション名から、"Walk" のようなマジックストリングを
    /// 使わずに済む const string の静的クラスを生成する。プログラマー向けの安全策。
    /// </summary>
    public static class SpriteAnimNameCodeGenerator
    {
        public static void Generate(SpriteAnimationSet set, string savePath, string className, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// このファイルは スプライトアニメスタジオによって自動生成されました。手動編集した内容は次回生成時に上書きされます。");

            bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
            string indent = hasNamespace ? "    " : "";

            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"{indent}public static class {className}");
            sb.AppendLine($"{indent}{{");

            foreach (var anim in set.Animations)
            {
                if (anim == null) continue;
                string constName = ToPascalCase(anim.AnimationName);
                sb.AppendLine($"{indent}    public const string {constName} = \"{anim.AnimationName}\";");
            }

            sb.AppendLine($"{indent}}}");
            if (hasNamespace) sb.AppendLine("}");

            File.WriteAllText(savePath, sb.ToString());
            AssetDatabase.Refresh();
        }

        private static string ToPascalCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Animation";

            var parts = s.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }

            string result = sb.ToString();
            if (result.Length == 0) return "Animation";
            if (char.IsDigit(result[0])) result = "_" + result;
            return result;
        }
    }
}
