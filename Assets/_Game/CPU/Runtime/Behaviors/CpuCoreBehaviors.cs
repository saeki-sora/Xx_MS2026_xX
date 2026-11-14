using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 本体の行動を登録する（差し込む側と同じやり方で登録する。消すときはここから外すだけ）。
    /// </summary>
    internal static class CpuCoreBehaviors
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            CpuPlugins.RegisterBehavior(CpuShootBehavior.BehaviorId, () => new CpuShootBehavior());
            CpuPlugins.RegisterBehavior(CpuCoolDownBehavior.BehaviorId, () => new CpuCoolDownBehavior());
            CpuPlugins.RegisterBehavior(CpuSkillBehavior.BehaviorId, () => new CpuSkillBehavior());
            CpuPlugins.RegisterBehavior(CpuSmashBallBehavior.BehaviorId, () => new CpuSmashBallBehavior());
            CpuPlugins.RegisterBehavior(CpuBrushingBehavior.BehaviorId, () => new CpuBrushingBehavior());
        }
    }
}
