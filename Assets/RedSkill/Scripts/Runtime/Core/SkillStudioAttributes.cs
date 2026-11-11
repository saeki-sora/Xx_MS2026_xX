using System;

namespace MS2026.Skills
{
    /// <summary>
    /// 「よく触る項目」の印。スキル調整スタジオは、ページを開いた瞬間にはこの印の付いた項目だけを出し、
    /// ほかの項目は「詳しい設定」の奥にしまう（使いやすさの資料 Docs/Reference/Tool_UI_Analysis.md の T2）。
    /// 設定のまとまりの中の項目（例: area.radius）か、CharacterSkill の共通の項目に付ける。1つの欄で 8 個くらいまでにする。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SkillQuickAttribute : Attribute
    {
        /// <param name="order">並び順（小さいほど上）。</param>
        /// <param name="label">「よく触る項目」に並べたときの名前（まとまりの外に出すと意味が分かりにくい名前を、言い直すとき用）。空なら [SkillLabel] の名前。</param>
        public SkillQuickAttribute(int order = 0, string label = null)
        {
            Order = order;
            Label = label;
        }

        public int Order { get; }

        public string Label { get; }
    }

    /// <summary>
    /// 演出の付け外し（<see cref="SkillEffectDial"/>）を、どの設定のまとまりと一緒に見せるか。
    /// スキル調整スタジオは、付け外しを1枚の表にまとめず、関係するまとまり（例: 稲妻の欄）の一番上に出す（同じ目的の値を1か所に集めるため。T1）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SkillDialForAttribute : Attribute
    {
        /// <param name="groupField">一緒に見せる設定のまとまりのフィールド名（例: "lightning"）。</param>
        public SkillDialForAttribute(string groupField)
        {
            GroupField = groupField;
        }

        public string GroupField { get; }
    }
}
