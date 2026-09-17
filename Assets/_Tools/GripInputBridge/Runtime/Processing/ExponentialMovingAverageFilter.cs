using System;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>指数移動平均(EMA)。直近値を重視しつつ滑らかにする。</summary>
    public sealed class ExponentialMovingAverageFilter : IGripSignalFilter
    {
        private readonly float _alpha;
        private float _value;
        private bool _hasValue;

        public ExponentialMovingAverageFilter(float alpha)
        {
            if (alpha <= 0f || alpha > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), "alphaは0より大きく1以下である必要があります。");
            }

            _alpha = alpha;
        }

        public float Filter(float rawValue)
        {
            if (!_hasValue)
            {
                // 初回はいきなり平均を取ると初期値0からの立ち上がりが不自然に遅れるため、そのまま採用する。
                _value = rawValue;
                _hasValue = true;
                return _value;
            }

            _value += _alpha * (rawValue - _value);
            return _value;
        }

        public void Reset()
        {
            _hasValue = false;
            _value = 0f;
        }
    }
}
