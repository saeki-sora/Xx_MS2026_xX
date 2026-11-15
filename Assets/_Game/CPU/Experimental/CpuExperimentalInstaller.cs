using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験中の要素とのつなぎ】CPU 本体の差し込み口に、このブランチにだけある要素（砲台の移動・付いていくカメラ・菌にやられて倒れる／復活）を差し込む。
    /// ・このフォルダ（アセンブリ MS2026.CPU.Experimental）は、本体から一切参照されない。消しても、記号 MS2026_CPU_NO_EXPERIMENTAL で切っても、
    ///   本体は直さずにコンパイルでき、CPU は「席から動かない・席のカメラの決まった視点で見る」CPU になる。
    /// ・移動が不採用になったら Movement フォルダを、倒れる仕組みが不採用になったら Rescue フォルダを消し、下の登録から外す。
    /// 差し込むのは本体の登録（SubsystemRegistration で空になる）の後、CPU が動き出す（AfterSceneLoad）前。
    /// </summary>
    internal static class CpuExperimentalInstaller
    {
        public const string MoveAreaId = "move";
        public const string RescueAreaId = "rescue";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // 砲台の移動・付いていくカメラ（Movement フォルダ）。
            CpuPlugins.MoveOutput = new CpuTurretMoveOutput();
            CpuPlugins.SetMovePlanner(() => new CpuMovePlanner());
            CpuPlugins.ViewSource = new CpuFollowViewSource();
            CpuPlugins.RegisterSkillArea(MoveAreaId, "動き");
            CpuPlugins.RegisterAreaReview(MoveAreaId, CpuExperimentalReviews.Move);

            // 菌にやられて倒れる・復活（Rescue フォルダ）。
            CpuPlugins.TeammateStatus = new CpuTurretDownStatus();
            CpuPlugins.RegisterBehavior(CpuReviveBehavior.BehaviorId, () => new CpuReviveBehavior());
            CpuPlugins.RegisterBehavior(CpuStruggleBehavior.BehaviorId, () => new CpuStruggleBehavior());
            CpuPlugins.RegisterSkillArea(RescueAreaId, "救助");
            CpuPlugins.RegisterAreaReview(RescueAreaId, CpuExperimentalReviews.Rescue);
        }
    }
}
