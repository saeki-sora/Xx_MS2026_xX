using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>スキルの色（＝キャラ）。左のナビの1行ずつ。</summary>
    public enum SkillColor
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    /// <summary>色ごとの表示名・キャラの id・席（キャラ選択が無いとき）・スキルIDの頭。</summary>
    public static class SkillColors
    {
        public static readonly SkillColor[] All = { SkillColor.Red, SkillColor.Blue, SkillColor.Green, SkillColor.Yellow };

        public static string Label(SkillColor color) => color switch
        {
            SkillColor.Red => "赤",
            SkillColor.Blue => "青",
            SkillColor.Green => "緑",
            _ => "黄"
        };

        /// <summary>キャラとスキルの対応表（CharacterSkillTable）の characterId。</summary>
        public static string CharacterId(SkillColor color) => color switch
        {
            SkillColor.Red => "alpha",
            SkillColor.Blue => "beta",
            SkillColor.Green => "gamma",
            _ => "delta"
        };

        /// <summary>スキルID の頭（対応表に無いときの探し方）。</summary>
        public static string SkillIdPrefix(SkillColor color) => color switch
        {
            SkillColor.Red => "red",
            SkillColor.Blue => "blue",
            SkillColor.Green => "green",
            _ => "yellow"
        };

        /// <summary>キャラ選択が無いときにこの色を使う席（0〜3、P1 赤・P2 青・P3 緑・P4 黄）。</summary>
        public static int DefaultSeat(SkillColor color) => (int)color;

        public static Color Tint(SkillColor color) => color switch
        {
            SkillColor.Red => new Color(1f, 0.33f, 0.36f),
            SkillColor.Blue => new Color(0.35f, 0.62f, 1f),
            SkillColor.Green => new Color(0.35f, 0.85f, 0.5f),
            _ => new Color(1f, 0.82f, 0.25f)
        };
    }
}
