namespace MS2026.Fortress
{
    /// <summary>
    /// レーザーが当たったときにダメージを受けるもの（敵・破壊可能な障害物など）。
    /// 何にどれだけダメージが入るか（倍率など）は受け取る側が決める。
    /// </summary>
    public interface ILaserTarget
    {
        void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning);
    }
}
