using System;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// メディアンで一瞬のスパイクを落としてから、1€フィルターでならす合成フィルタ。
    /// 1€フィルターはスパイクを「速い動き」と見なして通してしまうため、先にメディアンを置く
    /// （<see cref="MedianThenEmaFilter"/> と同じ考え方）。
    /// </summary>
    public sealed class MedianThenOneEuroFilter : IGripSignalFilter
    {
        private readonly MedianFilter _median;
        private readonly OneEuroFilter _oneEuro;

        public MedianThenOneEuroFilter(int medianWindowSize, float minCutoff, float beta, float derivativeCutoff, Func<double> clock = null)
        {
            _median = new MedianFilter(medianWindowSize);
            _oneEuro = new OneEuroFilter(minCutoff, beta, derivativeCutoff, clock);
        }

        public float Filter(float rawValue)
        {
            return _oneEuro.Filter(_median.Filter(rawValue));
        }

        public void Reset()
        {
            _median.Reset();
            _oneEuro.Reset();
        }
    }
}
