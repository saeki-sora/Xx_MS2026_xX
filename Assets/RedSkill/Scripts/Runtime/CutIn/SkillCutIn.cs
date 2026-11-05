using System;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// スキル欄（<see cref="PlayerSkillSlot"/>）とカットインの表示（<see cref="SkillCutInDirector"/>）をつなぐ窓口。
    /// スキル欄は「カットインを出す合図」を送り、「スキル本体を何秒待たせるか」を聞くだけで、見た目のことは知らない。
    /// いつ出すか（撃ったとき／溜まったとき）は設定ファイルの <see cref="SkillCutInSettings.timing"/> 1か所で切り替える。
    /// ネット対戦: スキル欄は各PCで動くので、合図も各PCで出る（通信は増やさない）。
    /// </summary>
    public static class SkillCutIn
    {
        /// <summary>今のシーンで使う設定（スキルを配る係が入れる）。null ならカットインなし。</summary>
        public static SkillCutInSettings Settings { get; set; }

        /// <summary>カットインを出す合図（どの砲台のスキル欄か）。</summary>
        public static event Action<PlayerSkillSlot> Cue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Settings = null;
            Cue = null;
        }

        /// <summary>このスキルにカットインがあるか（設定が「出さない」のとき・行が無いときは false）。</summary>
        public static bool Has(CharacterSkill skill)
        {
            return Settings != null && Settings.timing != SkillCutInTiming.Off && Settings.Find(skill) != null;
        }

        /// <summary>撃ったときに、スキル本体を何秒待たせるか（待たせないなら 0）。</summary>
        public static float StartDelayFor(CharacterSkill skill)
        {
            return Settings != null && Settings.timing == SkillCutInTiming.OnActivate && Has(skill) ? Settings.skillStartDelay : 0f;
        }

        /// <summary>待っている間、砲台を止める（回転・熱）か。</summary>
        public static bool FreezeTurretWhileWaiting => Settings == null || Settings.freezeTurretWhileWaiting;

        /// <summary>スキル欄から: 撃った（スキルが始まる）。「撃ったとき」の設定ならカットインを出す。</summary>
        internal static void NotifyActivated(PlayerSkillSlot slot)
        {
            if (Settings != null && Settings.timing == SkillCutInTiming.OnActivate && Has(slot.Skill))
            {
                Cue?.Invoke(slot);
            }
        }

        /// <summary>スキル欄から: 溜まった（使える状態になった）。「溜まったとき」の設定ならカットインを出す。</summary>
        internal static void NotifyReady(PlayerSkillSlot slot)
        {
            if (Settings != null && Settings.timing == SkillCutInTiming.OnReady && Has(slot.Skill))
            {
                Cue?.Invoke(slot);
            }
        }
    }
}
