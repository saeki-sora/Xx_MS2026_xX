using MS2026.Fortress;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】菌にやられて倒れる仕組み（TurretDownSystem）の調子を、CPU 本体の「仲間の調子」の口へ渡す（<see cref="TurretMoveHooks.GetCondition"/>）。
    /// 倒れる仕組みが不採用になったら、このフォルダ（Rescue）ごと消す。
    /// </summary>
    internal sealed class CpuTurretDownStatus : ICpuTeammateStatus
    {
        // この体力より低いと「弱っている（助けると良い）」とみなす。
        public const float HurtBelowHealth = 0.9f;

        public CpuTeammateCondition GetCondition(int player)
        {
            switch (TurretMoveHooks.GetCondition(player))
            {
                case TurretCondition.Downed:
                    return CpuTeammateCondition.Downed;
                case TurretCondition.Normal:
                    return TurretMoveHooks.GetHealth01(player) < HurtBelowHealth ? CpuTeammateCondition.Hurt : CpuTeammateCondition.Normal;
                default:
                    return CpuTeammateCondition.Normal; // 復活した直後（やられない間）
            }
        }

        public float GetHealth01(int player) => TurretMoveHooks.GetHealth01(player);
    }
}
