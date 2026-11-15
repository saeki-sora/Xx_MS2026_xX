using System;

namespace MS2026.Fortress
{
    /// <summary>
    /// 砲台を 360 度回さず、扇形の中で往復させる決まり（<see cref="LaserTurret.Sweep"/>。ボス戦で使う。2026-11-15）。
    /// 角度は <see cref="LaserTurret.AimAngleDegrees"/> と同じ（0 度で真上、反時計回りが正）。
    /// </summary>
    [Serializable]
    public struct TurretSweep
    {
        /// <summary>往復するか（false なら今までどおり 360 度回る）。</summary>
        public bool enabled;

        /// <summary>扇形の真ん中の向き（度）。</summary>
        public float centerDegrees;

        /// <summary>真ん中から端までの角度（度）。30 なら ±30 度。</summary>
        public float halfAngleDegrees;

        /// <summary>ふだんの回る速さに掛ける倍率。</summary>
        public float speedMultiplier;

        public static TurretSweep Create(float centerDegrees, float halfAngleDegrees, float speedMultiplier = 1f) => new TurretSweep
        {
            enabled = true,
            centerDegrees = centerDegrees,
            halfAngleDegrees = halfAngleDegrees,
            speedMultiplier = speedMultiplier
        };
    }
}
