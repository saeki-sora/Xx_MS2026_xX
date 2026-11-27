using System;
using System.Diagnostics;

namespace MS2026.GripInputBridge.Processing
{
    /// <summary>
    /// 1€フィルター（One Euro Filter, Casiez ほか 2012）。
    /// 値がゆっくり動くときは強くならして震えを消し、速く動くときはならしを弱めて遅れを消す。
    /// 経過時間（秒）で計算するので、フレームレートが違う PC どうしでも同じ効き方になる
    /// （EMA・移動平均は「1サンプルごと」に効くので、144fps の PC は 60fps の PC よりならしが弱くなる）。
    ///
    /// 注意: 一瞬だけ跳ねた値（スパイク）は「速い動き」と見なして通してしまう。
    /// スパイクが出るセンサーでは <see cref="MedianThenOneEuroFilter"/> のように先にメディアンを通す。
    /// </summary>
    public sealed class OneEuroFilter : IGripSignalFilter
    {
        private static readonly Stopwatch s_clock = Stopwatch.StartNew();

        private readonly float _minCutoff;
        private readonly float _beta;
        private readonly float _derivativeCutoff;
        private readonly Func<double> _clock;

        private bool _hasValue;
        private double _lastTime;
        private float _value;
        private float _derivative;

        /// <param name="minCutoff">ゆっくり動くときのならし（Hz）。小さいほど震えが消えるが、ゆっくりした動きにも遅れる。</param>
        /// <param name="beta">速さへの反応。大きいほど、速く動いたときの遅れが減る（そのぶん速い揺れも通る）。</param>
        /// <param name="derivativeCutoff">速さの見積もりをならす強さ（Hz）。LAN で届く値のような段々を「速い動き」と取り違えないようにする。</param>
        /// <param name="clock">今の時刻（秒）を返す関数。省略時は単調に増える実時間（テストでは差し替える）。</param>
        public OneEuroFilter(float minCutoff, float beta, float derivativeCutoff, Func<double> clock = null)
        {
            if (minCutoff <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minCutoff), "minCutoffは0より大きい必要があります。");
            }

            if (beta < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(beta), "betaは0以上である必要があります。");
            }

            if (derivativeCutoff <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(derivativeCutoff), "derivativeCutoffは0より大きい必要があります。");
            }

            _minCutoff = minCutoff;
            _beta = beta;
            _derivativeCutoff = derivativeCutoff;
            _clock = clock ?? MonotonicSeconds;
        }

        /// <summary>単調に増える実時間（秒）。スレッドを問わず使える。</summary>
        public static double MonotonicSeconds()
        {
            return s_clock.Elapsed.TotalSeconds;
        }

        public float Filter(float rawValue)
        {
            var now = _clock();
            if (!_hasValue)
            {
                _hasValue = true;
                _lastTime = now;
                _value = rawValue;
                _derivative = 0f;
                return _value;
            }

            var dt = (float)(now - _lastTime);
            if (dt <= 1e-6f)
            {
                // 同じ時刻に2回呼ばれた（時間が進んでいない）。速さが計算できないので、前の値を返す。
                return _value;
            }

            _lastTime = now;

            var rawDerivative = (rawValue - _value) / dt;
            _derivative += Alpha(_derivativeCutoff, dt) * (rawDerivative - _derivative);

            var cutoff = _minCutoff + _beta * Math.Abs(_derivative);
            _value += Alpha(cutoff, dt) * (rawValue - _value);
            return _value;
        }

        public void Reset()
        {
            _hasValue = false;
            _lastTime = 0d;
            _value = 0f;
            _derivative = 0f;
        }

        // 遮断周波数 cutoff(Hz) の1次ローパスを、dt 秒進めるときの混ぜる割合。
        private static float Alpha(float cutoff, float dt)
        {
            var tau = 1f / (2f * (float)Math.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}
