using System;

namespace MS2026.Skills
{
    /// <summary>スキル調整スタジオで、設定のまとまりをどちらの欄に出すか。</summary>
    public enum SkillTab
    {
        /// <summary>レベルデザイン（強さ・時間・範囲・溜まり方など、ゲームの手ごたえに関わる数字）。</summary>
        Level,

        /// <summary>見た目（色・演出・揺れ・効果音）。</summary>
        Look,

        /// <summary>スタジオには出さない（シェーダーの指定など、ふだん触らない物）。</summary>
        Hidden
    }

    /// <summary>
    /// スキルの設定ファイルの「設定のまとまり」に付けて、スキル調整スタジオのどちらの欄に出すかを決める。
    /// 付いていないまとまりは「見た目」に出る。新しいスキルを作ったときも、まとまりにこれを付けるだけでスタジオに並ぶ。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SkillTabAttribute : Attribute
    {
        public SkillTabAttribute(SkillTab tab)
        {
            Tab = tab;
        }

        public SkillTab Tab { get; }
    }
}
