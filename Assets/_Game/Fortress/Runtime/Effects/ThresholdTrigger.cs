namespace MS2026.Fortress
{
    /// <summary>
    /// 値がしきい値を上へ越えた瞬間に1回だけ true を返す（サウンドスタジオ、2026-11-22）。
    /// 越えた後は、しきい値より <c>gap</c> だけ下がるまで2回は返さない（しきい値のあたりで揺れても鳴り続けない）。
    /// 最初からしきい値より上にいたときは返さない（途中から見たときに鳴らないように）。
    /// </summary>
    public struct ThresholdTrigger
    {
        private bool _armed;
        private bool _started;

        public bool Update(float value, float threshold, float gap)
        {
            if (!_started)
            {
                _started = true;
                _armed = value < threshold;
                return false;
            }

            if (_armed && value >= threshold)
            {
                _armed = false;
                return true;
            }

            if (!_armed && value < threshold - gap)
            {
                _armed = true;
            }

            return false;
        }

        /// <summary>最初の状態に戻す（次の値から数え直す）。</summary>
        public void Reset()
        {
            _armed = false;
            _started = false;
        }
    }
}
