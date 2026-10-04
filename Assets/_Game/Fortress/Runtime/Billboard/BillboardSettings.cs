using System;
using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>ビルボード(立たせる)対象の種類。</summary>
    [Flags]
    public enum BillboardTargets
    {
        None = 0,

        /// <summary>敵(群衆・個別の敵)。</summary>
        Enemies = 1 << 0,

        /// <summary>コアクリスタル。</summary>
        Core = 1 << 1,

        /// <summary>砲台の本体(砲身・照準線・レーザーは地面に寝たまま)。</summary>
        TurretBody = 1 << 2,

        /// <summary>破壊可能物・スマッシュボール(HPバー等の子の絵も含む)。</summary>
        Destructibles = 1 << 3,

        All = Enemies | Core | TurretBody | Destructibles
    }

    /// <summary>立たせるときの軸。</summary>
    public enum BillboardAnchor
    {
        /// <summary>絵の下端が地面に接したまま立ち上がる。</summary>
        Feet,

        /// <summary>絵の中心を軸に立ち上がる(下半分は地面に埋まって見える)。</summary>
        Center
    }

    /// <summary>
    /// 地面に寝ている平らな絵を、カメラに向けて「立たせる」設定。視点プリセットごとに持つので、
    /// 「真上から(平ら)」と「斜め見下ろし(立たせる)」をプリセットの切り替えだけで行き来できる。
    /// OFFにすると、差し替えていたマテリアルを元に戻し、以前と全く同じ描画になる。
    /// </summary>
    [Serializable]
    public sealed class BillboardSettings
    {
        [Tooltip("ONで絵を立たせる。OFFなら今まで通り地面に寝た平らな絵。")]
        public bool enabled;

        [Tooltip("立ち上がり具合。1でカメラに正対(真正面を向く)、0で寝たまま。カメラが真上からのときは値に関係なく平らに見える。")]
        [Range(0f, 1f)]
        public float standAmount = 1f;

        [Tooltip("立たせるときの軸。足元なら絵の下端が地面に接したまま立つ。")]
        public BillboardAnchor anchor = BillboardAnchor.Feet;

        [Tooltip("立たせる対象。")]
        public BillboardTargets targets = BillboardTargets.All;

        [Tooltip("この濃さ未満のピクセルは描かない。立たせた絵同士が正しく前後に重なるよう、半透明の縁は切り落とす。")]
        [Range(0.01f, 0.99f)]
        public float alphaCutoff = 0.5f;

        public bool Includes(BillboardTargets target) => enabled && (targets & target) == target;
    }
}
