namespace MS2026.Cpu
{
    /// <summary>
    /// 行動「撃つ」: 自動で回る砲身が敵の多い向きに来るのを読んで握り、混んだ所をなめる（判断は <see cref="CpuShootMath"/>）。
    /// 見るのは目が頭に届けた様子（反応の遅れあり）。砲身の向きと熱は、人が自分の画面で見るのと同じく今の値。
    /// </summary>
    public sealed class CpuShootBehavior : ICpuBehavior
    {
        public const string BehaviorId = "shoot";

        public string Id => BehaviorId;

        /// <summary>最後の判断（頭の中の表示用）。</summary>
        public CpuShootDecision LastDecision { get; private set; }

        public float Score(CpuContext context)
        {
            LastDecision = Decide(context);
            return CpuShootMath.ShootScore(LastDecision);
        }

        public void Enter(CpuContext context)
        {
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            LastDecision = Decide(context);
            if (LastDecision.Fire)
            {
                context.Hands.Hold(LastDecision.Grip);
            }
            else
            {
                context.Hands.Release();
            }
        }

        public void Exit(CpuContext context)
        {
        }

        private static CpuShootDecision Decide(CpuContext context)
        {
            if (!context.Eyes.HasSeen)
            {
                return default;
            }

            var turret = new CpuTurretInfo(context.Turret);
            return CpuShootMath.Decide(context.Eyes.Perceived.Weights, turret, context.Skill);
        }
    }

    /// <summary>
    /// 行動「冷ます」: 熱が上がってきたら、敵の少ない向きの間に離して冷ます。危ないときは敵が多くても離す（冷めきるまで）。
    /// </summary>
    public sealed class CpuCoolDownBehavior : ICpuBehavior
    {
        public const string BehaviorId = "cool";

        private bool _panicking;

        public string Id => BehaviorId;

        public float Score(CpuContext context)
        {
            var turret = new CpuTurretInfo(context.Turret);
            if (!turret.Valid || turret.IsOverheated)
            {
                // オーバーヒート中は握っても撃てないので、何もしない（頭が手を離す）。
                _panicking = false;
                return 0f;
            }

            var score = CpuShootMath.CoolScore(turret.Heat01, _panicking, context.Skill);
            _panicking = score >= CpuShootMath.PanicScore;
            return score;
        }

        public void Enter(CpuContext context)
        {
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            context.Hands.Release();
        }

        public void Exit(CpuContext context)
        {
        }
    }
}
