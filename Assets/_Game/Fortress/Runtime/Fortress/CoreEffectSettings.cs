using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアクリスタルの演出欄。<see cref="CoreCrystalController"/> が持ち、ダメージを受けた瞬間と壊れた瞬間に鳴らす。
    /// コアへのダメージは Host だけが与えるので、今は Host（と1人用）の画面でだけ出る。
    /// </summary>
    [Serializable]
    public sealed class CoreEffectSettings
    {
        [Tooltip("敵がコアに当たってダメージを受けたときの演出（パリーンと割れる）。" +
                 "ダメージは毎フレーム入るので、下の「間隔」より短い間隔では出さない。")]
        public FortressEffect onDamaged = new FortressEffect();

        [Tooltip("コアのHPが0になって壊れたときの演出。")]
        public FortressEffect onDestroyed = new FortressEffect();

        [Tooltip("コアのHPが少なくなった（下の「ピンチ」の割合を下回った）瞬間に1回出す演出（サウンドスタジオ N9）。" +
                 "回復して少し上へ戻るまで、2回は出ない。")]
        public FortressEffect onPinch = new FortressEffect();

        [Range(0f, 1f), Tooltip("「コアがピンチ」を出すHPの割合（0〜1）。")]
        public float pinchAt = 0.25f;

        [Tooltip("コアのHPが回復しはじめた瞬間に1回出す演出（緑スキル・歯みがきなど。サウンドスタジオ N12）。" +
                 "じわじわ回復している間は、ひと続きとして1回だけ。試合の始めに満タンにするときは出ない。")]
        public FortressEffect onHealed = new FortressEffect();

        [Min(0f), Tooltip("ダメージ演出を出す最短の間隔（秒）。")]
        public float damageEffectInterval = 0.25f;
    }
}
