using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>【実験】救助の狙いの結果。</summary>
    public readonly struct CpuRescueAim
    {
        public readonly bool Found;
        public readonly int Player;
        public readonly bool Downed;
        public readonly bool OnTarget;
        public readonly float SecondsUntil;

        /// <summary>助けに行く気持ち（0〜1）。</summary>
        public readonly float Want;

        public CpuRescueAim(int player, bool downed, bool onTarget, float secondsUntil, float want)
        {
            Found = true;
            Player = player;
            Downed = downed;
            OnTarget = onTarget;
            SecondsUntil = secondsUntil;
            Want = want;
        }
    }

    /// <summary>【実験】救助の決まり（テストできるように Unity のオブジェクトに触らない）。</summary>
    public static class CpuRescueMath
    {
        public struct Tuning
        {
            public float ReviveAttention;
            public float HealAttention;
            public float HealBelowHealth;
            public float ShareWithHelpers;
            public float HitReach;

            /// <summary>設定と、分野「救助」の上手さの目盛り（0〜3）から作る。</summary>
            public static Tuning From(CpuRescueSettings s, float scale) => new Tuning
            {
                ReviveAttention = s.reviveAttention.Evaluate(scale),
                HealAttention = s.healAttention.Evaluate(scale),
                HealBelowHealth = s.healBelowHealth,
                ShareWithHelpers = s.shareWithHelpers.Evaluate(scale),
                HitReach = s.hitReach
            };
        }

        /// <summary>
        /// 助けに行く仲間を選ぶ（ビームが届く・倒れているか弱っている）。気持ち = 倒れていれば reviveAttention、
        /// 弱っていれば healAttention × 弱り具合。ほかの人がもう助けていれば shareWithHelpers 倍。いちばん気持ちが強く、早く砲身が着く仲間。
        /// </summary>
        public static CpuRescueAim Pick(IReadOnlyList<CpuTeammateSight> mates, float aimDegrees, float sign, float idleSpeed, in Tuning tuning)
        {
            var best = default(CpuRescueAim);
            var bestScore = 0f;
            if (mates == null)
            {
                return best;
            }

            foreach (var mate in mates)
            {
                if (!mate.InLine)
                {
                    continue;
                }

                float want;
                var downed = mate.Condition == CpuTeammateCondition.Downed;
                if (downed)
                {
                    want = tuning.ReviveAttention;
                }
                else if (mate.Health01 < tuning.HealBelowHealth)
                {
                    want = tuning.HealAttention * Mathf.Clamp01(1f - mate.Health01 / Mathf.Max(0.01f, tuning.HealBelowHealth));
                }
                else
                {
                    continue;
                }

                if (mate.Helpers > 0)
                {
                    want *= tuning.ShareWithHelpers;
                }

                if (want <= 0f)
                {
                    continue;
                }

                var half = mate.HalfAngle(tuning.HitReach);
                var delta = Mathf.DeltaAngle(aimDegrees, mate.Angle);
                var onTarget = Mathf.Abs(delta) <= half;
                float seconds;
                if (onTarget)
                {
                    seconds = 0f;
                }
                else if (Mathf.Abs(sign) < 0.5f || idleSpeed <= 0.01f)
                {
                    continue;
                }
                else
                {
                    var ahead = CpuSectorMath.Wrap360(sign > 0f ? mate.Angle - aimDegrees : aimDegrees - mate.Angle) - half;
                    seconds = Mathf.Max(0f, ahead) / idleSpeed;
                }

                // 早く着くほど少しだけ高く（同じくらいの気持ちなら、すぐ当てられる方）。
                var score = want / (1f + seconds * 0.2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new CpuRescueAim(mate.Player, downed, onTarget, seconds, want);
                }
            }

            return best;
        }
    }

    /// <summary>
    /// 【実験】行動「救助」: 倒れた仲間・弱っている仲間にビームを当てて助ける（味方のビームが上を通っている間、復活が進む・汚れが落ちる）。
    /// 誰も助けていない仲間ほど点が高い。倒れた仲間は普通の射撃（最高 1.0）より、弱っている仲間はふつうの射撃くらい。
    /// 記録「助けた秒」「洗った秒」。
    /// </summary>
    internal sealed class CpuReviveBehavior : ICpuBehavior
    {
        public const string BehaviorId = "revive";
        public const float ReviveScore = 1.15f;
        public const float HealScore = 0.9f;
        public const string ReviveSeconds = "助けた秒";
        public const string HealSeconds = "洗った秒";

        public string Id => BehaviorId;

        public float Score(CpuContext context)
        {
            var aim = Decide(context);
            if (!aim.Found || !(aim.OnTarget || aim.SecondsUntil <= Lead(context) + 0.2f))
            {
                return 0f;
            }

            // 助けに行く気持ち × 今の気持ち（協力の気持ちが強いほど、仲間を助ける）。
            return (aim.Downed ? ReviveScore : HealScore) * aim.Want * CpuMoodMath.RescueFactor(context.Mood);
        }

        public void Enter(CpuContext context)
        {
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var aim = Decide(context);
            if (aim.Found && (aim.OnTarget || aim.SecondsUntil <= Lead(context)))
            {
                // 当てている間は砲身が遅くなるよう強めに握る（長く当てられる）。
                context.Hands.Hold(0.8f);
                if (aim.OnTarget)
                {
                    context.Stats.Add(aim.Downed ? ReviveSeconds : HealSeconds, deltaTime);
                }
            }
            else
            {
                context.Hands.Release();
            }
        }

        public void Exit(CpuContext context)
        {
        }

        private static float Lead(CpuContext context) => Mathf.Max(0.1f, context.Skill.leadSeconds);

        private static CpuRescueAim Decide(CpuContext context)
        {
            var turret = new CpuTurretInfo(context.Turret);
            if (!turret.Valid || !turret.CanFire || !context.Eyes.HasSeen)
            {
                return default;
            }

            return CpuRescueMath.Pick(context.Eyes.Perceived.Teammates, turret.AimDegrees, turret.RotationSign, turret.IdleRotationSpeed,
                CpuRescueMath.Tuning.From(CpuRescueSettings.Load(), context.ScaleOf(CpuExperimentalInstaller.RescueAreaId)));
        }
    }

    /// <summary>
    /// 【実験】行動「抜け出す」: 自分が倒れたら、ほかの何より優先してにぎにぎで抜け出す（押すたびに復活が進む。長押しは1回）。
    /// 自分が倒れているかは本体の目が「仲間の調子」の口から知る（反応の遅れのある様子）。記録「倒れた回」「抜け出しの握り回」。
    /// </summary>
    internal sealed class CpuStruggleBehavior : ICpuBehavior
    {
        public const string BehaviorId = "struggle";
        public const float StruggleScore = 3f;
        public const string Downs = "倒れた回";
        public const string Presses = "抜け出しの握り回";

        private readonly CpuBrushRhythm _rhythm = new CpuBrushRhythm();
        private bool _wasPressed;

        public string Id => BehaviorId;

        public float Score(CpuContext context)
        {
            return context.Eyes.HasSeen && context.Eyes.Perceived.SelfCondition == CpuTeammateCondition.Downed ? StruggleScore : 0f;
        }

        public void Enter(CpuContext context)
        {
            _rhythm.Reset();
            _wasPressed = false;
            context.Stats.Add(Downs, 1f);
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var settings = CpuRescueSettings.Load();
            var rate = settings.strugglePressesPerSecond.Evaluate(context.ScaleOf(CpuExperimentalInstaller.RescueAreaId));
            var period = 1f / Mathf.Max(0.5f, rate);
            var pressed = _rhythm.Tick(context.Time, period, 0.5f);
            if (pressed && !_wasPressed)
            {
                context.Stats.Add(Presses, 1f);
            }

            _wasPressed = pressed;
            if (pressed)
            {
                context.Hands.Hold(0.9f);
            }
            else
            {
                context.Hands.Release();
            }
        }

        public void Exit(CpuContext context)
        {
            _rhythm.Reset();
        }
    }
}
