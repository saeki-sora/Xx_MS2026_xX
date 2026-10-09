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

        [Min(0f), Tooltip("ダメージ演出を出す最短の間隔（秒）。")]
        public float damageEffectInterval = 0.25f;
    }
}
