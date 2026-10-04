# ShaderFX (`com.ms2026.shaderfx` v0.1.0) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-03**.
> Human guide: `Docs/Tools/ShaderFX_ガイド.html`. Package README: `Packages/com.ms2026.shaderfx/README.md` (detailed, but **its menu names are stale** — real ones in §5).
> Embedded package (in-repo, editable, user-authored). If you change it, update this file + the HTML guide in the same task.

## 0. Mental model
- Data-driven material effects. `EffectProfile` (SO) = stack of `[SerializeReference] EffectModule`s. `EffectTarget` (component on a Renderer object) points at a Profile. `EffectDirector` (auto-created singleton, also in Edit Mode as hidden object) builds **one cached Material per (original material × profile content hash)**, ref-counted, `enableInstancing = true` → SRP Batcher stays effective for thousands of targets.
- Modules write material properties + enable shader keywords. Works only if the renderer's material uses **`ShaderFX/Uber`** (3D, lit, MeshRenderer) or **`ShaderFX/Uber Sprite`** (2D, unlit, SpriteRenderer). Same property/keyword names in both; C# never distinguishes 2D/3D.
- Screen modules aggregate into one `ScreenFXSettings` rendered by `ScreenFXFeature` (Render Graph renderer feature, registered in `Assets/Settings/PC_Renderer.asset` and `Mobile_Renderer.asset`).
- **Project status:** not used by game code yet. Demo: `Assets/ShaderFX_Demo/{DemoProfile.asset, DemoMat.mat, ShaderFX_DemoCube.prefab}`; imported sample `Assets/Samples/MS2026 ShaderFX/0.1.0/Stress Test (…)/StressTestSpawner.cs`.

## 1. Layout / asmdefs
```
Packages/com.ms2026.shaderfx/
  Runtime/ (MS2026.ShaderFX.Runtime, ns MS2026.ShaderFX)
    EffectProfile, EffectModule, EffectModuleInfoAttribute, EffectModuleUtility, EffectTarget, EffectDirector,
    EffectGroup (project-editable Flags), ScreenFXFeature/Pass/Settings, ShaderFXVolume, ShaderFXDebugOverlay,
    ShaderFXQuality, ShaderFXEase(+ShaderFXEasing), ShaderIDs, Modules/*.cs (16)
    Timeline/ (MS2026.ShaderFX.Timeline: ShaderFXParameterTrack/Clip/Behaviour; needs com.unity.timeline — present via D-Drive dependency)
  Editor/ (MS2026.ShaderFX.Editor) EffectProfileEditor (+ Add Effect menu), EffectTargetEditor, EffectDirectorEditor,
    EffectPresetLibrary (21 presets), ProfileBrowserWindow, ShaderFXBenchmarkMenu, ShaderFXBuildSettings, ShaderVariantStripper, EditModeSaveSafety
  Shaders/ ShaderFXUber.shader ("ShaderFX/Uber"), ShaderFXUberSprite.shader ("ShaderFX/Uber Sprite"), ShaderFXScreen.shader ("Hidden/ShaderFX/Screen"), Include/ShaderFXCommon.hlsl
  Samples~/ PresetLibrary (8 profiles), StressTest (spawner menu)
```

## 2. Modules (name / category / keyword / fields with defaults)
Object (keyword-gated; sprite shader has all except Toon):
| Module | Keyword | Fields |
|---|---|---|
| RimLightModule リムライト | `_FX_RIM` | color (0.3,0.9,1), power 3, intensity 1.5 — sprite version uses silhouette edge via `fwidth` |
| DissolveModule ディゾルブ | `_FX_DISSOLVE` | amount 0, edgeWidth 0.08, edgeColor (1,0.55,0.05), noiseScale 12 (world-space noise) |
| HitFlashModule 被弾フラッシュ | `_FX_HITFLASH` | color white, amount 0 |
| EmissionControlModule 発光 | `_FX_EMISSION` | color white, intensity 1, pulseSpeed 0 |
| FireModule 発火 | `_FX_FIRE` | color (1,0.45,0.05), intensity 2, scrollSpeed 1.5, noiseScale 24 [1–64] |
| FrostModule 氷結 | `_FX_FROST` | color (0.65,0.85,1), amount 0.7, sparkleColor, sparkleThreshold 0.85 |
| HologramModule ホログラム | `_FX_HOLOGRAM` | color (0.2,0.9,1), fresnelPower 2.5, scanlineSpeed 4, scanlineDensity 60, flickerSpeed 6, flickerIntensity 0.15 |
| GlitchModule グリッチ | `_FX_GLITCH` | amount 0.5, blockSize 16, speed 12, rgbSplit 0.02 (needs textured surface to be visible) |
| UVScrollModule UVスクロール | `_FX_UVSCROLL` | color (1,0.7,0.1), direction (0,1), speed 1, patternScale 30 [1–64], sharpness 0.6 |
| ToonShadingModule トゥーン影 (3D only) | `_FX_TOON` | steps 2, shadeColor (0.55,0.55,0.65) |
Screen (keyword empty; `ApplyToScreen(ScreenFXSettings)`):
| GrayscaleModule 白黒化 | intensity 1 |
| PosterizeModule | levels 4 |
| PixelateModule | blockSize 8, restrictToGroup false, group Enemy (needs `SetGroupRenderingLayer`) |
| OutlineModule | color black, thickness 1.5, depthThreshold 0.05, normalThreshold 0.4 (needs DepthNormals pass; sprites/custom shaders excluded) |
| ShockwaveModule | center (0.5,0.5), progress 0 [0–1], strength 0.05, width 0.15 |
| ScreenFlashModule | color white, amount 0 [0–1] |
- Base `EffectModule`: `bool enabled = true`, abstract `Keyword`, `ApplyTo(Material)`, `ComputeParameterHash()`, `GetSummary()`, virtual `ApplyToScreen(ScreenFXSettings)`. `[EffectModuleInfo(displayName, category = "General") { Description }]` → auto-listed in `+ Add Effect` (reflection).
- Shader property names (`ShaderIDs`): `_BaseMap _BaseColor _RimColor _RimPower _RimIntensity _DissolveAmount _DissolveEdgeWidth _DissolveEdgeColor _DissolveNoiseScale _HitFlashColor _HitFlashAmount _FXEmissionColor _FXEmissionIntensity _FXEmissionPulseSpeed _FireColor _FireIntensity _FireScrollSpeed _FireNoiseScale _FrostColor _FrostAmount _FrostSparkleColor _FrostSparkleThreshold _Hologram{Color,FresnelPower,ScanlineSpeed,ScanlineDensity,FlickerSpeed,FlickerIntensity} _Glitch{Amount,BlockSize,Speed,RGBSplit} _UVScroll{Color,Direction,Speed,PatternScale,Sharpness} _ToonSteps _ToonShadeColor`. Only `_DissolveAmount` and `_HitFlashAmount` are in `UNITY_INSTANCING_BUFFER`.

## 3. Runtime API (ns `MS2026.ShaderFX`)
```csharp
// EffectTarget  [AddComponentMenu("ShaderFX/Effect Target")]; fields: profile, group (EffectGroup flags), targetRenderer (auto GetComponent<Renderer>), hidden originalMaterial
EffectProfile Profile {get;set;}  EffectGroup Group  EffectProfile EffectiveProfile  Renderer TargetRenderer  Material OriginalMaterial
void SetInstanceFloat(string|int prop, float v); void SetInstanceColor(string|int prop, Color c);
void SetInstanceDissolveAmount(float); void SetInstanceHitFlashAmount(float); void ClearInstanceOverrides();
Coroutine PlayFloat(string|int prop, float from, float to, float dur, ShaderFXEase e = Linear, Action onComplete = null);
Coroutine PlayColor(...); Coroutine PlayDissolve(from,to,dur,ease,cb); Coroutine PlayHitFlash(from,to,dur,ease,cb);
void StopTween(string|int prop); void StopAllTweens();   // dur<=0 → jump + callback; same prop re-play cancels previous

// EffectDirector.Instance (HasInstance), [AddComponentMenu("")]
Register/Unregister(EffectTarget)   PushOverlay(target, string layer, EffectProfile p, int priority = 0)   PopOverlay(target, layer)
Refresh(target)  RefreshUsers(profile)  ApplyToGroup(EffectGroup, profile)  RemoveFromGroup(EffectGroup)
SetGroupRenderingLayer(EffectGroup, int bit)  ApplyToTag(string tag, profile)  RemoveFromTag(tag)  NotifySpawned(GameObject)
RevertAllToOriginal()  ReapplyAll()  RescanScene()  static SyncEditorVisibility()
int CachedMaterialCount, RegisteredTargetCount; IReadOnlyDictionary<string,EffectProfile> TagMappings; ScreenFXSettings ScreenSettings
```
- Overlays merge **per module type by priority** (higher wins; base-missing types are added); same layer name replaces. `ShaderFXVolume` (`ShaderFX/Effect Volume`: `overlayProfile`, `priority`, `layerName`) pushes/pops overlays on trigger enter/exit (3D + 2D physics; needs a Rigidbody on one side; Reset sets isTrigger).
- `ApplyToGroup` = temporary overlay over group members; `ApplyToTag` works without EffectTarget and re-applies on additive scene loads; bad tag → warning only.
- Screen settings are rebuilt (`RebuildScreenSettings`) on register/unregister/overlay/refresh — **last processed target wins**. Changing a screen module's field at runtime requires `EffectDirector.Instance.RefreshUsers(profile)` (OnValidate does this in Editor). There is no tween API for screen params (Shockwave.progress, ScreenFlash.amount) — animate by mutating a runtime profile instance + `RefreshUsers` (verify perf) or extend.
- Self-healing `PruneStaleRegistrations` handles parents destroyed with `DestroyImmediate` (children's OnDisable not called).
- `ShaderFXQuality.ResolutionScaleOverride` (float?) / `ScreenEffectResolutionScale` (auto 0.5 on mobile or quality names containing "Mobile"/"Low").
- `ShaderFXDebugOverlay` (`ShaderFX/Debug Overlay`, `anchor`, `visible`) — opt-in, shows targets / cached materials / MPB overrides.
- Timeline: `ShaderFXParameterTrack` binds `EffectTarget`; clip `parameter` (Dissolve/HitFlash/Custom), `fromValue`, `toValue`, `curve`; uses instance-property path.

## 4. Editor behaviour
- Live preview in Edit Mode by swapping `sharedMaterial`; `EditModeSaveSafety` restores originals before scene/prefab save & recompile and reapplies after → files never reference temp materials. `originalMaterial` is captured once when the component is added.
- `EffectProfileEditor`: foldout per module with enable toggle, summary, ×; `+ Add Effect` menu = `<category>/<displayName>` + `プリセット/<オブジェクト系|画面系>/<name>` (from `EffectPresetLibrary`, 13 object + 8 screen). Edits call `RefreshUsers`.
- `EffectTargetEditor` warnings: 「Effect Profile が未設定です。…」, 「Renderer が見つかりません。…」.

## 5. Menus (actual)
- `Tools/シェーダーFX/プロファイルブラウザ` (search, Ping, Select, Refresh)
- `Tools/シェーダーFX/SRPバッチャー負荷テストを生成（1024個のキューブ）` (uses selected profile)
- Create: `ShaderFX/Effect Profile`, `ShaderFX/Build Settings (Variant Stripping)` (`enableVariantStripping`, default off)
- Add Component: `ShaderFX/Effect Target`, `ShaderFX/Effect Volume`, `ShaderFX/Debug Overlay`
- Sample menu (StressTest sample is imported in this project): `Tools/シェーダーFX/サンプル/全エフェクト負荷テストを生成（1024個）` — delete the generated `ShaderFX_StressTest` root afterwards

## 6. Extending
New module = one `[Serializable][EffectModuleInfo(...)] sealed class X : EffectModule`. For object modules, **add the keyword branch to both `ShaderFXUber.shader` and `ShaderFXUberSprite.shader`** (+ `#pragma shader_feature_local _FX_X` in each pass) — missing one silently does nothing. To batch a new per-instance prop, declare it in `UNITY_INSTANCING_BUFFER`. Edit `EffectGroup` freely (project-specific). New presets: `Editor/EffectPresetLibrary.cs`.

## 7. Interaction with this game
- **Swarm enemies (Fortress `SwarmSystem`) are GPU-instanced without Renderers → cannot be EffectTargets.** Use for core, turrets, destructibles, Actor-mode enemies, UI3D.
- Fortress `BillboardMaterialSwapper` backs off if another system (ShaderFX) swapped a SpriteRenderer's material → that sprite won't billboard. A combined billboard+ShaderFX sprite shader does not exist.
- Sprite shader is unlit, no ShadowCaster, no DepthNormals (no outline), no Light2D integration.
- `renderingLayerMask` bits used by Pixelate group restriction are shared with URP Light Layers.

## 8. Gotchas
1. Wrong shader on material → no effect, no error.
2. `amount`-style modules (Dissolve, HitFlash, ScreenFlash, Shockwave.progress) default to 0 = invisible by design.
3. Instance API only overrides values; module must be enabled in the profile.
4. Arbitrary `SetInstanceFloat` props break batching (1 draw per renderer) unless added to instancing buffer.
5. Multiple screen profiles → last wins.
