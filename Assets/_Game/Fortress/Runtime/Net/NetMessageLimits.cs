using Unity.Collections.LowLevel.Unsafe;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 1回の送信に詰められる量の上限。
    /// NGOの取りこぼしてよい送信(Unreliable)は分割されないため、1回あたり NonFragmentedMessageMaxSize(既定1296バイト)を超えると
    /// 例外(OverflowException "RPC parameters are too large for unreliable delivery")になり、何も届かない
    /// (2026-10-04 の計測で、群衆の補正が全く届いていなかった原因)。確実に届ける送信(Reliable)は分割されるので上限は無い。
    /// </summary>
    public static class NetMessageLimits
    {
        /// <summary>NGO NetworkMessageManager.DefaultNonFragmentedMessageMaxSize と同じ値(公開されていないため写している)。</summary>
        public const int UnreliableMaxBytes = 1296;

        /// <summary>見出し(メッセージの種類・対象オブジェクト・RPCの番号・連番・配列の長さ等)の分として空けておくバイト数。</summary>
        public const int HeaderAllowanceBytes = 96;

        /// <summary>Unreliable の1回で送れる、T型の要素の数。</summary>
        public static int UnreliableItemsPerMessage<T>() where T : struct
        {
            return (UnreliableMaxBytes - HeaderAllowanceBytes) / UnsafeUtility.SizeOf<T>();
        }
    }
}
