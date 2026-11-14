using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 行き先を決める係（「手が2本」の移動の方。撃つなどの行動と同時に動く）。
    /// 本体には無い（砲台の移動は実験中の要素なので、MS2026.CPU.Experimental が差し込む）。無ければ CPU は動かない。
    /// CPU 1人ごとに1つずつ作られる。
    /// </summary>
    public interface ICpuMovePlanner
    {
        /// <summary>行きたい向き（ワールド。長さ 0〜1 = 速さの割合）。動かないなら Vector2.zero。</summary>
        Vector2 Plan(CpuContext context, float deltaTime);

        /// <summary>CPU が止まるとき。</summary>
        void Reset();
    }

    /// <summary>
    /// 行きたい向きを、実際に砲台を動かす係へ渡す口。本体には無い（MS2026.CPU.Experimental が差し込む）。
    /// </summary>
    public interface ICpuMoveOutput
    {
        /// <summary>プレイヤー player を、この向き（長さ 0〜1）へ動かしたい。</summary>
        void SetMove(int player, Vector2 direction);

        /// <summary>プレイヤー player の移動の入力をやめる（CPU が止まる・人と入れ替わるとき）。</summary>
        void ClearMove(int player);
    }
}
