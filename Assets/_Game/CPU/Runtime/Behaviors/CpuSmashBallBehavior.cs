using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>スマッシュボールの狙いの判断の結果。</summary>
    public readonly struct CpuBallAim
    {
        public readonly bool Found;

        /// <summary>今、砲身がボールに重なっているか。</summary>
        public readonly bool OnTarget;

        /// <summary>砲身がボールに着くまでの秒数（重なっていれば 0）。</summary>
        public readonly float SecondsUntil;

        public CpuBallAim(bool found, bool onTarget, float secondsUntil)
        {
            Found = found;
            OnTarget = onTarget;
            SecondsUntil = secondsUntil;
        }
    }

    /// <summary>スマッシュボールの狙い（決まりだけ）。</summary>
    public static class CpuBallMath
    {
        // ボールの幅に足す、狙いのゆとり（度）。
        private const float MarginDegrees = 1.5f;

        /// <summary>見えていて、ビームが途中で止まらずに届くボールのうち、砲身がいちばん早く着くもの。</summary>
        public static CpuBallAim Pick(IReadOnlyList<CpuBallSight> balls, float aimDegrees, float sign, float idleSpeed)
        {
            return Pick(balls, aimDegrees, sign, idleSpeed, out _);
        }

        /// <summary><see cref="Pick(IReadOnlyList{CpuBallSight},float,float,float)"/> と同じ。選んだボールを狙っているほかの人の数も返す。</summary>
        public static CpuBallAim Pick(IReadOnlyList<CpuBallSight> balls, float aimDegrees, float sign, float idleSpeed, out int contenders)
        {
            contenders = 0;
            var found = false;
            var bestOn = false;
            var bestTime = float.PositiveInfinity;
            if (balls == null)
            {
                return default;
            }

            foreach (var ball in balls)
            {
                if (!ball.InLine)
                {
                    continue;
                }

                var half = ball.HalfAngle + MarginDegrees;
                var delta = Mathf.DeltaAngle(aimDegrees, ball.Angle);
                if (Mathf.Abs(delta) <= half)
                {
                    contenders = ball.Contenders;
                    return new CpuBallAim(true, true, 0f);
                }

                if (Mathf.Abs(sign) < 0.5f || idleSpeed <= 0.01f)
                {
                    continue; // 回らない砲台は、今向いていないボールには届かない
                }

                var ahead = CpuSectorMath.Wrap360(sign > 0f ? ball.Angle - aimDegrees : aimDegrees - ball.Angle) - half;
                var time = Mathf.Max(0f, ahead) / idleSpeed;
                if (time < bestTime)
                {
                    found = true;
                    bestTime = time;
                    contenders = ball.Contenders;
                }
            }

            return found ? new CpuBallAim(true, bestOn, bestTime) : default;
        }
    }

    /// <summary>
    /// 行動「スマッシュボール」: 見えているボールに砲身が来るときに強く握って割る（割るとスキルが使えるようになる）。
    /// 狙う気持ちの強さは上手さの「スマッシュボールを狙う」。敵がとても多い向きでは、撃つ方が勝つことがある。
    /// </summary>
    public sealed class CpuSmashBallBehavior : ICpuBehavior
    {
        public const string BehaviorId = "smashball";

        /// <summary>狙う気持ちが 1 のときの点（撃つの最高 1.0 より少し高い）。</summary>
        public const float BallScore = 1.1f;

        public const string AimSeconds = "ボールを狙った秒";

        public string Id => BehaviorId;

        public const string YieldedCount = "ボールを譲った回";

        private bool _yielding;

        // 狙う気持ち = 上手さの「スマッシュボールを狙う」× 気持ち（稼ぐ気持ちで上がり、ほかの人がもう狙っていて協力の気持ちが強ければ譲る）。
        public float Score(CpuContext context)
        {
            var aim = Decide(context, out var contenders);
            var lead = Mathf.Max(0.1f, context.Skill.leadSeconds) + 0.1f;
            if (!aim.Found || !(aim.OnTarget || aim.SecondsUntil <= lead))
            {
                _yielding = false;
                return 0f;
            }

            var yielding = contenders > 0 && context.Mood.Coop > context.Mood.Greed;
            if (yielding && !_yielding)
            {
                context.Stats.Add(YieldedCount, 1f);
            }

            _yielding = yielding;
            return BallScore * Mathf.Clamp01(context.Skill.smashBallAttention) * CpuMoodMath.BallFactor(context.Mood, contenders);
        }

        public void Enter(CpuContext context)
        {
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var aim = Decide(context, out _);
            if (aim.Found && (aim.OnTarget || aim.SecondsUntil <= Mathf.Max(0f, context.Skill.leadSeconds)))
            {
                context.Hands.Hold(1f);
                context.Stats.Add(AimSeconds, deltaTime);
            }
            else
            {
                context.Hands.Release();
            }
        }

        public void Exit(CpuContext context)
        {
        }

        private static CpuBallAim Decide(CpuContext context, out int contenders)
        {
            contenders = 0;
            var turret = new CpuTurretInfo(context.Turret);
            if (!turret.Valid || !turret.CanFire || !context.Eyes.HasSeen)
            {
                return default;
            }

            return CpuBallMath.Pick(context.Eyes.Perceived.Balls, turret.AimDegrees, turret.RotationSign, turret.IdleRotationSpeed, out contenders);
        }
    }
}
