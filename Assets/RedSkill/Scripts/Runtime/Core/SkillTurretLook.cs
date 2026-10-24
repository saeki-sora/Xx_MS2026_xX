using System;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 砲台に出すキャラ1人分の絵（SD）と出し方（2026-10-22〜）。ふだんの絵と、スキル発動中だけの絵を持つ。
    /// <see cref="TurretPortraitTable"/>（砲台のキャラの絵の一覧）の1行。合う砲台では元の砲台の見た目の代わりにこの絵が出て、
    /// スキルを撃っている間だけ発動中の絵に切り替わる（<see cref="SkillTurretPortrait"/> が行う）。
    /// （2026-10-22 はスキルの設定ファイルの中にあったが、自分のスキルがまだ無いキャラにも出すため 2026-10-23 に一覧へ移した。）
    /// </summary>
    [Serializable]
    public sealed class SkillTurretLook
    {
        /// <summary>P1〜P4 の組み合わせ（Inspector では複数選べるメニューになる）。</summary>
        [Flags]
        public enum Players
        {
            None = 0,
            P1 = 1 << 0,
            P2 = 1 << 1,
            P3 = 1 << 2,
            P4 = 1 << 3,
        }

        [Tooltip("メモ用の名前（一覧の見出しに出る。画面には出ない）。")]
        [SkillLabel("名前（メモ）")]
        public string label = "";

        [Tooltip("ふだん砲台に出すキャラの絵。空ならこの行は使わない（砲台は元の見た目のまま）。")]
        [SkillLabel("ふだんの絵")]
        public Sprite normalSprite;

        [Tooltip("スキルを撃っている間だけ出す絵。空ならずっとふだんの絵（スキル発動時の絵がまだ無いキャラ）。2枚は同じ大きさ・同じ位置で描いた絵にすると、切り替えでずれない。")]
        [SkillLabel("スキル発動中の絵")]
        public Sprite activeSprite;

        [Header("だれの砲台に出すか")]
        [Tooltip("キャラ選択を通ったときは、このキャラ（キャラの設定の id。赤 alpha・青 beta・緑 gamma・黄 delta）を選んだ人の砲台に出す。空なら全員。")]
        [SkillLabel("このキャラを選んだ人だけ（キャラのid）")]
        public string characterId = "";

        [Tooltip("キャラ選択を通さずに遊んだとき（Game シーンだけで試すなど）に、この絵を出す席（ふつうは色が同じ席: 赤 P1・青 P2・緑 P3・黄 P4）。")]
        [SkillLabel("キャラ選択なしのときに出す人")]
        public Players withoutCharacterSelect = Players.P1;

        [Header("置き方")]
        [Tooltip("ONなら、絵を床に寝かせて、砲台が狙う向きへ一緒に回る（絵の上が狙う向き）。OFFなら、カメラの方へ立てて、いつも画面の正面を向く。")]
        [SkillLabel("砲台と一緒に回る（床に寝かせる）")]
        public bool rotateWithTurret = true;

        [Tooltip("ONなら、この絵を出している間、元の砲台の見た目（本体・砲身の絵）を隠す。")]
        [SkillLabel("元の砲台の見た目を隠す")]
        public bool hideTurretParts = true;

        [Tooltip("砲台の真ん中から絵の真ん中までのずれ（m）。X = 狙う向きの右、Y = 狙う向きの前。立てて出すときは X = 画面の右、Y = 画面の上。")]
        [SkillLabel("位置のずれ（m）")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("絵の向きの補正（度）。絵の上が狙う向きになっていないときに回す（左回りが +）。")]
        [SkillLabel("向きの補正（度）")]
        public float rotationOffset;

        [Tooltip("絵の高さ（m）。横幅は絵の縦横比で決まる。砲台の大きさ（拡大率）に関係なく、この高さで出す。")]
        [SkillLabel("絵の高さ（m）")]
        [Min(0.1f)]
        public float height = 2.2f;

        [Tooltip("絵にかける色（ふつうは白＝絵のまま）。")]
        [SkillLabel("色")]
        public Color tint = Color.white;

        [Tooltip("絵が切り替わる瞬間に、ぽよんと弾む強さ（0.2 = 20% 大きくなって戻る。0 で弾まない）。")]
        [SkillLabel("切り替わるときに弾む強さ")]
        [Range(0f, 0.6f)]
        public float swapPop = 0.22f;

        [Tooltip("弾みがおさまるまでの秒数。")]
        [SkillLabel("弾む時間（秒）")]
        [Min(0.05f)]
        public float popSeconds = 0.45f;

        [Tooltip("スキル発動中、踏ん張ってぶるぶる震える強さ（大きさの揺れ。0.03 = ±3%。0 で震えない）。")]
        [SkillLabel("発動中の震え")]
        [Range(0f, 0.15f)]
        public float activeTremble = 0.03f;

        [Tooltip("発動中の震えの速さ（1秒あたりの回数）。")]
        [SkillLabel("震えの速さ")]
        [Min(0f)]
        public float trembleFrequency = 16f;

        /// <summary>
        /// この人（playerIndex 0〜3）の砲台に出すか。selectedCharacterId = キャラ選択で選んだキャラの id（選んでいなければ null）、
        /// hasSelection = キャラ選択を通ったか。
        /// </summary>
        public bool ShowsFor(int playerIndex, bool hasSelection, string selectedCharacterId)
        {
            if (hasSelection)
            {
                return string.IsNullOrEmpty(characterId) || selectedCharacterId == characterId;
            }

            return playerIndex >= 0 && playerIndex < 4 && (withoutCharacterSelect & (Players)(1 << playerIndex)) != 0;
        }

        /// <summary>
        /// 大きさの倍率（1 = そのまま）。sincePop = 絵が切り替わってからの秒数、active = 発動中、time = 経過時間。
        /// 切り替わった直後に大きくなって、揺り戻しながら戻る。発動中はさらに小刻みに震える。
        /// </summary>
        public float ScaleAt(float sincePop, bool active, float time)
        {
            var scale = 1f;
            if (swapPop > 0f && sincePop >= 0f && sincePop < popSeconds)
            {
                var p = sincePop / popSeconds;
                scale += swapPop * Mathf.Sin(p * Mathf.PI * 2.5f) * (1f - p) * (1f - p);
            }

            if (active && activeTremble > 0f)
            {
                scale += activeTremble * Mathf.Sin(time * trembleFrequency * Mathf.PI * 2f);
            }

            return scale;
        }
    }
}
