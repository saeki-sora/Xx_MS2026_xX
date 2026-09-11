using System;
using UnityEngine;

namespace MS2026.ShaderFX
{
    [Serializable]
    public abstract class EffectModule
    {
        public bool enabled = true;

        public abstract string Keyword { get; }

        public abstract void ApplyTo(Material material);

        // Screen-space (画面系) modules override this instead of / in addition to ApplyTo(Material).
        // EffectDirector aggregates these into a single ScreenFXSettings for ScreenFXFeature to read.
        public virtual void ApplyToScreen(ScreenFXSettings settings) { }

        public abstract int ComputeParameterHash();

        public abstract string GetSummary();
    }
}
