namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 握力センサーの強さ（0〜1）から「握った」「離した」を数える。
    /// センサーの値はなめらかにされているので、すばやく握り直すと一度も「離した」強さまで下がらないことがある。
    /// そこで、いちばん強く握ったところから <c>dip</c> 以上ゆるんだら「離した」と数える（連打を1回ずつ数える）。
    /// Unity に頼らないので、テストで中身を確かめられる。
    /// </summary>
    public sealed class GripSqueezeTracker
    {
        private bool _down;
        private float _peak;
        private float _trough = 1f;

        public bool IsPressed => _down;

        /// <summary>
        /// 毎フレーム1回呼ぶ。on 以上で握った、off 未満か（いちばん強いところ − dip）未満で離した。
        /// 離した後は、いちばんゆるんだところから少し強く握り直したら、また「握った」と数える。戻り値 = このフレームで離した回数（0 か 1）。
        /// </summary>
        public int Tick(float value, float on, float off, float dip)
        {
            if (_down)
            {
                if (value > _peak)
                {
                    _peak = value;
                }

                if (value < off || value < _peak - dip)
                {
                    _down = false;
                    _trough = value;
                    return 1;
                }

                return 0;
            }

            if (value < _trough)
            {
                _trough = value;
            }

            var rise = dip < on - off ? dip : on - off;
            if (value >= on && value - _trough >= rise)
            {
                _down = true;
                _peak = value;
            }

            return 0;
        }

        public void Reset()
        {
            _down = false;
            _peak = 0f;
            _trough = 0f;
        }
    }
}
