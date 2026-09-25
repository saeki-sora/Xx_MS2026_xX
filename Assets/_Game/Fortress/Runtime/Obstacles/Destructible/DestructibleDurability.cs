using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>耐久力に関する設定（耐久値・受けるダメージの倍率・自己修復・破壊後の再生）。</summary>
    [Serializable]
    public sealed class DestructibleDurability
    {
        [Tooltip("耐久値。レーザーのダメージがこの値を超えると破壊される。")]
        [Min(1f)]
        public float maxHealth = 30f;

        [Tooltip("受けるダメージの倍率。0.5なら半分の速さで壊れる（硬い個体）、2なら倍の速さで壊れる（脆い個体）。")]
        [Min(0f)]
        public float damageMultiplier = 1f;

        [Tooltip("ダメージを受けてからこの秒数のあいだ攻撃されないと、耐久が少しずつ自己修復し始める。")]
        [Min(0f)]
        public float selfRepairDelaySeconds = 3f;

        [Tooltip("自己修復の速さ（耐久値/秒）。0で自己修復しない。")]
        [Min(0f)]
        public float selfRepairPerSecond;

        [Tooltip("破壊された後、時間経過で元に戻るか。")]
        public bool regenerates = true;

        [Tooltip("破壊されてから再生するまでの時間(秒)。")]
        [Min(0f)]
        public float regenDelaySeconds = 10f;

        [Tooltip("再生したときの耐久の割合。1で全快、0.5なら半分の耐久で復活する。")]
        [Range(0.05f, 1f)]
        public float regenHealthRatio = 1f;
    }
}
