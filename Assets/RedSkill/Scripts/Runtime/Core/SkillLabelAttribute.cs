using System;

namespace MS2026.Skills
{
    /// <summary>
    /// インスペクターに出す日本語の項目名。スキル関係のインスペクター（MS2026.Skills.EditorTools）だけが読む。
    /// PropertyAttribute ではないので、[Range] などの他の属性の表示を邪魔しない。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SkillLabelAttribute : Attribute
    {
        public SkillLabelAttribute(string label)
        {
            Label = label;
        }

        public string Label { get; }
    }
}
