using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    public enum DestructibleGroupMode
    {
        /// <summary>グループの中の1つが壊れたら、残りも全部同時に壊れる。</summary>
        DestroyTogether,

        /// <summary>グループの中の1つが受けたダメージを、残り全員も同じだけ受ける。</summary>
        ShareDamage
    }

    /// <summary>他の破壊可能物との連動（グループ・連鎖爆発）の設定。個体ごとの設定。</summary>
    [Serializable]
    public sealed class DestructibleLinkSettings
    {
        [Tooltip("同じ名前を付けた破壊可能物どうしが連動する。空なら連動しない。")]
        public string groupId = "";

        [Tooltip("グループの連動のしかた。")]
        public DestructibleGroupMode groupMode = DestructibleGroupMode.DestroyTogether;

        [Tooltip("壊れたとき、この半径(ワールド単位)内の他の破壊可能物にダメージを与える（連鎖爆発）。0で連鎖しない。")]
        [Min(0f)]
        public float chainRadius;

        [Tooltip("連鎖するまでの遅れ(秒)。少し遅らせると連鎖が目で追える。")]
        [Min(0f)]
        public float chainDelaySeconds = 0.15f;

        [Tooltip("連鎖で周囲に与えるダメージ。")]
        [Min(0f)]
        public float chainDamage = 20f;

        public bool HasGroup => !string.IsNullOrEmpty(groupId);
    }

    /// <summary>「最初は壊せない」条件の設定。個体ごとの設定。</summary>
    [Serializable]
    public sealed class DestructibleProtection
    {
        [Tooltip("ONなら、ゲーム開始時は無敵（レーザーが当たっても壊れない）。下の条件を満たすか、スクリプトから解除されると壊せるようになる。")]
        public bool startsInvulnerable;

        [Tooltip("ゲーム開始からこの秒数が経つと無敵が解ける。0で時間では解けない。")]
        [Min(0f)]
        public float unlockAfterSeconds;

        [Tooltip("ここに指定した破壊可能物が全て壊されると無敵が解ける（「発生装置を壊すとバリアが消える」等）。")]
        public List<DestructibleObstacle> unlockWhenDestroyed = new List<DestructibleObstacle>();
    }
}
