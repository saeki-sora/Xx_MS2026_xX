using System;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>直近Nサンプルの単純移動平均でガタつきを均す。</summary>
    public sealed class MovingAverageFilter : IGripSignalFilter
    {
        private readonly float[] _buffer;
        private int _count;
        private int _writeIndex;
        private float _sum;

        public MovingAverageFilter(int windowSize)
        {
            if (windowSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "windowSizeは1以上である必要があります。");
            }

            _buffer = new float[windowSize];
        }

        public float Filter(float rawValue)
        {
            if (_count < _buffer.Length)
            {
                _buffer[_writeIndex] = rawValue;
                _sum += rawValue;
                _count++;
            }
            else
            {
                _sum -= _buffer[_writeIndex];
                _buffer[_writeIndex] = rawValue;
                _sum += rawValue;
            }

            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            return _sum / _count;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _count = 0;
            _writeIndex = 0;
            _sum = 0f;
        }
    }
}
