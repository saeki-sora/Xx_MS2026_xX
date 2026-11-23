using System;
using DDrive.Generated;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザー1本分の演出欄（発射口・着弾点・群衆の敵に当たった瞬間）。<see cref="LaserTuningConfig"/> が持ち、
    /// <see cref="LaserTurretEffects"/> が鳴らす。要塞デザイナーの「砲台配置」タブで編集する。
    /// </summary>
    [Serializable]
    public sealed class LaserEffectSettings
    {
        [Tooltip("撃っている間、発射口（Muzzle）に出し続けるエフェクトと、撃ち始めに1回鳴らす効果音。" +
                 "エフェクトは寿命が「ループ」のVFXにすると、撃ち終わるまで出続ける。")]
        public FortressEffect muzzle = new FortressEffect { se = SEID.Se };

        [Tooltip("レーザーが壁・破壊可能物などに当たっている間、当たった場所に出し続けるエフェクトと、当たり始めに1回鳴らす効果音。" +
                 "何にも当たっていないとき（射程の端）は出ない。群衆の敵はビームが貫通するので、ここではなく下の欄。")]
        public FortressEffect impact = new FortressEffect();

        [Tooltip("群衆の敵に当たった瞬間（しばらく当たっていなかった敵にビームが触れた瞬間）に1回出すエフェクトと効果音。" +
                 "数の上限は群衆の設定「レーザー命中エフェクトの上限」（全レーザー合計）。")]
        public FortressEffect swarmHit = new FortressEffect();

        [Tooltip("ビームを撃ち終わったあと、砲台に出し続けるエフェクト（湯気など）と、出始めに1回鳴らす効果音。" +
                 "ビームを撃った時間が長いほど、湯気が出ている時間も長くなる（下の2つの設定）。" +
                 "エフェクトは寿命が「ループ」のVFXにすると、湯気の時間が終わるまで出続ける。")]
        public FortressEffect steam = new FortressEffect();

        [Tooltip("握りはじめて溜めに入った瞬間に1回出すエフェクトと効果音（サウンドスタジオ N5）。")]
        public FortressEffect onChargeStart = new FortressEffect();

        [Tooltip("熱がたまってオーバーヒートした瞬間に1回出すエフェクトと効果音（サウンドスタジオ N6）。")]
        public FortressEffect onOverheat = new FortressEffect();

        [Tooltip("オーバーヒートが治って、また撃てるようになった瞬間に1回出すエフェクトと効果音（サウンドスタジオ N7）。")]
        public FortressEffect onRecovered = new FortressEffect();

        [Tooltip("熱がもうすぐオーバーヒートする所（下の「熱の危ない所」）を越えた瞬間に1回出すエフェクトと効果音（サウンドスタジオ N8）。" +
                 "熱が少し下がるまで、2回は鳴らない。")]
        public FortressEffect onHeatWarning = new FortressEffect();

        [Range(0f, 1f), Tooltip("「熱が危ない」を鳴らす熱の量（0〜1。1 でオーバーヒート）。")]
        public float heatWarnAt = 0.8f;

        [Min(0f), Tooltip("ビームを1秒撃つごとに増える湯気の時間（秒）。1なら、撃った時間と同じだけ湯気が出る。")]
        public float steamSecondsPerFireSecond = 1f;

        [Min(0f), Tooltip("湯気が出続ける時間の上限（秒）。長く撃ち続けても、これ以上は増えない。")]
        public float maxSteamSeconds = 10f;
    }
}
