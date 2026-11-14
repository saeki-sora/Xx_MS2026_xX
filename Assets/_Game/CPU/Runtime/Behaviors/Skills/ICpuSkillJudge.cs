using MS2026.Skills;

namespace MS2026.Cpu
{
    /// <summary>スキルの判断の結果（今撃ったらどれだけ効くか）。</summary>
    public readonly struct CpuSkillPlan
    {
        /// <summary>今撃ったときの効き（0〜1）。</summary>
        public readonly float Value;

        /// <summary>撃ち始めてから握り続ける秒数（青の「握って飛距離を決める」など。0 = すぐ離してよい）。</summary>
        public readonly float HoldSeconds;

        public CpuSkillPlan(float value, float holdSeconds = 0f)
        {
            Value = value;
            HoldSeconds = holdSeconds;
        }
    }

    /// <summary>
    /// 1つのスキル（色）の使い方の判断。行動「スキル」（<see cref="CpuSkillBehavior"/>）が、持っているスキルに合う判断を探して使う。
    /// 新しいスキルを足したら、これを1つ書いて <see cref="CpuSkillJudges.Register"/> するだけ（合う判断が無いスキルは、
    /// 砲身の向きに敵が多いかで決める「ふつうの判断」になる）。
    /// </summary>
    public interface ICpuSkillJudge
    {
        /// <summary>このスキルの判断か。</summary>
        bool Handles(CharacterSkill skill);

        /// <summary>今撃ったらどれだけ効くか（目が頭に届けた様子 = 反応の遅れのある様子で決める）。</summary>
        CpuSkillPlan Evaluate(CpuContext context, PlayerSkillSlot slot, in CpuTurretInfo turret);

        /// <summary>スキルを撃っている間の握り（0 = 離す、1 = 握り続ける）。activeSeconds は撃ち始めてからの秒数。</summary>
        float ActiveGrip(CpuContext context, PlayerSkillSlot slot, float activeSeconds, in CpuSkillPlan plan);
    }
}
