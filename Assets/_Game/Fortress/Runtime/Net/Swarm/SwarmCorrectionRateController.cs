namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 群衆の位置補正の間隔(全員を1回ずつ補正するのにかける秒数)を、Clientが測ったズレに応じて自動で決める(Unity API非依存・EditModeテスト対象)。
    /// ・ズレが大きい(highError以上)ときは、すぐに最短の間隔(minCycle)にする(一斉投入直後の押し合いの混乱などを素早く収める)。
    /// ・ズレが小さい(lowError以下)ときは、ゆっくり普段の間隔(maxCycle)へ戻す(通信量を抑える)。
    /// ・その間は、ズレの大きさに応じて間の値にする。
    /// 縮めるのは即座、戻すのはゆっくり(relaxSecondsPerSecond)なので、ズレが一時的に下がっただけで間隔が暴れない。
    /// </summary>
    public sealed class SwarmCorrectionRateController
    {
        public float MinCycle { get; set; } = 0.15f;
        public float MaxCycle { get; set; } = 0.5f;
        public float HighError { get; set; } = 1f;
        public float LowError { get; set; } = 0.3f;

        /// <summary>間隔を普段へ戻す速さ(1秒あたりに伸ばす秒数)。</summary>
        public float RelaxSecondsPerSecond { get; set; } = 0.1f;

        /// <summary>今の間隔(秒)。</summary>
        public float CurrentCycle { get; private set; }

        public SwarmCorrectionRateController()
        {
            CurrentCycle = MaxCycle;
        }

        /// <summary>間隔を普段(MaxCycle)に戻す(設定を変えたときなど)。</summary>
        public void Reset()
        {
            CurrentCycle = MaxCycle;
        }

        /// <param name="deltaTime">前回からの経過秒。</param>
        /// <param name="reportedError">Clientが報告したズレ(複数いれば一番大きい物)。報告が無ければ負の値。</param>
        public float Update(float deltaTime, float reportedError)
        {
            var min = MinCycle < MaxCycle ? MinCycle : MaxCycle;
            var max = MaxCycle;

            var desired = max;
            if (reportedError >= HighError)
            {
                desired = min;
            }
            else if (reportedError > LowError)
            {
                var t = (reportedError - LowError) / (HighError - LowError);
                desired = max + (min - max) * t;
            }

            if (desired < CurrentCycle)
            {
                CurrentCycle = desired;
            }
            else
            {
                var next = CurrentCycle + RelaxSecondsPerSecond * deltaTime;
                CurrentCycle = next < desired ? next : desired;
            }

            if (CurrentCycle < min)
            {
                CurrentCycle = min;
            }
            else if (CurrentCycle > max)
            {
                CurrentCycle = max;
            }

            return CurrentCycle;
        }
    }
}
