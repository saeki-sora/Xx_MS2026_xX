namespace MS2026.Fortress
{
    /// <summary>
    /// ILaserTargetのうち、「誰が撃ったか」も受け取れるもの。LaserBeamVisualはこれを実装している対象には
    /// 砲台の情報も一緒に渡す（通常のILaserTargetは今まで通り互換のまま動く）。
    /// </summary>
    public interface IAttributedLaserTarget : ILaserTarget
    {
        void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning, LaserTurret attacker);
    }
}
