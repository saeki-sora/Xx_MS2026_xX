using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MS2026.Skills;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// スキルの設定を名前で探す（使いやすさの資料の T5: 探さずに見つかる）。レベルデザイン・見た目の両方から探す。
    /// 項目名・説明・見出し・プログラムの名前で当たる。まとまりの名前で当たれば、そのまとまりの中身を全部出す。
    /// ひらがなとカタカナは区別しない。空白で区切ると、全部の言葉を含む物だけ。
    /// </summary>
    public static class SkillSearch
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;

        public readonly struct Hit
        {
            public Hit(string place, string path, FieldInfo field)
            {
                Place = place;
                Path = path;
                Field = field;
            }

            /// <summary>どこにあるか（例: 「見た目 › ビリビリ（稲妻）」）。</summary>
            public string Place { get; }

            /// <summary>SerializedObject の中の場所。</summary>
            public string Path { get; }

            public FieldInfo Field { get; }
        }

        public static List<Hit> Find(Type skillType, string query)
        {
            var result = new List<Hit>();
            var words = Words(query);
            if (skillType == null || words.Length == 0)
            {
                return result;
            }

            foreach (var tab in new[] { SkillTab.Level, SkillTab.Look })
            {
                var tabName = tab == SkillTab.Level ? "レベルデザイン" : "見た目";
                if (tab == SkillTab.Level)
                {
                    AddBase(result, words, tabName + " › 溜まり方", "溜まり方 ゲージ", SkillTabs.ChargeFields);
                    AddBase(result, words, tabName + " › 普段のレーザーとの関係", "普段のレーザーとの関係", SkillTabs.LaserFields);
                }

                var groups = SkillTabs.Groups(skillType, tab);
                foreach (var group in groups)
                {
                    var groupText = SkillTabs.LabelOf(group) + " " + SkillTabs.TooltipOf(group);
                    // 付け外しのまとまりは中身がほかのまとまりに配られているので、まとまりの名前では当てない（1つずつ当てる）。
                    var groupHit = !SkillTabs.IsDialGroup(group) && Matches(groupText, words);
                    foreach (var field in group.FieldType.GetFields(Flags).OrderBy(f => f.MetadataToken))
                    {
                        if (field.IsNotSerialized || field.GetCustomAttribute<HideInInspector>() != null)
                        {
                            continue;
                        }

                        var place = tabName + " › " + SkillTabs.LabelOf(group);
                        var linkText = "";
                        var link = field.GetCustomAttribute<SkillDialForAttribute>();
                        if (link != null)
                        {
                            // 付け外しは、画面で出している場所（関係するまとまり）の名前で見せる・当てる。
                            var target = groups.FirstOrDefault(g => g.Name == link.GroupField)
                                         ?? SkillTabs.Groups(skillType, tab == SkillTab.Level ? SkillTab.Look : SkillTab.Level)
                                             .FirstOrDefault(g => g.Name == link.GroupField);
                            if (target != null)
                            {
                                place = (SkillTabs.TabOf(target) == SkillTab.Level ? "レベルデザイン" : "見た目") + " › " + SkillTabs.LabelOf(target);
                                linkText = SkillTabs.LabelOf(target);
                            }
                        }

                        if (groupHit || Matches(FieldText(field) + " " + linkText, words))
                        {
                            result.Add(new Hit(place, group.Name + "." + field.Name, field));
                        }
                    }
                }
            }

            // 同じ場所の項目は続けて並べる（場所の並びは最初に出てきた順）。
            return result.Select((hit, index) => (hit, index))
                .GroupBy(x => x.hit.Place)
                .OrderBy(g => g.Min(x => x.index))
                .SelectMany(g => g.Select(x => x.hit))
                .ToList();
        }

        private static void AddBase(List<Hit> result, string[] words, string place, string placeText, IEnumerable<string> names)
        {
            var placeHit = Matches(placeText, words);
            foreach (var name in names)
            {
                var field = typeof(CharacterSkill).GetField(name, Flags);
                if (field != null && (placeHit || Matches(FieldText(field), words)))
                {
                    result.Add(new Hit(place, name, field));
                }
            }
        }

        private static string FieldText(FieldInfo field)
        {
            var text = new StringBuilder();
            text.Append(SkillTabs.LabelOf(field)).Append(' ');
            text.Append(SkillTabs.TooltipOf(field)).Append(' ');
            text.Append(field.Name).Append(' ');
            var quick = field.GetCustomAttribute<SkillQuickAttribute>();
            if (quick != null)
            {
                text.Append(quick.Label).Append(' ');
            }

            foreach (var header in field.GetCustomAttributes<HeaderAttribute>())
            {
                text.Append(header.header).Append(' ');
            }

            return text.ToString();
        }

        private static bool Matches(string text, string[] words)
        {
            var normalized = Normalize(text);
            return words.All(w => normalized.Contains(w));
        }

        private static string[] Words(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<string>();
            }

            return Normalize(query).Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>小文字にし、カタカナをひらがなに、全角英数を半角にそろえる。</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var ch = c;
                if (ch >= 'ァ' && ch <= 'ヶ')
                {
                    ch = (char)(ch - 0x60);
                }
                else if (ch >= '！' && ch <= '～')
                {
                    ch = (char)(ch - 0xFEE0);
                }

                builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }
    }
}
