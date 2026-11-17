using System;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 副砲（普段のレーザーに付く、キャラごとの小さな攻撃。2026-11-17）の設定の共通部分。
    /// キャラのスキルの設定ファイル（<see cref="CharacterSkill.SubGun"/>）の中に1つ入れておくと、
    /// <see cref="PlayerSkillInstaller"/> がそのキャラの砲台に <see cref="SubGunRunner"/> を付ける。
    /// スキル調整スタジオでは「副砲」の欄に出る（<see cref="SkillTab.SubGun"/>）。
    /// 決まり: 本体のレーザーを撃っている間だけ撃つ（握っていない・オーバーヒート・スキル中は撃たない）。熱は増やさない。
    /// 当たりの結果（ダメージ）は結果を決めるPC（オフライン／Host）だけ、見た目と音は全PCがそれぞれ出す。
    /// </summary>
    [Serializable]
    public abstract class SubGunSettings
    {
        [SkillQuick(0, "副砲を使う")]
        [Tooltip("OFF にすると、このキャラの副砲は出ない（普段のレーザーだけになる）。")]
        [SkillLabel("使う")]
        public bool enabled = true;

        [Tooltip("副砲の色の、プレイヤー色に白を混ぜる割合。0 でプレイヤー色そのまま、1 で真っ白。" +
                 "本体のレーザー（白い芯で一番明るい）と見分けられるよう、キャラの色がはっきり分かる程度にする。")]
        [SkillLabel("色の薄さ（白を混ぜる割合）")]
        [Range(0f, 1f)]
        public float whiten = 0.2f;

        [Tooltip("副砲の濃さ（全体の不透明度）。")]
        [SkillLabel("濃さ")]
        [Range(0f, 1f)]
        public float opacity = 1f;

        [Tooltip("副砲の線・形のまわりに付ける暗い縁取りの太さ（m）。床や敵の上でも形が読めるようにする。0 で縁取りなし。")]
        [SkillLabel("縁取りの太さ（m）")]
        [Min(0f)]
        public float outlineWidth = 0.035f;

        [Tooltip("縁取りの濃さ。")]
        [SkillLabel("縁取りの濃さ")]
        [Range(0f, 1f)]
        public float outlineAlpha = 0.8f;

        [SkillQuick(20, "当たった所の光の大きさ（m）")]
        [Tooltip("副砲が敵に当たった所に出す、キャラの色の星形の光の大きさ（m）。何が起きたか分かるようにするための合図。0 で出さない。")]
        [SkillLabel("当たった所の光の大きさ（m）")]
        [Min(0f)]
        public float hitFlashSize = 0.55f;

        [Tooltip("当たった所の光が出ている時間（秒）。前半は大きく、後半は小さく（止めのある2段）。")]
        [SkillLabel("当たった所の光の時間（秒）")]
        [Min(0.02f)]
        public float hitFlashSeconds = 0.16f;

        /// <summary>この副砲を動かす係を砲台に付けて返す。</summary>
        public abstract SubGunRunner AddRunner(GameObject turretObject);

        /// <summary>副砲の色（プレイヤー色に少し白を混ぜた色）。</summary>
        public Color ColorFor(int playerIndex, float alpha = 1f)
        {
            var color = Color.Lerp(FortressColors.PlayerColor(playerIndex), Color.white, whiten);
            color.a = opacity * alpha;
            return color;
        }

        /// <summary>縁取りの色（暗い色）。</summary>
        public Color OutlineColor(float alpha = 1f)
        {
            return new Color(0.03f, 0.04f, 0.07f, outlineAlpha * opacity * alpha);
        }
    }

    /// <summary>握る強さ（本体の太さ 0〜1）で、弱いときの値と強いときの値の間を取る値。</summary>
    [Serializable]
    public struct GripRange
    {
        [Tooltip("軽く握ったとき（本体が一番細いとき）の値。")]
        [SkillLabel("軽く握る")]
        public float weak;

        [Tooltip("強く握ったとき（本体が一番太いとき）の値。")]
        [SkillLabel("強く握る")]
        public float strong;

        public GripRange(float weak, float strong)
        {
            this.weak = weak;
            this.strong = strong;
        }

        public float At(float grip01) => Mathf.Lerp(weak, strong, Mathf.Clamp01(grip01));
    }
}
