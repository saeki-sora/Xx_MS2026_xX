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
