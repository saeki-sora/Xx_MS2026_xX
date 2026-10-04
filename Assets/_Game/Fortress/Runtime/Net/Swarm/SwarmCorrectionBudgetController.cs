namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 1人のClientへ送る補正の通信量(バイト/秒)を決める(段階5。Unity API非依存・EditModeテスト対象)。
    ///
    /// ・ズレ: Clientが報告したズレが大きい(HighError以上)ときは、すぐ最大(MaxBytesPerSecond)まで増やす。
    ///   小さい(LowError以下)ときは、普段の量(NormalBytesPerSecond)へゆっくり戻す。その間はズレに応じて間の量。
    /// ・回線の混み具合: 往復時間(RTT。RttSmoothingSeconds でならした値)がそのClientの一番良かった値より CongestionRttMs 以上伸びたら、回線が詰まってきたとみなし、
    ///   上限を BackoffFactor 倍に下げる(BackoffIntervalSeconds に1回まで)。伸びていなければ上限を少しずつ戻す
    ///   (TCPと同じ「詰まったら大きく下げ、空いたら少しずつ上げる」考え方)。
    /// 実際に使う量 = min(ズレから決めた量, 混み具合の上限)。ただし MinBytesPerSecond は下回らない。
    /// 注意: NGO(UnityTransport)のRTTには双方のフレーム時間も含まれる(2026-10-05の計測で、FPSが430→90に落ちるとLAN内でも1→10〜25ms)。
    /// 一瞬の伸びやFPSの低下で誤って下げないよう、ならした値を使い、基準(一番良かった値)も早めに忘れる。
    /// </summary>
    public sealed class SwarmCorrectionBudgetController
    {
        public float MinBytesPerSecond { get; set; } = 120f * 1024f;
        public float NormalBytesPerSecond { get; set; } = 720f * 1024f;
        public float MaxBytesPerSecond { get; set; } = 1800f * 1024f;
        public float HighError { get; set; } = 1f;
        public float LowError { get; set; } = 0.3f;

        /// <summary>普段の量へ戻す速さ(1秒あたり、最大と普段の差の何割を戻すか)。</summary>
        public float RelaxFractionPerSecond { get; set; } = 0.25f;

        /// <summary>RTT(ならした値)が一番良かった値よりこれ以上(ミリ秒)伸びたら、回線が詰まってきたとみなす。</summary>
        public float CongestionRttMs { get; set; } = 40f;

        /// <summary>RTTをならす時間(秒)。0ならならさない。</summary>
        public float RttSmoothingSeconds { get; set; } = 0.5f;

        public float BackoffFactor { get; set; } = 0.7f;
        public float BackoffIntervalSeconds { get; set; } = 0.25f;

        /// <summary>混み具合の上限を戻す速さ(1秒あたり、最大の量の何割を戻すか)。</summary>
        public float RecoverFractionPerSecond { get; set; } = 0.2f;

        /// <summary>一番良かったRTTを忘れていく速さ(1秒あたりのミリ秒。経路が変わったときに基準を合わせ直すため)。</summary>
        public float BaselineForgetMsPerSecond { get; set; } = 10f;

        /// <summary>今使う量(バイト/秒)。</summary>
        public float CurrentBytesPerSecond { get; private set; }

        /// <summary>ズレから決めた量(バイト/秒)。</summary>
        public float ErrorBytesPerSecond { get; private set; }

        /// <summary>混み具合の上限(バイト/秒)。</summary>
        public float CongestionCapBytesPerSecond { get; private set; }

        /// <summary>そのClientの一番良かったRTT(ミリ秒、ならした値)。まだ測れていなければ負。</summary>
        public float BaselineRttMs { get; private set; } = -1f;

        /// <summary>ならしたRTT(ミリ秒)。まだ測れていなければ負。</summary>
        public float SmoothedRttMs { get; private set; } = -1f;

        /// <summary>混み具合で量を下げた回数(累計、計測用)。</summary>
        public int Backoffs { get; private set; }

        private float _sinceBackoff = float.MaxValue;

        // 最初のUpdateで、その時点の設定(普段の量・最大)から始める(設定はコンストラクタの後に入るため)。
        private bool _started;

        public SwarmCorrectionBudgetController()
        {
            CurrentBytesPerSecond = NormalBytesPerSecond;
        }

        /// <summary>最初の状態(普段の量、混み具合の上限なし)に戻す。</summary>
        public void Reset()
        {
            _started = false;
            BaselineRttMs = -1f;
            SmoothedRttMs = -1f;
            _sinceBackoff = float.MaxValue;
            CurrentBytesPerSecond = NormalBytesPerSecond;
        }

        /// <param name="deltaTime">前回からの経過秒。</param>
        /// <param name="reportedError">Clientが報告したズレ。報告が無ければ負の値。</param>
        /// <param name="rttMs">今の往復時間(ミリ秒)。測れなければ負の値。</param>
        public float Update(float deltaTime, float reportedError, float rttMs)
        {
            var max = MaxBytesPerSecond;
            var normal = NormalBytesPerSecond < max ? NormalBytesPerSecond : max;
            var min = MinBytesPerSecond < normal ? MinBytesPerSecond : normal;
            if (!_started)
            {
                _started = true;
                ErrorBytesPerSecond = normal;
                CongestionCapBytesPerSecond = max;
            }

            var desired = normal;
            if (reportedError >= HighError)
            {
                desired = max;
            }
            else if (reportedError > LowError)
            {
                var t = (reportedError - LowError) / (HighError - LowError);
                desired = normal + (max - normal) * t;
            }

            if (desired > ErrorBytesPerSecond)
            {
                ErrorBytesPerSecond = desired;
            }
            else
            {
                var next = ErrorBytesPerSecond - (max - normal) * RelaxFractionPerSecond * deltaTime;
                ErrorBytesPerSecond = next > desired ? next : desired;
            }

            _sinceBackoff += deltaTime;
            if (rttMs >= 0f)
            {
                if (SmoothedRttMs < 0f || RttSmoothingSeconds <= 0f)
                {
                    SmoothedRttMs = rttMs;
                }
                else
                {
                    var k = deltaTime / (RttSmoothingSeconds + deltaTime);
                    SmoothedRttMs += (rttMs - SmoothedRttMs) * k;
                }

                rttMs = SmoothedRttMs;
                if (BaselineRttMs < 0f || rttMs < BaselineRttMs)
                {
                    BaselineRttMs = rttMs;
                }
                else
                {
                    BaselineRttMs += BaselineForgetMsPerSecond * deltaTime;
                }

                if (rttMs > BaselineRttMs + CongestionRttMs)
                {
                    if (_sinceBackoff >= BackoffIntervalSeconds)
                    {
                        var basis = CurrentBytesPerSecond < CongestionCapBytesPerSecond ? CurrentBytesPerSecond : CongestionCapBytesPerSecond;
                        CongestionCapBytesPerSecond = basis * BackoffFactor;
                        _sinceBackoff = 0f;
                        Backoffs++;
                    }
                }
                else
                {
                    CongestionCapBytesPerSecond += max * RecoverFractionPerSecond * deltaTime;
                }
            }
            else
            {
                CongestionCapBytesPerSecond += max * RecoverFractionPerSecond * deltaTime;
            }

            if (CongestionCapBytesPerSecond > max)
            {
                CongestionCapBytesPerSecond = max;
            }
            else if (CongestionCapBytesPerSecond < min)
            {
                CongestionCapBytesPerSecond = min;
            }

            var current = ErrorBytesPerSecond < CongestionCapBytesPerSecond ? ErrorBytesPerSecond : CongestionCapBytesPerSecond;
            CurrentBytesPerSecond = current < min ? min : current > max ? max : current;
            return CurrentBytesPerSecond;
        }
    }
}
