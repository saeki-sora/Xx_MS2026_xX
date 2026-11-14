namespace MS2026.Cpu
{
    /// <summary>仲間の調子。</summary>
    public enum CpuTeammateCondition
    {
        /// <summary>ふつう（または、調子という仕組みが無い）。</summary>
        Normal = 0,

        /// <summary>汚れている・弱っている（助けると良い）。</summary>
        Hurt = 1,

        /// <summary>倒れていて動けない（助けないと戻らない）。</summary>
        Downed = 2
    }

    /// <summary>
    /// 仲間（自分も含む）の調子を CPU の目に知らせる口。
    /// 本体には無い（菌にやられて倒れる仕組みは実験中の要素なので、MS2026.CPU.Experimental が差し込む）。無ければ全員ふつう。
    /// </summary>
    public interface ICpuTeammateStatus
    {
        CpuTeammateCondition GetCondition(int player);

        /// <summary>元気さ（1 = 元気、0 = 倒れる寸前・倒れている）。</summary>
        float GetHealth01(int player);
    }
}
