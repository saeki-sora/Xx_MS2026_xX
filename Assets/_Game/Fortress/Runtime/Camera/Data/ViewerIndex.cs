using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 「誰の視点か」を表す番号の約束事。0-3がプレイヤー(砲台のplayerIndexと同じ)、-1が全体視点(観戦・未接続時など)。
    /// </summary>
    public static class ViewerIndex
    {
        public const int Overview = -1;
        public const int PlayerCount = 4;

        public static bool IsPlayer(int viewer) => viewer >= 0 && viewer < PlayerCount;

        public static bool IsValid(int viewer) => viewer == Overview || IsPlayer(viewer);

        public static string Label(int viewer) => IsPlayer(viewer) ? $"P{viewer + 1}" : "全体";

        public static string LongLabel(int viewer) => IsPlayer(viewer) ? $"プレイヤー{viewer + 1}" : "全体視点";

        public static Color Color(int viewer) => IsPlayer(viewer) ? FortressColors.PlayerColor(viewer) : new Color(0.85f, 0.85f, 0.85f);

        /// <summary>全体視点 → P1 → … → P4 の順。ツールの並びやループ用。</summary>
        public static readonly int[] All = { Overview, 0, 1, 2, 3 };
    }
}
