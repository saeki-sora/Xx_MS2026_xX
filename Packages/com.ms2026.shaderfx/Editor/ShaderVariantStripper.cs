using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.ShaderFX.Editor
{
    // Strips ShaderFX/Uber variants for _FX_RIM/_FX_DISSOLVE/_FX_HITFLASH/_FX_EMISSION
    // combinations that no EffectProfile asset in the project actually uses. Opt-in via
    // ShaderFXBuildSettings (default off — a wrongly-stripped combination fails silently
    // at runtime as a missing effect rather than a build error, so stripping only turns on
    // when a team has explicitly accepted that tradeoff for their project).
    public sealed class ShaderVariantStripper : IPreprocessShaders
    {
        private static readonly string[] TrackedKeywords = { "_FX_RIM", "_FX_DISSOLVE", "_FX_HITFLASH", "_FX_EMISSION" };
        private const string TargetShaderName = "ShaderFX/Uber";

        private HashSet<int> usedMasksCache;
        private bool scanned;

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != TargetShaderName) return;
            if (!IsStrippingEnabled()) return;

            var usedMasks = GetUsedMasks();
            int strippedCount = 0;

            for (int i = data.Count - 1; i >= 0; i--)
            {
                int mask = ComputeMask(shader, data[i].shaderKeywordSet);
                if (usedMasks.Contains(mask)) continue;
                data.RemoveAt(i);
                strippedCount++;
            }

            if (strippedCount > 0)
            {
                Debug.Log($"[ShaderFX] Stripped {strippedCount} unused variant(s) from '{shader.name}' / {snippet.passName} " +
                          $"({usedMasks.Count} keyword combination(s) kept, sourced from EffectProfile assets in the project).");
            }
        }

        private static int ComputeMask(Shader shader, ShaderKeywordSet keywordSet)
        {
            int mask = 0;
            for (int i = 0; i < TrackedKeywords.Length; i++)
            {
                var keyword = new LocalKeyword(shader, TrackedKeywords[i]);
                if (keyword.isValid && keywordSet.IsEnabled(keyword)) mask |= 1 << i;
            }
            return mask;
        }

        private HashSet<int> GetUsedMasks()
        {
            if (scanned) return usedMasksCache;
            scanned = true;

            // 0 ("every ShaderFX keyword off") is always kept: an EffectProfile with no enabled
            // object-level modules is a legitimate, reachable configuration.
            usedMasksCache = new HashSet<int> { 0 };

            foreach (var guid in AssetDatabase.FindAssets("t:EffectProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<EffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null) continue;

                int mask = 0;
                foreach (var module in profile.modules)
                {
                    if (module == null || !module.enabled || string.IsNullOrEmpty(module.Keyword)) continue;
                    int index = Array.IndexOf(TrackedKeywords, module.Keyword);
                    if (index >= 0) mask |= 1 << index;
                }
                usedMasksCache.Add(mask);
            }

            return usedMasksCache;
        }

        private static bool IsStrippingEnabled()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ShaderFXBuildSettings"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<ShaderFXBuildSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null && settings.enableVariantStripping) return true;
            }
            return false;
        }
    }
}
