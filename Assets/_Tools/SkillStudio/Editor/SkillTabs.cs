using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MS2026.Skills;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// スキルの設定ファイルの「設定のまとまり」を、レベルデザイン／見た目のどちらに出すかで分ける（<see cref="SkillTabAttribute"/>）。
    /// 付いていないまとまりは見た目に出る。並びは設定ファイルに書いてある順。
    /// </summary>
    public static class SkillTabs
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>共通部分（CharacterSkill）の「溜まり方」の項目（レベルデザインの一番上に出す）。</summary>
        public static readonly string[] ChargeFields =
        {
            nameof(CharacterSkill.chargeBySmashBall),
            nameof(CharacterSkill.chargeByKills),
            nameof(CharacterSkill.killsToCharge),
            nameof(CharacterSkill.countOwnSkillKills),
            nameof(CharacterSkill.ignoreChargeWhileActive)
        };

        /// <summary>共通部分の「普段のレーザーとの関係」の項目。</summary>
        public static readonly string[] LaserFields =
        {
            nameof(CharacterSkill.replacesNormalLaser),
            nameof(CharacterSkill.hideNormalLaserUntilRelease)
        };

        /// <summary>このスキルの型で、指定の欄に出す設定のまとまり（派生クラスで足された項目だけ）。</summary>
        public static IReadOnlyList<FieldInfo> Groups(Type skillType, SkillTab tab)
        {
            if (skillType == null)
            {
                return Array.Empty<FieldInfo>();
            }

            return skillType.GetFields(Flags)
                .Where(f => f.DeclaringType != typeof(CharacterSkill) && f.DeclaringType != typeof(UnityEngine.Object)
                            && f.DeclaringType != typeof(UnityEngine.ScriptableObject))
                .Where(f => !f.IsNotSerialized && TabOf(f) == tab)
                .OrderBy(f => f.MetadataToken)
                .ToList();
        }

        public static SkillTab TabOf(FieldInfo field)
        {
            var attribute = field.GetCustomAttribute<SkillTabAttribute>();
            return attribute != null ? attribute.Tab : SkillTab.Look;
        }

        /// <summary>中身が全部「付け外し・強さ」（<see cref="SkillEffectDial"/>）のまとまりか（スイッチとメーターの表で出す）。</summary>
        public static bool IsDialGroup(FieldInfo group)
        {
            var fields = group.FieldType.GetFields(Flags);
            return fields.Length > 0 && fields.All(f => f.FieldType == typeof(SkillEffectDial));
        }

        /// <summary>「よく触る項目」1つ（<see cref="SkillQuickAttribute"/> の付いた項目）。</summary>
        public readonly struct QuickItem
        {
            public QuickItem(string path, FieldInfo field, string label, int order)
            {
                Path = path;
                Field = field;
                Label = label;
                Order = order;
            }

            /// <summary>SerializedObject の中の場所（例: "area.radius"、共通の項目なら "killsToCharge"）。</summary>
            public string Path { get; }

            public FieldInfo Field { get; }

            /// <summary>並べるときの名前（空なら [SkillLabel] の名前）。</summary>
            public string Label { get; }

            public int Order { get; }
        }

        /// <summary>
        /// 指定の欄の「よく触る項目」。レベルデザインの欄では共通部分（溜まり方）の印も含める。
        /// 並びは印の order 順、同じなら設定ファイルに書いてある順。
        /// </summary>
        public static List<QuickItem> QuickItems(Type skillType, SkillTab tab)
        {
            var result = new List<QuickItem>();
            if (skillType == null)
            {
                return result;
            }

            if (tab == SkillTab.Level)
            {
                foreach (var field in typeof(CharacterSkill).GetFields(Flags))
                {
                    var quick = field.GetCustomAttribute<SkillQuickAttribute>();
                    if (quick != null)
                    {
                        result.Add(new QuickItem(field.Name, field, quick.Label, quick.Order));
                    }
                }
            }

            foreach (var group in Groups(skillType, tab))
            {
                foreach (var field in group.FieldType.GetFields(Flags).OrderBy(f => f.MetadataToken))
                {
                    var quick = field.GetCustomAttribute<SkillQuickAttribute>();
                    if (quick != null)
                    {
                        result.Add(new QuickItem(group.Name + "." + field.Name, field, quick.Label, quick.Order));
                    }
                }
            }

            return result.Select((item, index) => (item, index)).OrderBy(x => x.item.Order).ThenBy(x => x.index).Select(x => x.item).ToList();
        }

        /// <summary>
        /// このまとまりと一緒に見せる「付け外し・強さ」（付け外しのまとまりの中で <see cref="SkillDialForAttribute"/> がこのまとまりを指している物）。
        /// 戻り値は (付け外しのまとまり, 付け外しの項目) の組。
        /// </summary>
        public static List<(FieldInfo dialGroup, FieldInfo dial)> DialsFor(Type skillType, FieldInfo group)
        {
            var result = new List<(FieldInfo, FieldInfo)>();
            foreach (var dialGroup in Groups(skillType, SkillTab.Look).Concat(Groups(skillType, SkillTab.Level)))
            {
                if (!IsDialGroup(dialGroup))
                {
                    continue;
                }

                foreach (var dial in dialGroup.FieldType.GetFields(Flags).OrderBy(f => f.MetadataToken))
                {
                    var link = dial.GetCustomAttribute<SkillDialForAttribute>();
                    if (link != null && link.GroupField == group.Name)
                    {
                        result.Add((dialGroup, dial));
                    }
                }
            }

            return result;
        }

        /// <summary>付け外しのまとまりの中身が全部ほかのまとまりに配られているか（そうなら、まとまりとしては出さない）。</summary>
        public static bool IsFullyDistributed(FieldInfo dialGroup)
        {
            return IsDialGroup(dialGroup) && dialGroup.FieldType.GetFields(Flags).All(f => f.GetCustomAttribute<SkillDialForAttribute>() != null);
        }

        /// <summary>まとまりの中の、インスペクターに出る項目の数（「詳しい設定」の見出しに出す数）。</summary>
        public static int CountFields(Type groupType)
        {
            return groupType.GetFields(Flags).Count(f => !f.IsNotSerialized && f.GetCustomAttribute<UnityEngine.HideInInspector>() == null);
        }

        public static string LabelOf(FieldInfo field)
        {
            var label = field.GetCustomAttribute<SkillLabelAttribute>();
            return label != null ? label.Label : UnityEditor.ObjectNames.NicifyVariableName(field.Name);
        }

        public static string TooltipOf(FieldInfo field)
        {
            var tooltip = field.GetCustomAttribute<UnityEngine.TooltipAttribute>();
            return tooltip?.tooltip;
        }
    }
}
