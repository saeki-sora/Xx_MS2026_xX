namespace MS2026.GripInputBridge.Processing
{
    /// <summary>フィルタ設定が未割り当ての場合のフォールバック。値をそのまま通す。</summary>
    public sealed class PassthroughFilter : IGripSignalFilter
    {
        public float Filter(float rawValue) => rawValue;

        public void Reset()
        {
            // 状態を持たないため何もしない。
        }
    }
}
