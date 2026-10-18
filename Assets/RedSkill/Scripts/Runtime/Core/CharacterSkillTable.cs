using System;
using MS2026.CharacterSelect;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 「どのキャラがどのスキルを持つか」の対応表。キャラ選択で選ばれたキャラ（<see cref="CharacterDefinition.id"/>）から
    /// スキルを引くために <see cref="PlayerSkillInstaller"/> が使う。キャラを増やしたら行を足すだけでよい。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/キャラとスキルの対応表", fileName = "CharacterSkillTable")]
    public sealed class CharacterSkillTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("キャラのファイル（CharacterDefinition）。id より優先して使う。")]
            [SkillLabel("キャラ")]
            public CharacterDefinition character;

            [Tooltip("キャラの id（CharacterDefinition の id）。キャラのファイルを入れない場合はこちらで指定する。")]
            [SkillLabel("キャラのid")]
            public string characterId;

            [Tooltip("そのキャラが持つスキル。")]
            [SkillLabel("スキル")]
            public CharacterSkill skill;
        }

        [Tooltip("キャラ → スキルの対応。上から順に探す。")]
        [SkillLabel("対応表")]
        public Entry[] entries = Array.Empty<Entry>();

        /// <summary>そのキャラのスキル（見つからなければ null）。</summary>
        public CharacterSkill Find(CharacterDefinition character)
        {
            if (character == null || entries == null)
            {
                return null;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.character == character)
                {
                    return entry.skill;
                }

                if (entry.character == null && !string.IsNullOrEmpty(entry.characterId) && entry.characterId == character.id)
                {
                    return entry.skill;
                }
            }

            return null;
        }
    }
}
