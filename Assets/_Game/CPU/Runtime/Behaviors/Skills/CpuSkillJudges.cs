using System.Collections.Generic;
using MS2026.Skills;
using MS2026.Fortress;
using MS2026.Skills.Blue;
using MS2026.Skills.Green;
using MS2026.Skills.Red;
using MS2026.Skills.Yellow;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 4色のスキルの判断（本体が登録する。差し込む側も <see cref="CpuPlugins.RegisterSkillJudge"/> で足せる）。
    /// 「どれだけ効くか」の数（full = いっぱいとみなす敵の数）は、おおまかな目安。段11 の調整で直す。
    /// </summary>
    internal static class CpuSkillJudges
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            CpuPlugins.RegisterSkillJudge(new RedLaserJudge());
            CpuPlugins.RegisterSkillJudge(new YellowArrowJudge());
            CpuPlugins.RegisterSkillJudge(new GreenGuardianJudge());
            CpuPlugins.RegisterSkillJudge(new BlueVortexJudge());
        }
    }

    /// <summary>合う判断が無いスキル: 砲身の向きに敵が多い時に撃つ。</summary>
    public sealed class CpuGenericSkillJudge : ICpuSkillJudge
    {
        public static readonly CpuGenericSkillJudge Instance = new CpuGenericSkillJudge();

        private const float Full = 15f;

        public bool Handles(CharacterSkill skill) => true;

        public CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret)
        {
            return new CpuSkillPlan(CpuSkillMath.Normalize(CpuSkillMath.LineValue(context.Eyes.Perceived.Weights, turret.AimDegrees), Full));
        }

        public float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan) => 0f;
    }

    /// <summary>赤「極太レーザー」: 根元から先へ広がる太いレーザーを、ゆっくり回りながら撃つ。広がりと回る分の扇に敵が多い時。</summary>
    public sealed class RedLaserJudge : ICpuSkillJudge
    {
        private const float Full = 45f;

        public bool Handles(CharacterSkill skill) => skill is RedLaserSkill;

        public CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret)
        {
            var skill = (RedLaserSkill)slot.Skill;
            var halfWidth = Mathf.Atan2(skill.shape.tipWidth * 0.5f, Mathf.Max(1f, skill.shape.spreadDistance)) * Mathf.Rad2Deg;
            var sweep = Mathf.Max(0f, skill.timing.fireSeconds) * turret.IdleRotationSpeed * Mathf.Max(0f, skill.timing.rotationSpeedScale);
            var value = CpuSkillMath.ConeValue(context.Eyes.Perceived.Weights, turret.AimDegrees, turret.RotationSign, halfWidth, sweep);
            return new CpuSkillPlan(CpuSkillMath.Normalize(value, Full));
        }

        public float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan)
        {
            // 「離したら終わる」設定なら、撃っている間は握り続ける。
            return slot.Skill is RedLaserSkill red && red.timing.endWhenGripReleased ? 1f : 0f;
        }
    }

    /// <summary>黄「リコシェアロー」: 砲身の向きへ矢を放ち、壁で跳ね返る。まっすぐ先に敵が多い時（跳ね返りの分は見込まない）。</summary>
    public sealed class YellowArrowJudge : ICpuSkillJudge
    {
        private const float Full = 15f;

        public bool Handles(CharacterSkill skill) => skill is YellowArrowSkill;

        public CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret)
        {
            return new CpuSkillPlan(CpuSkillMath.Normalize(CpuSkillMath.LineValue(context.Eyes.Perceived.Weights, turret.AimDegrees), Full));
        }

        public float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan) => 0f;
    }

    /// <summary>
    /// 緑「守護霊」: コアのまわりを葉で守り、コアを回復する（向きは関係ない）。コアのまわりに敵が多い時か、コアが減っている時。
    /// </summary>
    public sealed class GreenGuardianJudge : ICpuSkillJudge
    {
        private const float FullCrowd = 35f;

        public bool Handles(CharacterSkill skill) => skill is GreenMintSkill;

        public CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret)
        {
            var sight = context.Eyes.Perceived;
            var crowd = CpuSkillMath.Normalize(sight.NearCoreCount, FullCrowd);
            var hurt = Mathf.Clamp01((1f - sight.CoreHealth01) * 1.5f);
            return new CpuSkillPlan(Mathf.Max(crowd, hurt));
        }

        public float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan) => 0f;
    }

    /// <summary>
    /// 青「うがい薬の渦」: ボトルを投げ、落ちた所に渦を広げて敵を足止めする。落ちる場所のまわりに敵が多い時。
    /// 「握って飛距離を決める」設定なら、いくつかの距離を試して、いちばん多い距離に届くまで握ってから離す。
    /// </summary>
    public sealed class BlueVortexJudge : ICpuSkillJudge
    {
        private const float Full = 25f;
        private const int DistanceSamples = 6;

        private readonly List<Vector3> _landings = new List<Vector3>();

        public bool Handles(CharacterSkill skill) => skill is BlueVortexSkill;

        public CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret)
        {
            var skill = (BlueVortexSkill)slot.Skill;
            var laserTurret = context.Turret;
            if (laserTurret == null)
            {
                return default;
            }

            var sight = context.Eyes.Perceived;
            var radius = skill.area.radius * skill.AreaRadiusScale;
            var grip = skill.gripDistance;
            if (grip == null || !grip.Enabled)
            {
                return new CpuSkillPlan(CpuSkillMath.Normalize(CountAt(skill, laserTurret, sight, skill.target.distance, radius), Full));
            }

            var bestValue = -1f;
            var bestDistance = grip.minDistance;
            for (var i = 0; i < DistanceSamples; i++)
            {
                var distance = Mathf.Lerp(grip.minDistance, grip.maxDistance, i / (float)(DistanceSamples - 1));
                var value = CountAt(skill, laserTurret, sight, distance, radius);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestDistance = distance;
                }
            }

            var hold = CpuSkillMath.HoldSecondsFor(bestDistance, grip.minDistance, grip.maxDistance, grip.secondsToMax);
            return new CpuSkillPlan(CpuSkillMath.Normalize(bestValue, Full), hold);
        }

        public float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan)
        {
            // 握っている間に距離が伸び、離した瞬間に投げる設定のときだけ、決めた秒数まで握る。
            var skill = slot.Skill as BlueVortexSkill;
            return skill != null && skill.gripDistance != null && skill.gripDistance.Enabled && activeSeconds < plan.HoldSeconds ? 1f : 0f;
        }

        private float CountAt(BlueVortexSkill skill, LaserTurret turret, CpuSight sight, float distance, float radius)
        {
            var count = skill.PredictLandings(turret, _landings, distance);
            var sum = 0f;
            for (var i = 0; i < count; i++)
            {
                sum += sight.CountNear(_landings[i], radius);
            }

            return sum;
        }
    }
}
