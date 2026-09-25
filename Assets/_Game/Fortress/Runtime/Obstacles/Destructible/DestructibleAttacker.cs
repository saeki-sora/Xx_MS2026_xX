using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ダメージ・破壊を与えた相手の情報。「誰が壊したか」を記録するための値で、分かる範囲だけ埋まる
    /// （レーザー以外や連鎖の途中では空のことがある）。値型なので気軽にイベントに乗せられる。
    /// </summary>
    public readonly struct DestructibleAttacker
    {
        /// <summary>プレイヤー番号(0-3)。分からなければ-1。</summary>
        public readonly int PlayerIndex;

        /// <summary>攻撃してきた砲台。将来、砲台以外の攻撃元が増えても参照だけ残せるようComponentで持つ。</summary>
        public readonly LaserTurret Turret;

        public static readonly DestructibleAttacker None = default;

        public DestructibleAttacker(int playerIndex, LaserTurret turret)
        {
            PlayerIndex = playerIndex;
            Turret = turret;
        }

        public static DestructibleAttacker FromTurret(LaserTurret turret)
        {
            return turret != null ? new DestructibleAttacker(turret.playerIndex, turret) : None;
        }

        public bool HasPlayer => PlayerIndex >= 0;
        public bool IsKnown => HasPlayer || Turret != null;

        public Color PlayerColor => HasPlayer ? FortressColors.PlayerColor(PlayerIndex) : Color.white;

        public string DisplayName => HasPlayer ? $"プレイヤー{PlayerIndex + 1}" : "不明";

        public override string ToString() => DisplayName;
    }
}
