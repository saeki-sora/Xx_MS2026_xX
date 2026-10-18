using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// キャラクターのスキル1つ分の設定ファイル（ScriptableObject）の共通部分。
    /// スキルの中身（何が起きるか）は派生クラス（例: <see cref="Red.RedLaserSkill"/>）が決め、
    /// 「いつ使えるようになるか・いつ撃つか」は砲台に付いた <see cref="PlayerSkillSlot"/> が共通で受け持つ。
    /// キャラに紐づけるときは、このファイルを <see cref="CharacterSkillTable"/> でキャラの id と結びつけるだけでよい。
    /// </summary>
    public abstract class CharacterSkill : ScriptableObject
    {
        [Header("スキルの情報")]
        [Tooltip("プログラムから探すときの名前（キャラとの紐づけ表やログで使う）。あとから変えないこと。")]
        [SkillLabel("スキルID")]
        public string skillId = "skill";

        [Tooltip("画面に出すスキル名（空ならファイル名）。")]
        [SkillLabel("表示名")]
        public string displayName = "";

        [Tooltip("スキルの説明（メモ・UI用）。")]
        [SkillLabel("説明")]
        [TextArea(2, 5)]
        public string description = "";

        [Tooltip("スキルのテーマ色（UIなどで使う想定。エフェクトの色はスキルごとの欄で決める）。")]
        [SkillLabel("テーマ色")]
        public Color themeColor = Color.red;

        [Header("使えるようになる条件")]
        [Tooltip("ONなら、この砲台のプレイヤーがスマッシュボールを割ったときに使えるようになる。")]
        [SkillLabel("スマッシュボールを割ると溜まる")]
        public bool chargeBySmashBall = true;

        [Tooltip("ONなら、スキルを撃っている最中に割ったスマッシュボールでは次のスキルが溜まらない（連発を防ぐ）。")]
        [SkillLabel("スキル中は溜まらない")]
        public bool ignoreChargeWhileActive = true;

        [Header("通常のレーザーとの関係")]
        [Tooltip("ONなら、スキルを撃っている間は普段のレーザーを消す（スキルのレーザーに置き換える）。")]
        [SkillLabel("普段のレーザーと入れ替える")]
        public bool replacesNormalLaser = true;

        [Tooltip("ONなら、スキルが終わっても握り続けている間は普段のレーザーを出さない（一度離すと元に戻る）。")]
        [SkillLabel("終わっても離すまで普段のレーザーを出さない")]
        public bool hideNormalLaserUntilRelease = true;

        /// <summary>画面に出す名前（空ならファイル名）。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>
        /// このスキルを実際に動かす係（<see cref="CharacterSkillRunner"/>）を砲台に付けて返す。
        /// <see cref="PlayerSkillSlot"/> がスキルを設定されたときに1回だけ呼ぶ。
        /// </summary>
        public abstract CharacterSkillRunner CreateRunner(PlayerSkillSlot slot);
    }
}
