using System;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>カットインの設定の1行 = キャラ（スキル）1人分の絵・技名・声。</summary>
    [Serializable]
    public sealed class SkillCutInEntry
    {
        [Tooltip("一覧で見分けるための名前（ゲームには出ない）。")]
        [SkillLabel("名前（メモ）")]
        public string label = "";

        [Tooltip("OFFなら、このキャラはカットインを出さない（スキルはすぐ始まる）。")]
        [SkillLabel("出す")]
        public bool enabled = true;

        [Tooltip("このスキルを撃った（溜まった）ときにこの行を使う。")]
        [SkillLabel("スキル")]
        public CharacterSkill skill;

        [Tooltip("カットインに出す技名。空ならスキルの「表示名」。")]
        [SkillLabel("技名")]
        public string skillName = "";

        [Tooltip("カットインの絵（背景込みの1枚絵）。差し替えは Assets/RedSkill/Art/CutIn の PNG を上書きするだけでよい。")]
        [SkillLabel("絵")]
        public Sprite picture;

        [Tooltip("途中から切り替える絵（叫ぶ顔など、表情違い）。空なら切り替えない。同じ大きさ・同じ構図で描くこと。")]
        [SkillLabel("途中から切り替える絵（表情違い）")]
        public Sprite laterPicture;

        [Tooltip("絵の中の顔の位置。左上が (0, 0)、右下が (1, 1)。窓の中でこの点が決まった場所に来るように絵を置く。")]
        [SkillLabel("顔の位置（左上0・右下1）")]
        public Vector2 face = new Vector2(0.5f, 0.35f);

        [Tooltip("顔の大きさの補正。1 = 共通の大きさ。大きくすると顔が大きく（寄って）映る。")]
        [SkillLabel("顔の大きさ（倍）")]
        [Range(0.5f, 2f)]
        public float faceScale = 1f;

        [Tooltip("窓のふちの稲妻と技名のふちの色。")]
        [SkillLabel("色")]
        public Color color = Color.white;

        [Tooltip("カットインが開くときに鳴らすキャラの声（D-Drive の SE。VFX は使わない）。")]
        [SkillLabel("声")]
        public FortressEffect voice = new FortressEffect();

        /// <summary>画面に出す技名。</summary>
        public string SkillNameOrDefault => !string.IsNullOrWhiteSpace(skillName) ? skillName : skill != null ? skill.DisplayName : "";
    }
}
