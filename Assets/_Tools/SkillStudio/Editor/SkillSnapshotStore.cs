using System;
using System.Collections.Generic;
using MS2026.Skills;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// スキル調整スタジオで保存した設定の一覧（比べる用）。プロジェクトに保存されるので、チームで共有できる。
    /// 1件 = 1つのスキルの設定ファイルの、その時点の値すべて（JSON）。A・B の印を付けた2件は、ワンクリックで切り替えて比べられる。
    /// </summary>
    public sealed class SkillSnapshotStore : ScriptableObject
    {
        public const string AssetPath = "Assets/_Tools/SkillStudio/Data/SkillStudioSnapshots.asset";

        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("どのスキルの設定か。")]
            public CharacterSkill skill;

            [Tooltip("名前（メモ）。")]
            public string name = "";

            [Tooltip("保存した日時。")]
            public string savedAt = "";

            [Tooltip("比べる印（\"A\" か \"B\"、空なら印なし）。")]
            public string mark = "";

            [Tooltip("設定ファイルの値（JSON）。")]
            [TextArea(1, 3)]
            public string json = "";
        }
    }
}
