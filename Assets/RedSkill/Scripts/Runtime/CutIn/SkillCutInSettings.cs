using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>カットインをいつ出すか。</summary>
    public enum SkillCutInTiming
    {
        /// <summary>カットインを出さない。</summary>
        [InspectorName("出さない")]
        Off,

        /// <summary>スキルを撃った（握った）ときに出す。スキル本体はカットインの後に始まる。</summary>
        [InspectorName("撃ったとき（スキルはカットインの後）")]
        OnActivate,

        /// <summary>スキルが溜まった（使える状態になった）ときに出す。撃つときは待たずにすぐ始まる。</summary>
        [InspectorName("溜まったとき（撃つときは待たない）")]
        OnReady
    }

    /// <summary>
    /// スキルのカットイン（2026-11-02〜）の設定ファイル。1行 = キャラ（スキル）1人分の絵・技名・声（<see cref="SkillCutInEntry"/>）と、全員共通の動き・形・音。
    /// スキルを配る係（<see cref="PlayerSkillInstaller.cutIn"/>）に入れておくと使われる。
    /// 自分が撃ったときは画面の左に大きく、ほかの人が撃ったときはその人の HUD の欄のそばに小さく出る。
    /// 絵を差し替えるときは、Assets/RedSkill/Art/CutIn の PNG を同じ名前で上書きするだけでよい。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/カットインの設定", fileName = "SkillCutIn", order = 40)]
    public sealed class SkillCutInSettings : ScriptableObject
    {
        [Header("いつ出すか")]
        [Tooltip("撃ったとき = 握った瞬間にカットインを出し、スキル本体はその後で始まる。溜まったとき = 使える状態になった瞬間に出し、撃つときは待たない。")]
        [SkillLabel("カットインを出すとき")]
        public SkillCutInTiming timing = SkillCutInTiming.OnActivate;

        [Tooltip("「撃ったとき」のとき、握ってからスキル本体が始まるまでの秒数。カットインが閉じ始めるころ（開く＋見せる時間）に合わせてある。")]
        [SkillLabel("スキルを待たせる時間（秒）")]
        [Min(0f)]
        public float skillStartDelay = 1.37f;

        [Tooltip("ONなら、スキルを待たせている間（カットイン中）は撃った人の砲台が回らず、握っていても熱が上がらない。")]
        [SkillLabel("待っている間は砲台を止める（回転・熱）")]
        public bool freezeTurretWhileWaiting = true;

        [Header("キャラごとの絵・技名・声")]
        [Tooltip("1行 = キャラ（スキル）1人分。上の行から順に調べ、撃ったスキルと「スキル」が同じ最初の行を使う。")]
        [SkillLabel("キャラ")]
        public SkillCutInEntry[] entries = new SkillCutInEntry[0];

        [Header("全員共通")]
        [Tooltip("開く・見せる・閉じる時間と、絵の寄り方。")]
        [SkillLabel("動き")]
        public SkillCutInMotion motion = new SkillCutInMotion();

        [Tooltip("自分が撃ったときに画面の左に出る大きい窓。")]
        [SkillLabel("大きい窓（自分）")]
        public SkillCutInBigWindow big = new SkillCutInBigWindow();

        [Tooltip("ほかの人が撃ったときに、その人の HUD の欄のそばに出る小さい窓。")]
        [SkillLabel("小さい窓（ほかの人）")]
        public SkillCutInSmallWindow small = new SkillCutInSmallWindow();

        [Tooltip("窓のふちの稲妻と、消えるときの横一本の稲妻。")]
        [SkillLabel("稲妻のふち")]
        public SkillCutInEdge edge = new SkillCutInEdge();

        [Tooltip("技名の文字。")]
        [SkillLabel("技名の文字")]
        public SkillCutInText text = new SkillCutInText();

        [Tooltip("開く・閉じるときの効果音（D-Drive）と、小さい窓で鳴らすかどうか。キャラの声は各キャラの行で決める。")]
        [SkillLabel("音")]
        public SkillCutInSounds sounds = new SkillCutInSounds();

        [Tooltip("ほかの画面との重なり順。HUD（100）より上、メニュー（200）より下。")]
        [SkillLabel("重なり順")]
        public int sortingOrder = 150;

        /// <summary>このスキルのカットインの行。無い・OFF・絵が無いときは null。</summary>
        public SkillCutInEntry Find(CharacterSkill skill)
        {
            if (skill == null || entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry != null && entry.enabled && entry.skill == skill && entry.picture != null)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
