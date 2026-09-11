using System;

namespace MS2026.ShaderFX
{
    // Project-specific membership flags. Edit freely per project.
    [Flags]
    public enum EffectGroup
    {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        Stage = 1 << 2,
        Pickup = 1 << 3,
        UI3D = 1 << 4,
    }
}
