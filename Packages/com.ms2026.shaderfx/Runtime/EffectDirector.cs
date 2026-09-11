using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.ShaderFX
{
    // Auto-created, one per scene. Holds the shared material cache that keeps the
    // SRP Batcher happy: N objects with the same EffectProfile on the same source
    // material always resolve to the exact same Material instance.
    [AddComponentMenu("")]
    public sealed class EffectDirector : MonoBehaviour
    {
        private static EffectDirector instance;

        public static bool HasInstance => instance != null;

        public static EffectDirector Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindFirstObjectByType<EffectDirector>();
                if (instance == null)
                {
                    var go = new GameObject("EffectDirector");
                    instance = go.AddComponent<EffectDirector>();

                    // A fresh Director starts with an empty registry. Normally each EffectTarget
                    // registers itself via OnEnable, but that doesn't reliably re-fire for
                    // already-existing scene objects on every event that can destroy a Director
                    // (e.g. stopping Play Mode doesn't restart the object lifecycle the way a
                    // script recompile's domain reload does). Scanning once here — instead of
                    // relying purely on the push-based OnEnable path — is what makes a freshly
                    // (re)created Director actually reflect the scene it just appeared in.
                    instance.RescanScene();
                }
                SyncEditorVisibility();
                return instance;
            }
        }

        // Drops every tracked registration/cache entry and re-scans the current scene from
        // scratch. Also called (via an Editor-only hook) on every Play Mode enter/exit: the
        // Director's own GameObject can survive that transition while other scene objects get
        // fresh native instances underneath it (observed with a large, script-spawned object
        // count), which would otherwise leave `registered` full of stale/destroyed references
        // that Unity's fake-null check silently treats as "no-op" instead of "needs re-adding".
        public void RescanScene()
        {
            foreach (var material in materialCache.Values)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            materialCache.Clear();
            materialRefCounts.Clear();
            targetCacheKeys.Clear();
            externalCacheKeys.Clear();
            externalOriginals.Clear();
            registered.Clear();
            overlays.Clear();

            var existingTargets = FindObjectsByType<EffectTarget>(FindObjectsSortMode.None);
            foreach (var target in existingTargets)
            {
                if (target != null && target.isActiveAndEnabled) Register(target);
            }
        }

        // Edit Mode: hidden/unsaved, since it exists only to drive Scene view live preview.
        // Play Mode: normal, visible GameObject (matches pre-phase-4 behavior; also lets the
        // Editor's Enter Play Mode Options "don't reload scene" carry the same instance over
        // without leaving it stuck hidden). An Editor-only hook calls this on every play mode
        // transition; the Instance getter also re-syncs opportunistically on every access.
        public static void SyncEditorVisibility()
        {
            if (instance == null) return;
            instance.gameObject.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.HideAndDontSave;
        }

        private readonly Dictionary<int, Material> materialCache = new();
        private readonly Dictionary<int, int> materialRefCounts = new();
        private readonly Dictionary<EffectTarget, int> targetCacheKeys = new();
        private readonly Dictionary<Renderer, int> externalCacheKeys = new();
        private readonly HashSet<EffectTarget> registered = new();
        private readonly Dictionary<string, EffectProfile> tagMappings = new();
        private readonly Dictionary<Renderer, Material> externalOriginals = new();
        private readonly Dictionary<EffectGroup, uint> groupRenderingLayerBits = new();
        private readonly ScreenFXSettings screenSettings = new();
        private readonly Dictionary<EffectTarget, List<OverlayEntry>> overlays = new();

        private struct OverlayEntry
        {
            public string Layer;
            public int Priority;
            public EffectProfile Profile;
        }

        public int CachedMaterialCount => materialCache.Count;
        public int RegisteredTargetCount => registered.Count;
        public IReadOnlyDictionary<string, EffectProfile> TagMappings => tagMappings;

        // Diagnostic only (ShaderFXDebugOverlay, design doc §5「デバッグオーバーレイ」) — walks the
        // full registry, so this is deliberately not something anything else in the package calls
        // every frame. CachedMaterialCount is roughly "how many batches the shared-Material path
        // costs"; this is the count of renderers currently sitting outside that path because an
        // individual-difference MaterialPropertyBlock override is active on them (each one is its
        // own draw call unless GPU Instancing picks it up — see EffectTarget's SetInstanceX docs).
        public int CountRenderersWithPropertyBlockOverride()
        {
            int count = 0;
            foreach (var target in registered)
            {
                if (target == null) continue;
                var renderer = target.TargetRenderer;
                if (renderer != null && renderer.HasPropertyBlock()) count++;
            }
            return count;
        }

        // Aggregated 画面系 (screen-space) settings read by ScreenFXFeature every frame.
        public ScreenFXSettings ScreenSettings => screenSettings;

        private void Awake()
        {
            // Re-applies tag mappings when content streams in via an additive scene load.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Register(EffectTarget target)
        {
            if (target == null) return;
            registered.Add(target);
            Apply(target);
            RebuildScreenSettings();
        }

        public void Unregister(EffectTarget target)
        {
            registered.Remove(target);
            ReleaseTargetKey(target);
            overlays.Remove(target);
            RebuildScreenSettings();
        }

        // --- Overlay: layers a temporary Profile on top of a target's base Profile (its own
        // Profile, or a Group/Tag override — see EffectiveProfile), instead of replacing it
        // outright (design doc §5「エフェクトのブレンド/優先度」). For each concrete EffectModule
        // TYPE, the highest-priority overlay that declares that type wins; module types no
        // overlay touches keep coming from the base. This is how "常時トゥーン(RimLight) +
        // 一時的な被弾フラッシュ(HitFlash)" coexist — pushing a HitFlash-only overlay doesn't make
        // the RimLight disappear the way SetOverrideProfile-based Group/Tag application would.
        //
        // `layer` is a caller-chosen name ("HitFlash", "StatusEffect", ...): pushing the same
        // layer again replaces its profile/priority in place rather than stacking duplicates, so
        // repeated hits can just re-push the same layer with a fresh profile instance.
        public void PushOverlay(EffectTarget target, string layer, EffectProfile profile, int priority = 0)
        {
            if (target == null || string.IsNullOrEmpty(layer) || profile == null) return;

            if (!overlays.TryGetValue(target, out var list))
            {
                list = new List<OverlayEntry>();
                overlays[target] = list;
            }

            var entry = new OverlayEntry { Layer = layer, Priority = priority, Profile = profile };
            int existingIndex = list.FindIndex(e => e.Layer == layer);
            if (existingIndex >= 0) list[existingIndex] = entry;
            else list.Add(entry);

            Apply(target);
            RebuildScreenSettings();
        }

        public void PopOverlay(EffectTarget target, string layer)
        {
            if (target == null || string.IsNullOrEmpty(layer)) return;
            if (!overlays.TryGetValue(target, out var list)) return;
            if (list.RemoveAll(e => e.Layer == layer) == 0) return;

            if (list.Count == 0) overlays.Remove(target);
            Apply(target);
            RebuildScreenSettings();
        }

        // Base Profile's modules, then each active overlay (lowest priority first) overwriting —
        // or adding — its own module types on top. Order is deterministic for a given base +
        // overlay-set so the same combination always hashes/caches to the same Material.
        private List<EffectModule> BuildEffectiveModules(EffectTarget target)
        {
            var result = new List<EffectModule>();

            var baseProfile = target.EffectiveProfile;
            if (baseProfile != null) result.AddRange(baseProfile.modules);

            if (overlays.TryGetValue(target, out var list) && list.Count > 0)
            {
                var sorted = new List<OverlayEntry>(list);
                sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                foreach (var entry in sorted)
                {
                    if (entry.Profile == null) continue;
                    foreach (var module in entry.Profile.modules)
                    {
                        if (module == null) continue;
                        int existingIndex = result.FindIndex(m => m != null && m.GetType() == module.GetType());
                        if (existingIndex >= 0) result[existingIndex] = module;
                        else result.Add(module);
                    }
                }
            }

            return result;
        }

        public void Refresh(EffectTarget target)
        {
            if (target == null) return;
            registered.Add(target);
            Apply(target);
            RebuildScreenSettings();
        }

        // Called when an EffectProfile asset itself is edited (its own OnValidate),
        // since editing the asset does not raise OnValidate on the EffectTargets
        // that merely reference it.
        public void RefreshUsers(EffectProfile profile)
        {
            if (profile == null) return;

            foreach (var target in registered)
            {
                if (target != null && target.Profile == profile) Apply(target);
            }
            RebuildScreenSettings();
        }

        // --- EffectGroup: requires an EffectTarget component to declare membership ---

        public void ApplyToGroup(EffectGroup group, EffectProfile profile)
        {
            foreach (var target in registered)
            {
                if (target == null || (target.Group & group) == EffectGroup.None) continue;
                target.SetOverrideProfile(profile);
                Apply(target);
            }
            RebuildScreenSettings();
        }

        public void RemoveFromGroup(EffectGroup group)
        {
            foreach (var target in registered)
            {
                if (target == null || (target.Group & group) == EffectGroup.None) continue;
                target.SetOverrideProfile(null);
                Apply(target);
            }
            RebuildScreenSettings();
        }

        // Reserves a URP rendering-layer bit for a group so screen-space effects (e.g. a
        // PixelateModule with restrictToGroup) can render just that group into a separate
        // buffer ("敵だけドット化"). renderingLayerMask is shared with URP Light Layers, so
        // pick a bit that isn't already used for lighting.
        public void SetGroupRenderingLayer(EffectGroup group, int layerBitIndex)
        {
            uint bit = 1u << layerBitIndex;
            groupRenderingLayerBits[group] = bit;

            foreach (var target in registered)
            {
                if (target == null) continue;
                ApplyGroupRenderingLayerBits(target);
            }
            RebuildScreenSettings();
        }

        private void ApplyGroupRenderingLayerBits(EffectTarget target)
        {
            if (groupRenderingLayerBits.Count == 0) return;

            var renderer = target.TargetRenderer;
            if (renderer == null) return;

            foreach (var mapping in groupRenderingLayerBits)
            {
                if ((target.Group & mapping.Key) != EffectGroup.None) renderer.renderingLayerMask |= mapping.Value;
            }
        }

        // --- Tag: works on plain Renderers too, no EffectTarget component required ---

        public void ApplyToTag(string tag, EffectProfile profile)
        {
            if (string.IsNullOrEmpty(tag)) return;
            tagMappings[tag] = profile;
            ApplyTagNow(tag, profile);
            RebuildScreenSettings();
        }

        public void RemoveFromTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || !tagMappings.Remove(tag)) return;

            foreach (var target in registered)
            {
                if (target != null && target.CompareTag(tag))
                {
                    target.SetOverrideProfile(null);
                    Apply(target);
                }
            }

            foreach (var renderer in FindTaggedRenderersWithoutEffectTarget(tag))
            {
                RevertExternal(renderer);
            }
            RebuildScreenSettings();
        }

        // Call right after Instantiate() for zero-component (tag-only) objects so they pick up
        // a matching tag mapping immediately instead of waiting for the next scene load.
        // EffectTarget-based objects don't need this: OnEnable registers them automatically.
        public void NotifySpawned(GameObject spawned)
        {
            if (spawned == null || spawned.GetComponent<EffectTarget>() != null) return;

            var renderer = spawned.GetComponent<Renderer>();
            if (renderer == null) return;

            foreach (var mapping in tagMappings)
            {
                if (spawned.CompareTag(mapping.Key))
                {
                    ApplyExternal(renderer, mapping.Value);
                    return;
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var mapping in tagMappings) ApplyTagNow(mapping.Key, mapping.Value);
        }

        private void ApplyTagNow(string tag, EffectProfile profile)
        {
            foreach (var target in registered)
            {
                if (target != null && target.CompareTag(tag))
                {
                    target.SetOverrideProfile(profile);
                    Apply(target);
                }
            }

            foreach (var renderer in FindTaggedRenderersWithoutEffectTarget(tag))
            {
                ApplyExternal(renderer, profile);
            }
        }

        private static IEnumerable<Renderer> FindTaggedRenderersWithoutEffectTarget(string tag)
        {
            GameObject[] tagged;
            try { tagged = GameObject.FindGameObjectsWithTag(tag); }
            catch (UnityException e)
            {
                Debug.LogWarning($"[ShaderFX] Tag '{tag}' is not defined in Tags & Layers: {e.Message}");
                yield break;
            }

            foreach (var go in tagged)
            {
                if (go.GetComponent<EffectTarget>() != null) continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) yield return renderer;
            }
        }

        private void ApplyExternal(Renderer renderer, EffectProfile profile)
        {
            if (renderer == null || profile == null) return;

            if (!externalOriginals.TryGetValue(renderer, out var original) || original == null)
            {
                original = renderer.sharedMaterial;
                externalOriginals[renderer] = original;
            }
            if (original == null) return;

            if (externalCacheKeys.TryGetValue(renderer, out var oldKey)) ReleaseMaterialKey(oldKey);

            int newKey = AcquireMaterialKey(original, profile);
            renderer.sharedMaterial = materialCache[newKey];
            externalCacheKeys[renderer] = newKey;
        }

        private void RevertExternal(Renderer renderer)
        {
            if (renderer == null) return;
            if (externalOriginals.TryGetValue(renderer, out var original) && original != null)
            {
                renderer.sharedMaterial = original;
            }
            externalOriginals.Remove(renderer);

            if (externalCacheKeys.TryGetValue(renderer, out var key))
            {
                ReleaseMaterialKey(key);
                externalCacheKeys.Remove(renderer);
            }
        }

        // --- Edit Mode save safety: called by an Editor-only hook right before a scene/prefab
        // save writes to disk, and right after, so the on-disk file only ever references real,
        // persistent materials — never one of Director's runtime-generated preview variants.

        public void RevertAllToOriginal()
        {
            foreach (var target in registered)
            {
                if (target == null) continue;
                var renderer = target.TargetRenderer;
                var source = target.OriginalMaterial;
                if (renderer != null && source != null) renderer.sharedMaterial = source;
            }

            foreach (var mapping in externalOriginals)
            {
                if (mapping.Key != null && mapping.Value != null) mapping.Key.sharedMaterial = mapping.Value;
            }
        }

        public void ReapplyAll()
        {
            foreach (var target in registered)
            {
                if (target != null) Apply(target);
            }

            foreach (var mapping in tagMappings)
            {
                ApplyTagNow(mapping.Key, mapping.Value);
            }
        }

        // --- Core ---

        private void Apply(EffectTarget target)
        {
            var renderer = target.TargetRenderer;
            var source = target.OriginalMaterial;
            if (renderer == null || source == null) return;

            var effectiveModules = BuildEffectiveModules(target);

            ReleaseTargetKey(target);

            if (effectiveModules.Count > 0)
            {
                int key = AcquireMaterialKey(source, effectiveModules);
                renderer.sharedMaterial = materialCache[key];
                targetCacheKeys[target] = key;
            }
            else
            {
                renderer.sharedMaterial = source;
            }

            ApplyGroupRenderingLayerBits(target);
        }

        private void ReleaseTargetKey(EffectTarget target)
        {
            if (targetCacheKeys.TryGetValue(target, out var key))
            {
                ReleaseMaterialKey(key);
                targetCacheKeys.Remove(target);
            }
        }

        // A GameObject destroyed as part of its PARENT being destroyed never fires OnDisable on
        // its own components (only an object destroyed directly gets that individual-disable
        // step) — so EffectTarget.OnDisable's Unregister() call can't be relied on to fire for
        // every child in a deleted hierarchy. Left alone, that leaves permanently-dead entries in
        // `registered` and their cached Materials/ref counts never released. Every registration
        // change already walks the full set anyway (see RebuildScreenSettings below), so sweeping
        // stale entries there is effectively free and needs no extra hook — and unlike an
        // Editor-only hierarchyChanged callback, it works identically in real builds.
        private void PruneStaleRegistrations()
        {
            List<EffectTarget> stale = null;
            foreach (var target in registered)
            {
                if (target == null) (stale ??= new List<EffectTarget>()).Add(target);
            }
            if (stale == null) return;

            foreach (var target in stale)
            {
                ReleaseTargetKey(target);
                registered.Remove(target);
                overlays.Remove(target);
            }
        }

        // Re-scans every registered target's effective (base Profile + overlay-merged) modules
        // for 画面系 (screen-space) modules and rebuilds the single aggregated ScreenFXSettings
        // that ScreenFXFeature reads. Event-driven (called after any registration/group/tag/
        // overlay change), never per-frame.
        private void RebuildScreenSettings()
        {
            PruneStaleRegistrations();
            screenSettings.Reset();

            foreach (var target in registered)
            {
                if (target == null) continue;

                foreach (var module in BuildEffectiveModules(target))
                {
                    if (module != null && module.enabled) module.ApplyToScreen(screenSettings);
                }
            }

            screenSettings.pixelateRenderingLayerMask = 0;
            if (screenSettings.pixelateRestrictGroup != EffectGroup.None
                && groupRenderingLayerBits.TryGetValue(screenSettings.pixelateRestrictGroup, out var bit))
            {
                screenSettings.pixelateRenderingLayerMask = bit;
            }
        }

        // Used by ApplyExternal (tag-only, zero-component renderers) — those never go through
        // BuildEffectiveModules/overlays, so a plain single-Profile lookup is enough.
        private int AcquireMaterialKey(Material source, EffectProfile profile) => AcquireMaterialKey(source, (IReadOnlyList<EffectModule>)profile.modules);

        // Every acquire must be paired with a release (see ReleaseTargetKey / externalCacheKeys
        // handling above) — that's what lets a cached variant that nothing uses anymore get
        // freed immediately instead of only when the whole Director is torn down.
        private int AcquireMaterialKey(Material source, IReadOnlyList<EffectModule> effectiveModules)
        {
            int key = HashCode.Combine(source.GetInstanceID(), EffectModuleUtility.ComputeHash(effectiveModules));

            if (!materialCache.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(source) { name = $"{source.name} [ShaderFX {key:X8}]" };
                EffectModuleUtility.ApplyTo(effectiveModules, material);

                // Lets EffectTarget.SetInstanceFloat/SetInstanceColor overrides on _DissolveAmount /
                // _HitFlashAmount (declared as UNITY_INSTANCING_BUFFER properties in ShaderFXUber.shader)
                // get GPU-instanced across every Renderer sharing this cached Material, instead of each
                // MaterialPropertyBlock override forcing its own separate draw call. Harmless to enable
                // even for materials nothing ever calls SetInstanceX on.
                material.enableInstancing = true;

                materialCache[key] = material;
                materialRefCounts[key] = 0;
            }

            materialRefCounts[key] = materialRefCounts.GetValueOrDefault(key) + 1;
            return key;
        }

        private void ReleaseMaterialKey(int key)
        {
            if (!materialRefCounts.TryGetValue(key, out var count)) return;

            count--;
            if (count > 0)
            {
                materialRefCounts[key] = count;
                return;
            }

            materialRefCounts.Remove(key);
            if (materialCache.TryGetValue(key, out var material) && material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            materialCache.Remove(key);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            foreach (var material in materialCache.Values)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            materialCache.Clear();
            materialRefCounts.Clear();
            targetCacheKeys.Clear();
            externalCacheKeys.Clear();
        }
    }
}
