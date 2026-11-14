using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>「撃つ」の判断の結果。</summary>
    public readonly struct CpuShootDecision
    {
        public readonly bool Fire;

        /// <summary>握る強さ（0〜1）。</summary>
        public readonly float Grip;

        /// <summary>今撃つ価値（0〜1。今の向きの敵の多さ）。</summary>
        public readonly float Value;

        public CpuShootDecision(bool fire, float grip, float value)
        {
            Fire = fire;
            Grip = grip;
            Value = value;
        }
    }

    /// <summary>
    /// 「撃つ」「冷ます」の判断（決まりだけ。テストできるように Unity のオブジェクトに触らない）。
    /// 砲身は自動で回り、撃っている間は遅くなる（強く握るほど遅い）。そこで:
    /// ・撃っていないとき: 回ってくる砲身が、先読みの角度の中に「敵が多い向き」を捉えたら握り始める
    /// ・撃っている間: 今の向きと次の向きに敵が残っている間は握り続ける（遅くなった砲身で混んだ所をなめる）
    /// ・握る強さ: 敵が多いほど強く。熱が上がってきたら弱める
    /// </summary>
    public static class CpuShootMath
    {
        // 「撃つ」の点（0.25〜1.0）。冷ます点と比べる。
        public const float ShootBaseScore = 0.25f;
        public const float ShootValueScore = 0.75f;

        // 「冷ます」の点。熱が危ないときは撃つより必ず高い。
        public const float PanicScore = 1.5f;
        public const float CoolMaxScore = 0.9f;

        // 握るときの最低の強さ（「握っている」の境目より少し上）。
        private const float MinFiringGrip = CpuHands.GrippingThreshold + 0.05f;

        public static CpuShootDecision Decide(float[] weights, in CpuTurretInfo turret, in CpuSkillValues skill)
        {
            if (!turret.Valid || !turret.CanFire || weights == null || weights.Length == 0)
            {
                return default;
            }

            var width = 360f / weights.Length;
            var here = CpuSectorMath.MaxAhead(weights, turret.AimDegrees, turret.RotationSign, 0f, width);
            var value = Mathf.Clamp01(here / Mathf.Max(1f, skill.denseCount));

            bool fire;
            if (turret.IsFiring)
            {
                fire = here >= Mathf.Max(0.01f, skill.keepThreshold);
            }
            else
            {
                var lead = Mathf.Max(width, turret.IdleRotationSpeed * Mathf.Max(0f, skill.leadSeconds));
                var ahead = CpuSectorMath.MaxAhead(weights, turret.AimDegrees, turret.RotationSign, 0f, lead);
                fire = ahead >= Mathf.Max(0.1f, skill.startThreshold);

                // 先読みで握り始めたときは、着く先の多さで強さを決める。
                value = Mathf.Max(value, Mathf.Clamp01(ahead / Mathf.Max(1f, skill.denseCount)));
            }

            if (!fire)
            {
                return new CpuShootDecision(false, 0f, value);
            }

            var grip = Mathf.Lerp(Mathf.Max(MinFiringGrip, skill.minGrip), 1f, value) * HeatTaper(turret.Heat01, skill);
            grip = Mathf.Max(MinFiringGrip, grip);
            if (turret.NeedsCharge && !turret.IsFiring)
            {
                grip = Mathf.Max(grip, turret.ChargeGrip);
            }

            return new CpuShootDecision(true, Mathf.Clamp01(grip), value);
        }

        /// <summary>熱が上がってきたら握りを弱める倍率（1 → 0.55）。</summary>
        public static float HeatTaper(float heat01, in CpuSkillValues skill)
        {
            var start = skill.heatCoolStart01;
            var panic = Mathf.Max(start + 0.01f, skill.heatPanic01);
            if (heat01 <= start)
            {
                return 1f;
            }

            return Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((heat01 - start) / (panic - start)));
        }

        /// <summary>「撃つ」の点（撃たないなら 0）。</summary>
        public static float ShootScore(in CpuShootDecision decision)
        {
            return decision.Fire ? ShootBaseScore + ShootValueScore * Mathf.Clamp01(decision.Value) : 0f;
        }

        /// <summary>
        /// 「冷ます」の点。panicking は「危ないから冷ましている最中」（冷めきるまで続ける）。
        /// 熱が冷まし始めの割合を超えると少しずつ点が上がり、敵が少ない向きでは撃つより高くなる。危ない割合を超えたら必ず冷ます。
        /// </summary>
        public static float CoolScore(float heat01, bool panicking, in CpuSkillValues skill)
        {
            var start = Mathf.Clamp01(skill.heatCoolStart01);
            if (heat01 >= skill.heatPanic01 || (panicking && heat01 > start * 0.8f))
            {
                return PanicScore;
            }

            if (heat01 <= start)
            {
                return 0f;
            }

            var panic = Mathf.Max(start + 0.01f, skill.heatPanic01);
            return CoolMaxScore * Mathf.Clamp01((heat01 - start) / (panic - start));
        }
    }
}
