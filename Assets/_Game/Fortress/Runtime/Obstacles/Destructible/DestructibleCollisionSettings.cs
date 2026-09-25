using System;
using UnityEngine;

namespace MS2026.Fortress
{
    public enum DestructibleShape
    {
        Box,
        Circle,
        Capsule,

        /// <summary>当たり判定は自分で用意したものをそのまま使う（自動では作らない・替えない）。</summary>
        Custom
    }

    /// <summary>当たり判定と、敵の経路への影響に関する設定。</summary>
    [Serializable]
    public sealed class DestructibleCollisionSettings
    {
        [Tooltip("当たり判定の形。大きさはオブジェクトのスケールに合わせて決まる。")]
        public DestructibleShape shape = DestructibleShape.Box;

        [Tooltip("ONなら、壊れていない間は敵の通れない壁になり、敵は迂回する。OFFなら敵は素通りする（レーザーは当たる）。")]
        public bool blocksEnemies = true;

        [Tooltip("ONなら、壊れた後に瓦礫が残り、通れるが遅くなる地帯（通行コスト地帯）になる。")]
        public bool leavesRubble;

        [Tooltip("瓦礫の上を歩く敵の移動速度倍率。0.5なら半分の速さ。")]
        [Range(0.05f, 1f)]
        public float rubbleSpeedMultiplier = 0.6f;

        [Tooltip("瓦礫の通行コスト倍率。大きいほど敵は瓦礫を避けて迂回する。")]
        [Min(1f)]
        public float rubbleCostMultiplier = 2f;
    }
}
