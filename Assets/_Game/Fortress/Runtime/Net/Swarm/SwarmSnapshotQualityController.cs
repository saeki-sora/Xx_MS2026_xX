namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 写真方式で、位置の細かさ(写真1枚の重さ)を、Clientの「写真がいつもよりどれだけ遅れて届いているか」で決める(2026-10-05。Unity API非依存・EditModeテスト対象)。
    ///
    /// ・遅れ(全Clientの中で一番大きい物)が HighLagMs を超えたら、送りきれていない(確実な届け方の送信枠が詰まっている)とみなし、
    ///   段階(Level)を1つ上げる(=位置を2倍粗くして写真を軽くする。StepUpIntervalSeconds に1回まで、MaxLevel まで)。
    /// ・遅れが LowLagMs 以下の状態が StepDownAfterSeconds 続いたら、段階を1つ戻す。その間は今の段階のまま。
    ///
    /// 最初は写真の枚数を減らしていたが、計測(2026-10-05、1台に4つ起動)で逆効果と分かった: 一斉投入の直後は敵が押し合いで不規則に動くので、
    /// 写真の間隔が延びると先読みが大きく外れる(投入後1〜2秒の瞬間移動が約1万→約11万)。枚数(間隔)は保ち、1枚を軽くする方式にした。
    /// </summary>
    public sealed class SwarmSnapshotQualityController
    {
        public int MaxLevel { get; set; } = 2;
        public float HighLagMs { get; set; } = 80f;
        public float LowLagMs { get; set; } = 40f;
        public float StepUpIntervalSeconds { get; set; } = 0.3f;
        public float StepDownAfterSeconds { get; set; } = 1f;

        /// <summary>今の段階(0=設定どおりの細かさ、1段ごとに2倍粗い)。</summary>
        public int Level { get; private set; }

        /// <summary>段階を上げた回数(累計、計測用)。</summary>
        public int StepUps { get; private set; }

        private float _sinceStepUp = float.MaxValue;
        private float _calmSeconds;

        /// <summary>最初の状態(段階0)に戻す。</summary>
        public void Reset()
        {
            Level = 0;
            _sinceStepUp = float.MaxValue;
            _calmSeconds = 0f;
        }

        /// <param name="deltaTime">前回からの経過秒。</param>
        /// <param name="worstLagMs">Clientが報告した遅れ(ミリ秒、一番大きい物)。報告が無ければ負の値。</param>
        public int Update(float deltaTime, float worstLagMs)
        {
            _sinceStepUp += deltaTime;
            if (worstLagMs > HighLagMs)
            {
                _calmSeconds = 0f;
                if (Level < MaxLevel && _sinceStepUp >= StepUpIntervalSeconds)
                {
                    Level++;
                    StepUps++;
                    _sinceStepUp = 0f;
                }
            }
            else if (worstLagMs <= LowLagMs)
            {
                _calmSeconds += deltaTime;
                if (Level > 0 && _calmSeconds >= StepDownAfterSeconds)
                {
                    Level--;
                    _calmSeconds = 0f;
                }
            }
            else
            {
                _calmSeconds = 0f;
            }

            if (Level > MaxLevel)
            {
                Level = MaxLevel < 0 ? 0 : MaxLevel;
            }

            return Level;
        }
    }
}
