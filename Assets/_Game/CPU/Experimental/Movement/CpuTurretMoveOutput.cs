using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】CPU の「行きたい向き」を、砲台の移動の係（TurretMoveDirector）へ渡す（<see cref="TurretMoveHooks.SetPilotMove"/>）。
    /// 動かす計算（加速・壁・歯・敵に押される・動ける範囲）と LAN への位置の配り方は、人の WASD と同じ。
    /// </summary>
    internal sealed class CpuTurretMoveOutput : ICpuMoveOutput
    {
        public void SetMove(int player, Vector2 direction)
        {
            TurretMoveHooks.SetPilotMove(player, direction);
        }

        public void ClearMove(int player)
        {
            TurretMoveHooks.ClearPilotMove(player);
        }
    }
}
