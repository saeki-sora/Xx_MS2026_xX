using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.ShaderFX
{
    // Shared by EffectProfile (applying its own modules list) and EffectDirector (applying a
    // merged base+overlay modules list — see PushOverlay/PopOverlay). Kept independent of any
    // single EffectProfile asset so both call sites compute hashes/apply materials identically.
    public static class EffectModuleUtility
    {
        public static int ComputeHash(IReadOnlyList<EffectModule> modules)
        {
            int hash = 17;
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null || !module.enabled) continue;
                hash = HashCode.Combine(hash, module.GetType(), module.ComputeParameterHash());
            }
            return hash;
        }

        public static void ApplyTo(IReadOnlyList<EffectModule> modules, Material material)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null) continue;

                if (module.enabled)
                {
                    module.ApplyTo(material);
                }
                else if (!string.IsNullOrEmpty(module.Keyword))
                {
                    material.DisableKeyword(module.Keyword);
                }
            }
        }
    }
}
