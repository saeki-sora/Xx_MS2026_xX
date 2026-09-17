namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// 1人のプレイヤー分のノイズフィルタ状態を保持し、生値を逐次フィルタリングするインターフェース。
    /// 状態を持つため、プレイヤーごとに別インスタンスを用意すること。
    /// </summary>
    public interface IGripSignalFilter
    {
        /// <summary>新しい生値を1つ入力し、フィルタ後の値を返す。</summary>
        float Filter(float rawValue);

        /// <summary>内部状態をリセットする(トランスポート切替時などに使用)。</summary>
        void Reset();
    }
}
