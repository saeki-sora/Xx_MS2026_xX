using System.Collections.Generic;
using MS2026.Skills;
using UnityEditor;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>
    /// スキル調整スタジオの共有の状態: 色ごとのスキルの設定ファイル、保存した設定（比べる用）、ページごとの表示中の欄。
    /// どのスキルがどの色かは、キャラとスキルの対応表（CharacterSkillTable）の characterId（alpha 赤・beta 青・gamma 緑・delta 黄）で決める。
    /// 対応表に無ければ、スキルID の頭（red_ / blue_ …）で探す。
    /// </summary>
    public sealed class SkillStudioContext
    {
        private readonly Dictionary<SkillColor, CharacterSkill> _skills = new Dictionary<SkillColor, CharacterSkill>();
        private readonly Dictionary<SkillColor, SkillTab> _tabs = new Dictionary<SkillColor, SkillTab>();
        private double _nextScan;

        public SkillStudioContext()
        {
            Snapshots = new SkillSnapshotService();
            Rescan();
        }

        public SkillSnapshotService Snapshots { get; }

        /// <summary>開いている折りたたみ（「詳しい設定」・各まとまり）。画面を作り直しても開いたままにするため、ここで覚える。</summary>
        public HashSet<string> OpenSections { get; } = new HashSet<string>();

        public bool IsOpen(string key) => OpenSections.Contains(key);

        public void SetOpen(string key, bool open)
        {
            if (open)
            {
                OpenSections.Add(key);
            }
            else
            {
                OpenSections.Remove(key);
            }
        }

        /// <summary>撃ち終わったらすぐもう一度撃つ（試し撃ち）色。Play を止めると消える。</summary>
        public HashSet<SkillColor> AutoRefire { get; } = new HashSet<SkillColor>();

        public CharacterSkill SkillOf(SkillColor color) => _skills.TryGetValue(color, out var skill) ? skill : null;

        public SkillTab TabOf(SkillColor color) => _tabs.TryGetValue(color, out var tab) ? tab : SkillTab.Look;

        public void SetTab(SkillColor color, SkillTab tab) => _tabs[color] = tab;

        /// <summary>定期更新（数秒に1回だけ、設定ファイルを探し直す）。</summary>
        public void Tick()
        {
            if (EditorApplication.timeSinceStartup >= _nextScan)
            {
                Rescan();
            }

            TickAutoRefire();
        }

        public void Rescan()
        {
            _nextScan = EditorApplication.timeSinceStartup + 3.0;
            _skills.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterSkillTable"))
            {
                var table = AssetDatabase.LoadAssetAtPath<CharacterSkillTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (table == null || table.entries == null)
                {
                    continue;
                }

                foreach (var entry in table.entries)
                {
                    var id = !string.IsNullOrEmpty(entry.characterId) ? entry.characterId : entry.character != null ? entry.character.id : null;
                    foreach (var color in SkillColors.All)
                    {
                        if (entry.skill != null && id == SkillColors.CharacterId(color) && !_skills.ContainsKey(color))
                        {
                            _skills[color] = entry.skill;
                        }
                    }
                }
            }

            if (_skills.Count == SkillColors.All.Length)
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:CharacterSkill"))
            {
                var skill = AssetDatabase.LoadAssetAtPath<CharacterSkill>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill == null || string.IsNullOrEmpty(skill.skillId))
                {
                    continue;
                }

                foreach (var color in SkillColors.All)
                {
                    if (!_skills.ContainsKey(color) && skill.skillId.StartsWith(SkillColors.SkillIdPrefix(color)))
                    {
                        _skills[color] = skill;
                    }
                }
            }
        }

        /// <summary>今のシーンで、このスキルを持っている砲台のスキル欄（Play 中のみ）。</summary>
        public static List<PlayerSkillSlot> SlotsUsing(CharacterSkill skill)
        {
            var result = new List<PlayerSkillSlot>();
            if (!Application.isPlaying || skill == null)
            {
                return result;
            }

            foreach (var slot in PlayerSkillSlot.All)
            {
                if (slot != null && slot.Skill == skill)
                {
                    result.Add(slot);
                }
            }

            result.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            return result;
        }

        /// <summary>試し撃ち: 溜まっていなければ溜めてから、今すぐ撃つ（撃っている最中なら何もしない）。</summary>
        public static void FireNow(PlayerSkillSlot slot)
        {
            if (slot == null || slot.State == PlayerSkillState.Active || slot.State == PlayerSkillState.NoSkill)
            {
                return;
            }

            if (slot.State == PlayerSkillState.Empty)
            {
                slot.GrantCharge();
            }

            slot.ActivateNow();
        }

        private void TickAutoRefire()
        {
            if (!Application.isPlaying)
            {
                AutoRefire.Clear();
                return;
            }

            foreach (var color in AutoRefire)
            {
                foreach (var slot in SlotsUsing(SkillOf(color)))
                {
                    FireNow(slot);
                }
            }
        }
    }
}
