using System;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>選べるキャラクターの一覧（並び順 = 画面に左から並ぶ順）。</summary>
    [CreateAssetMenu(menuName = "MS2026/キャラ選択/キャラの一覧", fileName = "CharacterRoster", order = 1)]
    public sealed class CharacterRoster : ScriptableObject
    {
        [Tooltip("選べるキャラクター（上から順に、画面の左から並ぶ）。空の欄は作らないこと。")]
        public CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();

        public int Count => characters != null ? characters.Length : 0;

        /// <summary>番号のキャラ（範囲外・空の欄は null）。</summary>
        public CharacterDefinition Get(int index) => index >= 0 && index < Count ? characters[index] : null;

        /// <summary>id からキャラの番号を探す（無ければ -1）。</summary>
        public int IndexOf(string id)
        {
            for (var i = 0; i < Count; i++)
            {
                if (characters[i] != null && characters[i].id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
