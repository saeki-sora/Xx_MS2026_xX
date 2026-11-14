using MS2026.Skills;

namespace MS2026.Cpu
{
    /// <summary>
    /// 行動「スキル」: スキルが「使える」になったら、すぐには撃たずに良い時を待つ（次の撃ち始めがスキルになる仕組みなので、
    /// 待っている間は普通の射撃もしない）。効きの見積もりは色ごとの判断（<see cref="ICpuSkillJudge"/>）。
    /// 待つ長さ・目安は上手さの「スキルの判断」（下手なほど、使えたらすぐ撃つ）。
    /// 撃っている間の握り方も判断に任せる（青の「握って飛距離を決める」など）。
    /// </summary>
    public sealed class CpuSkillBehavior : ICpuBehavior
    {
        public const string BehaviorId = "skill";

        /// <summary>使える間・撃っている間の点（撃つ 1.0・冷ます 0.9 より高く、危ない熱 1.5 より低い）。</summary>
        public const float ReadyScore = 1.25f;

        public const string UsedCount = "スキルを使った回";
        public const string WaitSeconds = "スキルを待った秒";
        public const string UsedValue = "スキルの効きの合計";

        private PlayerSkillSlot _slot;
        private int _slotPlayer = -1;
        private float _waited;
        private bool _pressing;
        private float _activeSeconds;
        private CpuSkillPlan _plan;
        private PlayerSkillState _lastState;

        public string Id => BehaviorId;

        /// <summary>最後に見積もった効き（頭の中の表示用）。</summary>
        public float LastValue => _plan.Value;

        public float Score(CpuContext context)
        {
            var slot = Slot(context);
            if (slot == null || slot.Skill == null)
            {
                return 0f;
            }

            var state = slot.State;
            if (state != PlayerSkillState.Ready && state != PlayerSkillState.Active)
            {
                return 0f;
            }

            // オーバーヒート中・歯みがきタイムなどで撃てないときは、待っていても仕方がない（ほかの行動に任せる）。
            var turret = new CpuTurretInfo(context.Turret);
            return state == PlayerSkillState.Active || turret.CanFire ? ReadyScore : 0f;
        }

        public void Enter(CpuContext context)
        {
            _waited = 0f;
            _pressing = false;
            _activeSeconds = 0f;
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var slot = Slot(context);
            if (slot == null)
            {
                context.Hands.Release();
                return;
            }

            var state = slot.State;
            var judge = CpuPlugins.FindSkillJudge(slot.Skill);
            var turret = new CpuTurretInfo(context.Turret);

            if (state == PlayerSkillState.Active)
            {
                if (_lastState != PlayerSkillState.Active)
                {
                    _activeSeconds = 0f;
                    context.Stats.Add(UsedCount, 1f);
                    context.Stats.Add(UsedValue, _plan.Value);
                }

                _activeSeconds += deltaTime;
                _pressing = false;
                var grip = judge.ActiveGrip(context, slot, _activeSeconds, _plan);
                if (grip > 0f)
                {
                    context.Hands.Hold(grip);
                }
                else
                {
                    context.Hands.Release();
                }
            }
            else if (state == PlayerSkillState.Ready)
            {
                // 1回の溜めで何回か撃てるスキル（青の「1本ずつ順番に」）は、撃ち終わると「使える」に戻る。待ち時間は数え直す。
                if (_lastState == PlayerSkillState.Active)
                {
                    _waited = 0f;
                }

                TickReady(context, slot, judge, turret, deltaTime);
            }
            else
            {
                context.Hands.Release();
            }

            _lastState = state;
        }

        public void Exit(CpuContext context)
        {
            _pressing = false;
            _lastState = PlayerSkillState.NoSkill;
        }

        private void TickReady(CpuContext context, PlayerSkillSlot slot, ICpuSkillJudge judge, in CpuTurretInfo turret, float deltaTime)
        {
            // 撃っている最中に使えるようになったときは、いったん離さないと撃てない。
            if (slot.IsWaitingForRelease)
            {
                _pressing = false;
                context.Hands.Release();
                return;
            }

            if (_pressing)
            {
                context.Hands.Hold(1f);
                return;
            }

            _waited += deltaTime;
            context.Stats.Add(WaitSeconds, deltaTime);
            _plan = context.Eyes.HasSeen ? judge.Evaluate(context, slot, turret) : default;
            var skill = context.Skill;
            if (turret.CanFire &&
                CpuSkillMath.ShouldFire(_plan.Value, _waited, skill.skillPatienceSeconds, skill.skillGoodValue, skill.skillMinValue))
            {
                _pressing = true;
                context.Hands.Hold(1f);
            }
            else
            {
                context.Hands.Release();
            }
        }

        private PlayerSkillSlot Slot(CpuContext context)
        {
            if (_slot == null || _slotPlayer != context.Player || (context.Turret != null && _slot.Turret != context.Turret))
            {
                _slot = PlayerSkillSlot.Find(context.Player);
                _slotPlayer = context.Player;
            }

            return _slot;
        }
    }
}
