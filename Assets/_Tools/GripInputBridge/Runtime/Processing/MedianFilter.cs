using System;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>単発の外れ値(スパイクノイズ)に強いメディアンフィルタ。</summary>
    public sealed class MedianFilter : IGripSignalFilter
    {
        private readonly float[] _buffer;
        private readonly float[] _sortScratch;
        private int _count;
        private int _writeIndex;

        public MedianFilter(int windowSize)
        {
            if (windowSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "windowSizeは1以上である必要があります。");
            }

            _buffer = new float[windowSize];
            _sortScratch = new float[windowSize];
        }

        public float Filter(float rawValue)
        {
            _buffer[_writeIndex] = rawValue;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }

            // _sortScratchは事前確保済みバッファなので、ここでの並び替えはGCアロケーションを発生させない。
            Array.Copy(_buffer, _sortScratch, _count);
            Array.Sort(_sortScratch, 0, _count);
            return _sortScratch[_count / 2];
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            Array.Clear(_sortScratch, 0, _sortScratch.Length);
            _count = 0;
            _writeIndex = 0;
        }
    }
}
