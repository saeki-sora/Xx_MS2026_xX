using MS2026.CharacterSelect;
using MS2026.Fortress;
using MS2026.Fortress.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.Skills
{
    /// <summary>
    /// シーンに1つ置くだけで、シーン内の全ての砲台（<see cref="LaserTurret"/>）に <see cref="PlayerSkillSlot"/> を付けてスキルを配る係。
    /// 別のシーンでスキルを使いたいときは、この部品が付いたプレハブ（Assets/RedSkill/Prefabs）をシーンに置くだけでよい。
    /// どのスキルを配るかの優先順位: キャラ選択の結果＋対応表 → プレイヤーごとの指定 → 全員共通のスキル。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/スキル/スキルを砲台に配る")]
    public sealed class PlayerSkillInstaller : MonoBehaviour
    {
        public const int PlayerCount = 4;

        [Header("配るスキル")]
        [Tooltip("全員に配るスキル（下の「プレイヤーごとのスキル」が空の席に使う）。")]
        [SkillLabel("全員に配るスキル")]
        public CharacterSkill defaultSkill;

        [Tooltip("P1〜P4 ごとのスキル。空ならその席は「全員に配るスキル」になる。")]
        [SkillLabel("プレイヤーごとのスキル（P1〜P4）")]
        public CharacterSkill[] perPlayerSkills = new CharacterSkill[PlayerCount];

        [Header("キャラとの紐づけ（キャラ選択の画面を通ってきたときだけ使う）")]
        [Tooltip("ONなら、キャラ選択で選ばれたキャラのスキルを下の対応表から探して配る（見つからなければ上の指定を使う）。")]
        [SkillLabel("キャラ選択の結果を使う")]
        public bool useCharacterSelection = true;

        [Tooltip("キャラ → スキルの対応表。空ならキャラでは決めない。")]
        [SkillLabel("キャラとスキルの対応表")]
        public CharacterSkillTable characterTable;

        [Header("テスト用")]
        [Tooltip("ONなら、ゲーム開始時から全員スキルを使える状態にする。")]
        [SkillLabel("最初から全員使える（テスト）")]
        public bool startReady;

        [Tooltip("ONなら、エディタ（とデバッグ表示を出す起動）でだけ、数字キー 1〜4 で P1〜P4 のスキルを使える状態にする。")]
        [SkillLabel("数字キー1〜4で溜める（テスト）")]
        public bool enableTestKeys = true;

        private void Awake()
        {
            InstallAll();
        }

        /// <summary>シーン内の全ての砲台にスキル欄を付けてスキルを配る（もう付いていれば配り直すだけ）。</summary>
        public void InstallAll()
        {
            var turrets = FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < turrets.Length; i++)
            {
                Install(turrets[i]);
            }
        }

        /// <summary>1台の砲台にスキル欄を付けてスキルを配る。</summary>
        public PlayerSkillSlot Install(LaserTurret turret)
        {
            if (turret == null)
            {
                return null;
            }

            var slot = turret.GetComponent<PlayerSkillSlot>();
            if (slot == null)
            {
                slot = turret.gameObject.AddComponent<PlayerSkillSlot>();
            }

            slot.SetSkill(ResolveSkill(turret.playerIndex));
            if (startReady)
            {
                slot.startReady = true;
            }

            return slot;
        }

        /// <summary>その席に配るスキルを決める。</summary>
        public CharacterSkill ResolveSkill(int playerIndex)
        {
            if (useCharacterSelection && characterTable != null && CharacterSelectionResult.HasResult)
            {
                var fromCharacter = characterTable.Find(CharacterSelectionResult.Get(playerIndex));
                if (fromCharacter != null)
                {
                    return fromCharacter;
                }
            }

            if (perPlayerSkills != null && playerIndex >= 0 && playerIndex < perPlayerSkills.Length && perPlayerSkills[playerIndex] != null)
            {
                return perPlayerSkills[playerIndex];
            }

            return defaultSkill;
        }

        private void Update()
        {
            if (!enableTestKeys || !DebugUi.Visible)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (var i = 0; i < PlayerCount; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    var slot = PlayerSkillSlot.Find(i);
                    if (slot != null)
                    {
                        slot.GrantCharge();
                    }
                }
            }
        }

        private void OnValidate()
        {
            if (perPlayerSkills == null || perPlayerSkills.Length != PlayerCount)
            {
                var resized = new CharacterSkill[PlayerCount];
                if (perPlayerSkills != null)
                {
                    for (var i = 0; i < Mathf.Min(PlayerCount, perPlayerSkills.Length); i++)
                    {
                        resized[i] = perPlayerSkills[i];
                    }
                }

                perPlayerSkills = resized;
            }
        }
    }
}
