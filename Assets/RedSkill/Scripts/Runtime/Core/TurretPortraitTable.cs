using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 砲台に出すキャラの絵（SD）の一覧（2026-10-23〜）。1行 = キャラ1人分の絵と出し方（<see cref="SkillTurretLook"/>）。
    /// スキルを配る係（<see cref="PlayerSkillInstaller.turretPortraits"/>）に入れておくと、各砲台の <see cref="SkillTurretPortrait"/> が
    /// 「キャラ選択で選んだキャラ（通らなかったときは P1〜P4 の席）」に合う行を探して、その絵を出す。
    /// スキルとは別に持つので、自分のスキルがまだ無いキャラ（全員に配るスキルを借りている席）にも絵を出せる。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/砲台のキャラの絵の一覧", fileName = "TurretPortraits", order = 30)]
    public sealed class TurretPortraitTable : ScriptableObject
    {
        [Tooltip("キャラ1人ずつの絵と出し方。上の行から順に調べ、最初に合った行を使う。")]
        [SkillLabel("キャラの絵")]
        public SkillTurretLook[] entries = new SkillTurretLook[0];

        /// <summary>この人（playerIndex 0〜3）の砲台に出す行。無ければ null。</summary>
        public SkillTurretLook Find(int playerIndex, bool hasSelection, string selectedCharacterId)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.normalSprite != null && entry.ShowsFor(playerIndex, hasSelection, selectedCharacterId))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
