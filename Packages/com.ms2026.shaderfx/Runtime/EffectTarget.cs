using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.ShaderFX
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ShaderFX/Effect Target")]
    public sealed class EffectTarget : MonoBehaviour
    {
        [SerializeField] private EffectProfile profile;
        [SerializeField] private EffectGroup group = EffectGroup.None;
        [SerializeField] private Renderer targetRenderer;

        // Captured once (Reset, or lazily on first use) before EffectDirector ever swaps
        // sharedMaterial, so re-applies always start from the untouched template material
        // instead of stacking copies-of-copies.
        [SerializeField, HideInInspector] private Material originalMaterial;

        // Non-serialized fallback caches. Deliberately NOT written back into the SerializeFields
        // above: doing that from a lazy getter risks baking a stale/wrong reference into a Prefab
        // template if the getter happens to run at the wrong moment (e.g. while a fresh source
        // GameObject is being turned into a Prefab asset, before instances exist).
        private Renderer resolvedRenderer;
        private Material resolvedOriginalMaterial;

        private EffectProfile overrideProfile;

        public EffectProfile Profile
        {
            get => profile;
            set
            {
                if (profile == value) return;
                profile = value;
                if (isActiveAndEnabled) EffectDirector.Instance.Refresh(this);
            }
        }

        public EffectGroup Group => group;

        // What actually gets rendered: a group/tag override pushed via
        // EffectDirector.ApplyToGroup/ApplyToTag takes priority over the object's own Profile.
        public EffectProfile EffectiveProfile => overrideProfile != null ? overrideProfile : profile;

        public Renderer TargetRenderer
        {
            get
            {
                if (targetRenderer != null) return targetRenderer;
                if (resolvedRenderer == null) resolvedRenderer = GetComponent<Renderer>();
                return resolvedRenderer;
            }
        }

        public Material OriginalMaterial
        {
            get
            {
                if (originalMaterial != null) return originalMaterial;
                if (resolvedOriginalMaterial == null && TargetRenderer != null) resolvedOriginalMaterial = TargetRenderer.sharedMaterial;
                return resolvedOriginalMaterial;
            }
        }

        internal void SetOverrideProfile(EffectProfile value) => overrideProfile = value;

        private MaterialPropertyBlock propertyBlock;

        // Individual-difference parameter path (design doc 2.4-④), e.g. "this one enemy's
        // dissolve progress" while every other enemy stays on the shared cached Material.
        // Unlike the Profile/Group/Tag paths above, this does NOT create or switch to a
        // different cached Material — it overrides a single property on THIS renderer only via
        // MaterialPropertyBlock. A renderer with an active property block override is always
        // excluded from SRP Batcher for as long as the override is set.
        //
        // For _DissolveAmount / _HitFlashAmount specifically (see SetInstanceDissolveAmount /
        // SetInstanceHitFlashAmount below), that's not the whole story: ShaderFXUber.shader
        // declares both as GPU-instancing per-instance properties, and EffectDirector always
        // creates its cached Materials with enableInstancing = true. So many renderers sharing
        // one cached Material, each with a different MPB-set _DissolveAmount, still get merged
        // into a single GPU-instanced draw call instead of one draw call per renderer — SRP
        // Batcher is off, but GPU Instancing picks up the slack. For any OTHER property passed to
        // the generic overloads below, the shader almost certainly does not declare it as
        // instanced, so the override falls back to a genuine one-draw-call-per-renderer cost —
        // use those sparingly (a handful of actively-animating objects), not for every object in a
        // batch. For anything shared by a whole group, vary the Profile/Group instead.
        //
        // The relevant EffectModule (e.g. DissolveModule) must already be enabled on this
        // object's Profile; this only overrides the value, not whether the effect is on.
        public void SetInstanceFloat(string propertyName, float value) => SetInstanceFloat(Shader.PropertyToID(propertyName), value);

        public void SetInstanceFloat(int propertyId, float value)
        {
            var renderer = TargetRenderer;
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(propertyId, value);
            renderer.SetPropertyBlock(propertyBlock);
        }

        public void SetInstanceColor(string propertyName, Color value) => SetInstanceColor(Shader.PropertyToID(propertyName), value);

        public void SetInstanceColor(int propertyId, Color value)
        {
            var renderer = TargetRenderer;
            if (renderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(propertyId, value);
            renderer.SetPropertyBlock(propertyBlock);
        }

        // Convenience wrappers for the two parameters the design doc calls out by name
        // (individual dissolve progress, individual hit-flash amount).
        public void SetInstanceDissolveAmount(float value) => SetInstanceFloat(ShaderIDs.DissolveAmount, value);

        public void SetInstanceHitFlashAmount(float value) => SetInstanceFloat(ShaderIDs.HitFlashAmount, value);

        public void ClearInstanceOverrides()
        {
            var renderer = TargetRenderer;
            if (renderer != null) renderer.SetPropertyBlock(null);
            StopAllTweens();
        }

        // --- Runtime Tween API (design doc §5「ランタイムTween API」) ---
        //
        // A time-based value animation on top of the individual-difference path above — the
        // "8割は値の時間変化" case the design doc calls out (hit flash fade, dissolve-out on
        // death, etc). Deliberately built on SetInstanceFloat/SetInstanceColor rather than a new
        // mechanism: it inherits the same GPU Instancing behavior for _DissolveAmount/
        // _HitFlashAmount, and the same "use sparingly for custom properties" caveat for anything
        // else. Driven by a Coroutine — only objects actually mid-tween pay any per-frame cost,
        // keeping the package's "no Update by default" rule intact for the other 95% of objects
        // that are just sitting on a shared cached Material.
        private Dictionary<int, Coroutine> activeTweens;

        public Coroutine PlayFloat(string propertyName, float from, float to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
            => PlayFloat(Shader.PropertyToID(propertyName), from, to, duration, ease, onComplete);

        public Coroutine PlayFloat(int propertyId, float from, float to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
        {
            StopTween(propertyId);
            bool finishedSynchronously = false;
            var routine = StartCoroutine(TweenFloatRoutine(propertyId, from, to, duration, ease, onComplete, () => finishedSynchronously = true));
            // duration<=0 hits no `yield` at all, so Unity runs the whole coroutine body —
            // including its own activeTweens.Remove(propertyId) — synchronously inside
            // StartCoroutine, before this line even runs. Registering it below in that case would
            // just re-add an already-finished handle that nothing will ever remove again.
            if (!finishedSynchronously) (activeTweens ??= new Dictionary<int, Coroutine>())[propertyId] = routine;
            return routine;
        }

        public Coroutine PlayColor(string propertyName, Color from, Color to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
            => PlayColor(Shader.PropertyToID(propertyName), from, to, duration, ease, onComplete);

        public Coroutine PlayColor(int propertyId, Color from, Color to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
        {
            StopTween(propertyId);
            bool finishedSynchronously = false;
            var routine = StartCoroutine(TweenColorRoutine(propertyId, from, to, duration, ease, onComplete, () => finishedSynchronously = true));
            if (!finishedSynchronously) (activeTweens ??= new Dictionary<int, Coroutine>())[propertyId] = routine;
            return routine;
        }

        // Convenience wrappers matching the design doc's own example
        // (`target.Play("Dissolve", 0→1, 0.5f, Ease.OutQuad)`).
        public Coroutine PlayDissolve(float from, float to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
            => PlayFloat(ShaderIDs.DissolveAmount, from, to, duration, ease, onComplete);

        public Coroutine PlayHitFlash(float from, float to, float duration, ShaderFXEase ease = ShaderFXEase.Linear, Action onComplete = null)
            => PlayFloat(ShaderIDs.HitFlashAmount, from, to, duration, ease, onComplete);

        public void StopTween(string propertyName) => StopTween(Shader.PropertyToID(propertyName));

        public void StopTween(int propertyId)
        {
            if (activeTweens == null || !activeTweens.TryGetValue(propertyId, out var routine)) return;
            if (routine != null) StopCoroutine(routine);
            activeTweens.Remove(propertyId);
        }

        public void StopAllTweens()
        {
            if (activeTweens == null) return;
            foreach (var routine in activeTweens.Values)
            {
                if (routine != null) StopCoroutine(routine);
            }
            activeTweens.Clear();
        }

        private IEnumerator TweenFloatRoutine(int propertyId, float from, float to, float duration, ShaderFXEase ease, Action onComplete, Action markFinishedSynchronously)
        {
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = ShaderFXEasing.Evaluate(ease, elapsed / duration);
                    SetInstanceFloat(propertyId, Mathf.LerpUnclamped(from, to, t));
                    yield return null;
                }
            }
            SetInstanceFloat(propertyId, to);
            activeTweens?.Remove(propertyId);
            markFinishedSynchronously();
            onComplete?.Invoke();
        }

        private IEnumerator TweenColorRoutine(int propertyId, Color from, Color to, float duration, ShaderFXEase ease, Action onComplete, Action markFinishedSynchronously)
        {
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = ShaderFXEasing.Evaluate(ease, elapsed / duration);
                    SetInstanceColor(propertyId, Color.LerpUnclamped(from, to, t));
                    yield return null;
                }
            }
            SetInstanceColor(propertyId, to);
            activeTweens?.Remove(propertyId);
            markFinishedSynchronously();
            onComplete?.Invoke();
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            originalMaterial = targetRenderer != null ? targetRenderer.sharedMaterial : null;
        }

        // Runs in both Play Mode (real gameplay) and Edit Mode (Scene view live preview) —
        // EffectDirector itself decides what's safe to do in each (see its HideAndDontSave use).
        private void OnEnable()
        {
            EffectDirector.Instance.Register(this);
        }

        private void OnDisable()
        {
            // Unity already stops every running Coroutine on a disabled MonoBehaviour, but
            // activeTweens would otherwise keep holding onto those now-dead Coroutine handles.
            activeTweens?.Clear();
            if (EffectDirector.HasInstance) EffectDirector.Instance.Unregister(this);
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            EffectDirector.Instance.Refresh(this);
        }
    }
}
