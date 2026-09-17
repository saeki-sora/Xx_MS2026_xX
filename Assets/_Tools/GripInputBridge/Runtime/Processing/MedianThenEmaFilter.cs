namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// 設計書 7.2 の推奨初期設定。メディアンでスパイクを落としてからEMAで滑らかにする合成フィルタ。
    /// EMAは単発の外れ値を後々まで"引きずってしまう"性質があるため、先にメディアンでスパイクだけを
    /// 除去してからEMAに渡すことで、スパイクにもガタつきにも強くしている。
    /// </summary>
    public sealed class MedianThenEmaFilter : IGripSignalFilter
    {
        private readonly MedianFilter _median;
        private readonly ExponentialMovingAverageFilter _ema;

        public MedianThenEmaFilter(int medianWindowSize, float emaAlpha)
        {
            _median = new MedianFilter(medianWindowSize);
            _ema = new ExponentialMovingAverageFilter(emaAlpha);
        }

        public float Filter(float rawValue)
        {
            return _ema.Filter(_median.Filter(rawValue));
        }

        public void Reset()
        {
            _median.Reset();
            _ema.Reset();
        }
    }
}
