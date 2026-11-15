using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>CPU の強さ（3段階。段階ごとに「試合の始め → 慣れきったとき」の幅を持つ）。</summary>
    public enum CpuLevel
    {
        [InspectorName("やさしい")]
        Easy = 0,

        [InspectorName("ふつう")]
        Normal = 1,

        [InspectorName("つよい")]
        Hard = 2
    }

    public static class CpuLevels
    {
        public const int Count = 3;

        /// <summary>上手さの目盛り（0 = 初めて遊ぶ人、1 = 少し慣れた人、2 = 上手い人、3 = とても上手い人）。強さ + その分野の慣れ（0〜1）。</summary>
        public static float Scale(CpuLevel level, float familiarity01) => (int)level + Mathf.Clamp01(familiarity01);

        public static string Label(CpuLevel level)
        {
            switch (level)
            {
                case CpuLevel.Easy: return "やさしい";
                case CpuLevel.Hard: return "つよい";
                default: return "ふつう";
            }
        }

        /// <summary>起動オプションの文字（easy / normal / hard、1〜3、やさしい／ふつう／つよい）から読む。</summary>
        public static bool TryParse(string text, out CpuLevel level)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "easy":
                case "1":
                case "やさしい":
                    level = CpuLevel.Easy;
                    return true;
                case "normal":
                case "2":
                case "ふつう":
                    level = CpuLevel.Normal;
                    return true;
                case "hard":
                case "3":
                case "つよい":
                    level = CpuLevel.Hard;
                    return true;
                default:
                    level = CpuLevel.Normal;
                    return false;
            }
        }
    }
}
