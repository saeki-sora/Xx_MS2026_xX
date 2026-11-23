namespace MS2026.Fortress
{
    /// <summary>
    /// 数がふえはじめた瞬間（まとまりの最初）だけ true を返す（サウンドスタジオ N11「敵が出てくる」、2026-11-23）。
    /// 前にふえてから <c>gap</c> 秒より長く間が空いてからふえたら、新しいまとまりとして数え直す。最初の1回は記録するだけ。
    /// </summary>
    public struct IncreaseBurstDetector
    {
        private bool _started;
        private int _last;
        private float _lastIncreaseTime;

        public bool Update(int count, float now, float gap)
        {
            if (!_started)
            {
                _started = true;
                _last = count;
                _lastIncreaseTime = float.NegativeInfinity;
                return false;
            }

            var increased = count > _last;
            _last = count;
            if (!increased)
            {
                return false;
            }

            var first = now - _lastIncreaseTime > gap;
            _lastIncreaseTime = now;
            return first;
        }

        public void Reset()
        {
            _started = false;
        }
    }
}
