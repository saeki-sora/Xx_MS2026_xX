using System.Collections.Generic;
using UnityEngine;

namespace MS2026.ShaderFX
{
    [CreateAssetMenu(menuName = "ShaderFX/Effect Profile", fileName = "New Effect Profile")]
    public sealed class EffectProfile : ScriptableObject
    {
        [SerializeReference] public List<EffectModule> modules = new();

        public int ComputeHash() => EffectModuleUtility.ComputeHash(modules);

        public void ApplyTo(Material material) => EffectModuleUtility.ApplyTo(modules, material);

        // Runs in both Play Mode and Edit Mode (Scene view live preview).
        // Must use .Instance (not just check HasInstance) — if the Director was destroyed
        // (e.g. after stopping Play Mode) this is what lazily recreates it. Guarding on
        // HasInstance here would silently no-op forever once the Director is gone.
        private void OnValidate()
        {
            EffectDirector.Instance.RefreshUsers(this);
        }
    }
}
