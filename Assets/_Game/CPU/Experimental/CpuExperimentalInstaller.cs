using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験中の要素とのつなぎ】CPU 本体の差し込み口に、このブランチにだけある要素（砲台の移動・付いていくカメラ）を差し込む。
    /// ・このフォルダ（アセンブリ MS2026.CPU.Experimental）は、本体から一切参照されない。消しても、記号 MS2026_CPU_NO_EXPERIMENTAL で切っても、
    ///   本体は直さずにコンパイルでき、CPU は「席から動かない・席のカメラの決まった視点で見る」CPU になる。
    /// ・移動が不採用になったら、このフォルダから移動の部品（CpuTurretMoveOutput・CpuMovePlanner・CpuFollowViewSource）を消す。
    /// 差し込むのは本体の登録（SubsystemRegistration で空になる）の後、CPU が動き出す（AfterSceneLoad）前。
    /// </summary>
    internal static class CpuExperimentalInstaller
    {
        public const string MoveAreaId = "move";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            CpuPlugins.MoveOutput = new CpuTurretMoveOutput();
            CpuPlugins.SetMovePlanner(() => new CpuMovePlanner());
            CpuPlugins.ViewSource = new CpuFollowViewSource();
            CpuPlugins.RegisterSkillArea(MoveAreaId, "動き");
        }
    }
}
