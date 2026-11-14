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

        public float Score(CpuContext context)
        {
            var aim = Decide(context);
            var lead = Mathf.Max(0.1f, context.Skill.leadSeconds) + 0.1f;
            return aim.Found && (aim.OnTarget || aim.SecondsUntil <= lead) ? BallScore * Mathf.Clamp01(context.Skill.smashBallAttention) : 0f;
        }

        public void Enter(CpuContext context)
        {
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var aim = Decide(context);
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

        private static CpuBallAim Decide(CpuContext context)
        {
            var turret = new CpuTurretInfo(context.Turret);
            if (!turret.Valid || !turret.CanFire || !context.Eyes.HasSeen)
            {
                return default;
            }

            return CpuBallMath.Pick(context.Eyes.Perceived.Balls, turret.AimDegrees, turret.RotationSign, turret.IdleRotationSpeed);
        }
    }
}
