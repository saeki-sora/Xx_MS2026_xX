# D-Drive — AI Agent Reference (for Claude Code)

> Purpose: a cold-start brief so an agent can work with D-Drive in this repo **without re-reading the package**.
> Verified against source of `com.ddrive.core` **v1.2.1** (PackageCache folder `com.ddrive.core@54dde12a3be3`) on **2026-10-03**.
> If `Packages/manifest.json` pins a different `#vX.Y.Z`, treat API/paths below as "probably still true" and spot-check §3 source paths.
> Human-facing guide: `Docs/Tools/D-Drive_ガイド.html`. Package skill (may be overwritten by D-Drive's update window): `.claude/skills/ddrive-consumer/`.
> **Maintenance rule:** when D-Drive is updated, or when this project's D-Drive data/settings change (new IDs registered, paths, bootstrap settings, new call sites), update this file (esp. §1 registered IDs / call sites) and the HTML guide in the same task. Style rules: `Docs/Tools/DOCS_GUIDE.md`.
> Japanese strings in backticks are exact Unity UI labels — keep them verbatim when telling the user what to click.

---

## 0. Mental model (read this even if you read nothing else)

- D-Drive = "Designer-Driven Re: IDE Visual Environment". Unity 6000.3+, **URP only**, Addressables-based. Third-party package (author wrenchsun), installed via git URL — **read-only for us**.
- Code never references assets directly. Code passes a generated **ID constant** (`SEID.X`, `VFXID.X`…) to a **static facade** (`Audio.PlaySe`, `Vfx.Spawn`…). Designers fill the **Data** (`ScriptableObject`, `.asset`) behind that ID in dedicated editor windows.
- Resolution: `ID (ulong from asset GUID) → AssetCatalog → Addressables → Data → Manager spawns pooled Instance → returns Handle`.
- **Never throws.** Unregistered ID / facade not yet bound / dead handle → warning log + Placeholder (silent SE, magenta sphere, empty GameObject) or no-op. So "nothing happens" bugs are usually registration/boot-order problems, not code errors.
- Data is **read-only at runtime**. Per-instance mutable state lives in the Instance, reached via Handle.
- One composition root: `DDriveRuntimeBootstrap` (MonoBehaviour, exec order -1000) builds all managers and binds facades. Facades work only after it exists; IDs resolve correctly only after `IsReady` (catalogs registered, async in `Start`).

---

## 1. This project (Xx_MS2026_xX) — concrete facts

| Item | Value |
|---|---|
| manifest entry | `"com.ddrive.core": "git+https://github.com/wrenchsun/D-Drive.git?path=Packages/com.ddrive.core#v1.2.1"` |
| Package source on disk | `Library/PackageCache/com.ddrive.core@54dde12a3be3/` (hash changes on update; glob `Library/PackageCache/com.ddrive.core@*`) |
| Path settings | `ProjectSettings/DDriveProjectSettings.asset`: GameData=`Assets/_Game/DDrive/GameData`, Generated=`Assets/_Game/DDrive/Generated`, SourceAssets=`Assets/_Game/DDrive/SourceAssets`, Specs=`Assets/_Game/DDrive/Specs`, `_emitGeneratedAsmdef: 1` |
| Manual says `Assets/GameData`, `Assets/SourceAssets`, `Assets/Generated` | **read as** the `Assets/_Game/DDrive/...` paths above |
| Generated code | `Assets/_Game/DDrive/Generated/AssetIds.g.cs`, `Tuning.g.cs`, own asmdef **`DDrive.Generated`** (namespace `DDrive.Generated`) |
| Catalogs | `Assets/_Game/DDrive/GameData/Catalogs/*.asset` (13: Anchor, Anim, Audio, CameraFx, Canvas, Cutscene, Material, Model, Prefab, Presentation, Ui, UiTween, Vfx) |
| Other GameData | `Preload/Game_PreloadList.asset`, `Preload/SampleScene_PreloadList.asset`, `Settings/DDriveSpecSettings.asset`, `Settings/DDriveTuningTable.asset`, `Ui/UI_LayerSettings.asset` |
| Registered IDs (as of 2026-10-03) | **only `SEID.Se`** → `GameData/Audio/SE/SE_Se.asset`, DisplayName 「和太鼓でドン」, source `SourceAssets/Se/和太鼓でドン.mp3` (Japanese filename collapsed to fallback identifier `Se`). Mixer unassigned (→ Validation warning), Spatial=None, Flags.Load=Preload, Flags.Net=Local. All other ID classes are empty. |
| Bootstrap | In `Assets/Scenes/Game.unity`: `DDriveRuntimeBootstrap` with `DefaultNetBridge: Ngo(1)`, `DefaultNetStart: Manual(1)`, port 7777. Build scenes: `Game.unity`, `SampleScene.unity`. |
| NGO | Installed → `DDRIVE_NGO` define active, `NgoNetBridge` usable. |
| Game asmdef refs | `MS2026.Fortress.Runtime.asmdef` references `DDrive.Runtime`, `DDrive.Foundation`, `DDrive.Generated` (+ `Unity.Netcode.Runtime`). Other game asmdefs must add these refs to use D-Drive. |
| Existing D-Drive call sites | `Assets/_Game/Fortress/Runtime/Laser/LaserTurretAudio.cs` → `Audio.PlaySe(SEID.Se)` on `TurretState.Firing`.<br>`Assets/_Game/Fortress/Runtime/Camera/Feedback/CameraReaction.cs` holds `ShakeId ddriveShake` (file-local alias) + `UsesDDriveAsset`; `CameraReactionPlayer.cs` calls `CameraFx.Shake(id, scale)` when `CameraFx.IsBound`, else a built-in fallback shake.<br>`Assets/_Game/Fortress/Runtime/Net/FortressNetworkBootstrap.cs` → `HostGame/JoinGame/StopGame` delegate to `DDriveRuntimeBootstrap.StartHost(port)/StartClient(addr,port)/StopNetworking()`; handles NGO connection approval + PlayerIndex.<br>`Assets/_Game/Fortress/Editor/FortressDesignerWindow/Camera/CameraLivePanel.cs` shows `CameraFx.IsBound` status. |
| Unity MCP | `com.coplaydev.unity-mcp` is in manifest; no `.mcp.json` yet (see `Docs/Reference/MCP_Setup_from_DDrive.md`). Without MCP, Editor actions (create Data, run Validation, regenerate IDs) must be done by the user. |
| `CLAUDE.md` (repo root) | Tool-doc index + the rule to read/update `Docs/Tools/*` docs. Other project conventions come from the code. |

---

## 2. Asset types master table

`AssetType` enum (byte, append-only): `None, Se, Bgm, Vfx, Anim, Anim2D, Material, Texture, Canvas, Prefab, Presentation, Shake, Haptics, UiTween, Model, Anchor, AnchorGroup, ControlSkin, Cutscene`.

| Type | Data class | ID class (generated) | Marker (namespace) | Alias in package | Facade | File prefix | GameData subfolder | Catalog | SourceAssets folder / exts |
|---|---|---|---|---|---|---|---|---|---|
| Se | `SeData` | `SEID` | `SeMarker` (DDrive.Runtime.Audio) | `SeId` | `Audio` | `SE` | `Audio/SE` | AudioCatalog | `Se/` .wav .mp3 .ogg .aiff .aif |
| Bgm | `BgmData` | `BGMID` | `BgmMarker` (…Audio) | `BgmId` | `Audio` | `BGM` | `Audio/BGM` | AudioCatalog | `Bgm/` same as Se |
| Vfx | `VfxData` | `VFXID` | `VfxMarker` (…Vfx) | `VfxId` | `Vfx` | `VFX` | `Vfx` | VfxCatalog | `Vfx/` .prefab |
| Anim | `AnimData` | `ANIMID` | `AnimMarker` (…Anim) | `AnimId` | `Anim` | `ANIM` | `Anim` | AnimCatalog | `Anim/` .anim .fbx |
| Anim2D | `Anim2DData : AnimData` | `ANIM2DID` | `Anim2DMarker` (…Anim2D) | `Anim2DId` | `Anim2D` (ns DDrive.Runtime.Anim) | `ANIM2D` | `Anim2D` | AnimCatalog | `Anim2D/` .anim .fbx |
| Material | `MaterialData` | `MATID` | `MaterialMarker` (…Material) | `MaterialId` | `Mats` | `MAT` | `Material` | MaterialCatalog | — (Maya FBX import / converter) |
| Texture | `TextureData` | `TEXID` | `TextureMarker` | — | — | `TEX` | `Texture` | MaterialCatalog | `Texture/` .png .jpg .jpeg .tga .psd .tif .tiff .exr .bmp |
| Model | `ModelData` | `MODELID` | `ModelMarker` (…Model) | `ModelId` | `Models` | `MODEL` | `Model` | ModelCatalog | `Model/` .fbx |
| Prefab | `PrefabData` | `PREFABID` | `PrefabMarker` (…Prefab) | `PrefabAssetId` | `Prefabs` | `PREFAB` | `Prefab` | PrefabCatalog | `Prefab/` .prefab |
| Canvas | `CanvasData` | `CANVASID` | `CanvasMarker` (…Ui) | `CanvasId` | `Ui` | `CANVAS` | `Canvas` | CanvasCatalog | `Canvas/` .prefab |
| UiTween | `UiTweenData` | `UITWEENID` | `UiTweenMarker` (…Ui) | `UiTweenId` | `UiFx` | `UITWEEN` | `UiTween` | UiTweenCatalog | — |
| ControlSkin | `ButtonSkinData` / `SliderSkinData` (: `ControlSkinData`) | `SKINID` / `SLIDERSKINID` | `ControlSkinMarker` | — | `UiSkins` (resolver only) | `SKIN` | `Ui/Skin` | UiCatalog | — |
| Presentation | `PresentationData` | **`PRESENTID`** (not PRESID) | `PresentationMarker` (…Presentation) | `PresentationId` | `Presentation` | `PRES` | `Presentation` | PresentationCatalog | — |
| Shake | `CameraShakeData` | `SHAKEID` | `ShakeMarker` (**DDrive.Runtime.CameraShake**) | `ShakeId` | `CameraFx` | `SHAKE` | `Camera` | CameraFxCatalog | — |
| Haptics | `HapticsData` | `HAPTICID` | `HapticMarker` (…Haptics) | `HapticId` | `Haptics` | `HAPTIC` | `Haptics` | CameraFxCatalog | — |
| Anchor | `AnchorData` | `ANCHORID` | `AnchorMarker` (…Anchoring) | `AnchorId` | (arg to PlaySe/Spawn) | `ANC` | `Anchor` | AnchorCatalog | — |
| AnchorGroup | `AnchorGroupData` | `ANCHORGROUPID` | `AnchorGroupMarker` (…Anchoring) | `AnchorGroupId` | `Anchors` | `ANCG` | `AnchorGroup` | AnchorCatalog | — |
| Cutscene | `CutsceneData` | `CUTID` | `CutsceneMarker` (…Cutscene) | `CutsceneId` | `Cutscene` | `CUT` | `Cutscene` | CutsceneCatalog | `Cutscene/` .fbx (special naming, §9) |
| (Tuning) | `TuningTable` | `TUNING`, `TUNING_TABLE`, `TUNING_COLUMN` (Tuning.g.cs) | — | — | `Tuning` | — | `Settings/` | — | from spec sync |

**Aliases are file-local** (`using SeId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.SeMarker>;` inside package files). In game code either redeclare the alias (as `CameraReaction.cs` does) or just pass the generated constant (it is already `AssetId<Marker>`).

### ID mechanics
- `AssetId<TMarker>` (`DDrive.Foundation.Identity`): `ulong Value`, `AssetType Type`, `bool IsValid` (Value≠0), `static Invalid`. `[Serializable]`, non-readonly (Unity serializer). Inspector-assignable with a picker → good for `[SerializeField]` fields in game components.
- `Value` = `StableHashFromGuid(asset GUID)` → stable across rename/move. Duplicate IDs → error, regeneration aborts.
- **Constant name = asset *file name* minus known type prefix**, tokens split on `_`/`-`/space, non-identifier chars dropped, PascalCase-joined. **Category is part of the name**: `SE_Player_SwordSlash.asset` → `SEID.PlayerSwordSlash`. Collisions get `_XXXX` (hex of ID) suffix. Empty → `Unnamed`; non-letter start → `_` prefix. DisplayName is never used.
- Known prefixes stripped: SE BGM VFX ANIM ANIM2D MAT TEX CANVAS PREFAB PRES SHAKE HAPTIC HAPTICS UITWEEN MODEL ANC ANCG SKIN CUT.
- File name rule: `{PREFIX}_{lastCategorySegment}_{Identifier}` (category segment ASCII-alnum only; Japanese category → omitted). Folder: `{GameData}/{typeFolder}/{category path (ASCII-alnum segments)}`.
- Identifier: ASCII PascalCase from display/file name; if empty or digit-led → type fallback prepended (`Se`, `Bgm`, `Tex`…). ⇒ **Japanese-only source filenames all become `Se`/`Bgm`… and collide. Name source files in English.**
- Regenerated automatically on create/rename; manually: `Tools > D-Drive > Generate > Regenerate Asset IDs`. CI: `-executeMethod DDrive.Editor.CI.RegenerateIds`.

---

## 3. Source map (grep targets inside `Library/PackageCache/com.ddrive.core@*/`)

| Need | Path |
|---|---|
| Facades | `Runtime/Audio/Audio.cs`, `Runtime/Vfx/Vfx.cs`, `Runtime/Anim/Anim.cs`, `Runtime/Anim2D/Anim2D.cs`, `Runtime/Model/Models.cs`, `Runtime/Material/Mats.cs`, `Runtime/Prefab/Prefabs.cs`, `Runtime/Canvas/Ui.cs`, `Runtime/UiTween/UiFx.cs`, `Runtime/Ui/UiSkins.cs`, `Runtime/Camera/CameraFx.cs`, `Runtime/Haptics/Haptics.cs`, `Runtime/Presentation/Presentation.cs`, `Runtime/Cutscene/Cutscene.cs`, `Runtime/Anchoring/AnchorGroupPlayer.cs` (class `Anchors`), `Runtime/Tuning/Tuning.cs`, `Runtime/Loading/ScenePreload.cs`, `Runtime/Ui/OptionStore.cs` (class `Options`) |
| Data classes | `Runtime/<Type>/*Data.cs`; base `Foundation/Data/AssetDataBase.cs`, `AssetFlags.cs`, `AnchorDef.cs`, `AssetRef.cs`, `ParamValue.cs` |
| Presentation internals | `Runtime/Presentation/PresentationManager.cs` (FireXxx per TrackKind ~L1600), `PresentationTrack.cs`, `PlayContext.cs`, `PresentationHandle.cs` |
| Boot | `Runtime/Loop/DDriveRuntimeBootstrap.cs`, `GameLoopDriver.cs`; time/pause `Foundation/Pause/TimeService.cs`, `PauseService.cs` |
| Handle | `Foundation/Handle/Handle.cs`, `InstanceStore.cs` |
| Net | `Foundation/Net/INetBridge.cs`, `LocalLoopbackBridge.cs`; `Runtime/Net/NgoNetBridge.cs`, `NetLaunchArgs.cs`; `Runtime/Ngo/*` |
| Naming/creation | `Editor/AssetBrowser/AssetNamingService.cs`, `AssetCreationService.cs`, `AddressablesSync.cs` |
| Import rules | `Editor/Import/ImportRuleHandlers.cs`, `ImportRuleService.cs`; cutscene `Editor/Cutscene/CutsceneImportService.cs` |
| Codegen | `Editor/Codegen/AssetIdGenerator.cs`, `TuningCodegen.cs` |
| Validation | `Editor/Validation/*` (incl. `CI.cs`, `ForbiddenApiScanner.cs`, `CameraExecutionOrderValidator.cs`); per-type `Runtime/**/*Validator.cs`; interfaces `Foundation/Validation/` |
| Menus | `Editor/Menu/DDriveMenu.cs` (constants); grep `MenuItem(DDriveMenu.` |
| Settings | `Editor/Settings/DDriveProjectSettings.cs` |
| Official manuals (HTML) | `Documentation~/DesignerManual/*.html`, `Documentation~/ProgrammerManual/*.html`, `Documentation~/ConsumerGuide/`, `Documentation~/AGENTS_CONSUMER.md`, `CHANGELOG.md` |
| Sample | `Samples~/Demo/PresentationSkillSlashDemo.cs` |

---

## 4. Runtime API (all `public static`; namespaces from §2; generated IDs in `DDrive.Generated`)

All return `Handle<XMarker>` unless shown. Every facade also has `Bind(...)` (bootstrap-only, don't call) and most have `IsBound`.

**Audio** (`DDrive.Runtime.Audio`)
`PlaySe(SeId)`, `PlaySe(SeId, Vector3 pos)`, `PlaySe(SeId, Transform contextRoot)`, `PlaySe(SeId, AnchorId)`, `PlaySe(SeId, AnchorId, Transform)` · `Stop(h, float fade=0)` · `void PlayBgm(BgmId, float fadeIn=-1)` (−1 = use Data FadeIn; crossfades if something plays) · `void StopBgm(float fadeOut=-1)` · `void CrossFade(BgmId next, float duration)`. BGM returns no handle (2 internal channels). Intro plays only when starting from silence.

**Vfx** (`DDrive.Runtime.Vfx`)
`Spawn(VfxId)`, `Spawn(id, Vector3, Quaternion)`, `Spawn(id, Transform attach)`, `Spawn(id, AnchorId)`, `Spawn(id, AnchorId, Transform)` · `Stop(h)` (stop emission, wait `FadeOutSec`) · `Kill(h)` (immediate) · `Preload(params VfxId[])` · `IsPlaying/Move/Attach/Detach/SetSpeed` · `SetParam(h, label, ParamValue|float|Color)` (label = VfxData.Params label). Extension methods on handle: `h.Stop() h.Kill() h.Move() h.Attach() h.Detach() h.SetSpeed() h.SetParam() h.IsPlaying()`.

**CameraFx** (`DDrive.Runtime.CameraShake`) — `Shake(ShakeId)`, `Shake(id, Vector3 sourcePos)`, `Shake(id, float strengthScale)` · `Stop(h, fade=0.1)` · `SetStrength(h, s)` · `IsPlaying` · `StopAll(fadeOut=0.1)` · `SetGlobalScale(s)`. Inserts a shake node above `Camera.main`. Driven with **unscaled** dt (keeps shaking during HitStop).
**Haptics** (`DDrive.Runtime.Haptics`) — `Play(HapticId)`, `Play(id, float strengthScale)` · `Stop` · `IsPlaying` · `StopAll()` · `SetGlobalScale`.

**Anim** (`DDrive.Runtime.Anim`) — `Play(AnimId, Animator)`, `Play(id, Animator, float fade)` · `Stop(h, fade=0)` · `IsPlaying` · `NormalizedTime` · `SetSpeed` · `SetTrigger(Animator, string)` · `SetLayerWeight(Animator, int, float)`.
**Anim2D** (ns `DDrive.Runtime.Anim`) — `Play(Anim2DId, Animator)`, `Play(id, Animator, Vector2 dir)` · `PlayData(AnimData, Animator)` · `SetDirection(Animator, Vector2, "x","y")` / hash overload · `SetSpeed` · `FreezeAtFirstFrame(h)` · `Unfreeze(h, speed=1)` · `Stop` · `IsPlaying`. Returns `Handle<AnimMarker>`.
**Models** (`DDrive.Runtime.Model`) — `Spawn(ModelId, Vector3, Quaternion)`, `Spawn(id, Transform parent)` · `Despawn(h)` · `SetMaterial(h, slot, MaterialId)` · `SetLayer(h, layer)` · `GameObject GetGameObject(h)` · `PlayAnim(h, AnimId[, fade])`.
**Prefabs** (`DDrive.Runtime.Prefab`) — `Spawn(PrefabAssetId, Vector3, Quaternion)`, `Spawn(id, Transform parent)` · `Despawn(h)` · `Preload(...)` · `UniTask PreloadAsync(...)` · `GetGameObject` · `HasTag(h, tag)` · `Move(h, pos, rot)`. Extensions: `h.Go() h.GetComponent<T>() h.Move() h.HasTag() h.Despawn() h.IsValid()`.
**Mats** (`DDrive.Runtime.Material`) — `Material Get(MaterialId)` · `Apply(Renderer, int slot, MaterialId)` · `int Replace(from, to)` · `FadeTo(Renderer, [slot,] MaterialId to, float sec)` · `SetGlobalParam(string, ParamValue)` · `IsFading` · `Stop`.
**Ui** (`DDrive.Runtime.Ui`) — `Open(CanvasId)` · `Popup(CanvasId)` · `OpenAsync/PopupAsync` · `Close(h)` · `CloseAsync(h)` · `bool CloseTop()` · `CloseTopAsync()` · `IDisposable OnSignal(string key, Action<SignalArgs>)` · `SendSignal(key, from, elementPath=null)` · `SetLayerVisible(UiLayer, bool)` · `bool MoveFocus(Vector2)` · `GetGameObject` · `IsOpen`. `UiLayer`: HUD, Menu, Popup, Overlay, Loading.
**UiFx** (`DDrive.Runtime.Ui`) — `Play(UiTweenId, RectTransform)` · `PlayData(UiTweenData, RectTransform)` · `Play(UiPreset, RectTransform, in UiPresetRef p=default)` · one shortcut per preset (`FadeIn(t, sec)`, `PopIn`, `Shake(t, strength=8, sec=.4)`, `Pulse`, …, 59 presets) · raw: `MoveTo(t, Vector2, sec, Ease)`, `Scale(t, to, sec, Ease)`, `Fade(CanvasGroup, to, sec, Ease)`, `Rotate`, `MoveAlong(t, SplinePath, sec)` · `Stop(h, complete=false)` · `StopAll(t, complete)` · `IsPlaying` · `Progress` · `SetSpeed` · `UniTask WaitAsync(h)` · `TweenSequence Sequence()`.
**Anchors** (`DDrive.Runtime.Anchoring`) — `Play(AnchorGroupId[, Transform contextRoot])` · `Stop` · `Kill` · `IsPlaying`.
**Presentation** / **Cutscene** — §6, §7.
**Tuning** (`DDrive.Runtime.Tuning`) — `GetFloat/GetInt/GetBool/GetString/GetEnum(key, default)`, `GetTableFloat/Int/Bool/String(tableKey, rowId, columnKey, default)`. Unknown key / wrong type → default (+1 warning per key in dev). Keys as constants in `TUNING.*` after `Regenerate Tuning Keys`.
**Options** (`DDrive.Runtime.Ui`) — `float Get(OptionKey)` (1f if unbound), `Set(OptionKey, float)`, `event OnChanged`. Keys: MasterVolume, BgmVolume, SeVolume, VoiceVolume, ShakeScale, HapticScale, UiSpeedScale. Persisted in PlayerPrefs by bootstrap.
**ScenePreload** (`DDrive.Runtime.Loading`) — `UniTask RunAsync(ScenePreloadList, IProgress<float>=null)`, `Release(list)` (pair them).

---

## 5. Handles

- `Handle<TMarker>`: `readonly struct { int Index; int Generation; }`, `static Invalid = (-1,0)`, `==`/`!=`. **No `IsValid` member** on the generic struct (only Prefab extension `h.IsValid()`); test `h != Handle<T>.Invalid` or use `IsPlaying`.
- Generation bumps on every instance removal → stale handle after slot reuse is detected and becomes no-op.
- Operations (`Stop`, `Signal`…) on a dead handle: no exception, but **dev builds log a warning** + `InvalidAccessCount++`. Queries (`IsPlaying`, `WaitAsync`) on dead handles are silent. ⇒ prefer `if (Vfx.IsPlaying(h)) h.Stop();` in code that may hit finished handles often.
- Default `Handle` field value is `(0,0)`, not `Invalid`. Generations start at 1 so it never matches a live instance, but because Index 0 is in range, operations on it log the dev warning. Initialise stored fields to `Handle<T>.Invalid` (-1) to stay silent.
- `PresentationHandle`/`CutsceneHandle` are dedicated structs wrapping `Raw` with instance members (`h.Signal("hit")`, `h.Cancel()`, `h.OnCompleted`, …).
- Observables (R3) on unbound facades return `Observable.Empty<T>()`.

---

## 6. Presentation (flagship: SE/VFX/Anim/Shake/Haptic/HitStop/UI/Cutscene on one timeline)

```csharp
var ctx = new PlayContext { Self = transform, Target = enemy, Position = pos, OnSignal = key => {...} };
PresentationHandle h = Presentation.Play(PRESENTID.X, in ctx);   // `in` optional at call site
h.Signal("hit");   // fires Trigger=OnSignal tracks with SignalKey "hit" (each at most once)
h.Cancel();        // only if Data.Interruptible; else warning + no-op
await h.WaitAsync(ct);
```
- Static API: `Play, PlayData, Signal, Cancel, Pause, Resume, SetSpeed, Seek(time), IsPlaying, NormalizedTime (-1 if invalid), OnCompleted, OnCancelled, OnMarker (Observable<string>), OnTrackFired (Observable<PresentationTrack>), WaitAsync`.
- `PlayContext` (struct): `Transform Self`, `Transform Target`, `Vector3 Position` (used by Shake/Haptic as source pos, NOT for Vfx/Se placement), `Action<string> OnSignal`. Null Self/Target → only tracks needing them are skipped with a warning.
- `PresentationData`: `PresentationTrack[] Tracks`, `float TotalDuration` (0 = max AtTime; **OnSignal-only presentations with 0 end instantly**), `bool Interruptible=true`, `bool PredictLocal`, plus base `Flags`/`Events`.
- `PresentationTrack`: `TrackTrigger Trigger {AtTime, OnSignal}`, `float Time`, `string SignalKey`, `TrackKind Kind`, `AssetRef Asset {AssetType Type; ulong Id}` (`AssetRef.From(id)`), `TrackTargetMode Target {Self, ContextTarget, World, Anchor}`, `AnchorDef Anchor`, `ParamValue[] Params`, `bool StopOnCancel`, `PresentationEffectScope Scope {Everyone, ParticipantsOnly}` (HitStop/Shake/Haptic only).
- `TrackKind`: Anim, Anim2D, Se, Bgm, Vfx, CameraShake, Haptic, HitStop (Params[0]=seconds), Timeline (→ Cutscene, **implemented** even though DesignerManual says 未対応), Canvas, UiTween, Marker, Signal, AnchorGroup.
- Three channels: `handle.Signal(key)` code→data; `handle.OnMarker` data→code (Kind=Marker); `ctx.OnSignal` data→code (Kind=Signal). **AtTime 0 tracks fire synchronously inside `Play()`** → subscribe-after-play misses them; use `ctx.OnSignal` for t=0.
- Anchor compose: track Anchor only / asset Anchor only / both (track = parent, asset = child offset).
- Vfx track Params override VfxData.Params by index; Se volume override **not supported**.
- Net: `Flags.Net = Cosmetic` → auto-broadcast to all clients (default Local = own screen only). `PredictLocal` → actor plays immediately without waiting for host.
- Cycles Presentation→Cutscene→same Presentation = Validation Error.
- Choose Cutscene if Maya FBX camera/character motion or camera takeover is needed; Presentation otherwise; both → Presentation parent with a Timeline track.

## 7. Cutscene (Maya FBX → Timeline)
- `Cutscene.Play(CUTID.X, in ctx)` → `CutsceneHandle`; also `Cancel, Skip, Pause, Resume, SetSpeed, Seek, IsPlaying, NormalizedTime, OnCompleted, OnCancelled, OnMarker, WaitAsync`, `static bool IsInputLocked`, `static Observable<bool> OnInputLockChanged`, handle `IsInputLocked`.
- Import: drop into `SourceAssets/Cutscene/<Category>/`: `<Shot>.fbx` (camera+props, props named `PRP_<ModelId>`) and `<Shot>__<ModelIdentifier>.fbx` per character (double underscore; 2nd copy `_2`). Never use `@` in names. Units cm, Y-up. → `CUT_<Category>_<Shot>.asset` + TimelineAsset auto-created. `EVT_*` locators not imported (place D-Drive Signal markers manually in Timeline).
- `CutsceneData`: `Timeline`, `Bindings`, `Origin {Self, World, AnchorPoint}`, `OriginAnchorName`, `FrameRate`, `SourceFrameRange`, `Skip {Disabled, Immediate, ToMarker}`, `SkipToMarkerKey`, `Wrap`, `LockInput`, `PredictLocal`.
- **Camera contract**: `DDriveCutsceneCameraApplier` auto-attaches to `Camera.main`, `[DefaultExecutionOrder(1000)]`, writes in LateUpdate. Game camera code must write `Camera.main` in Update/FixedUpdate/LateUpdate with exec order **< 1000**, never in render callbacks / after LateUpdate, don't swap `Camera.main` mid-cutscene, and preferably not read back camera transform as its own state. Cinemachine Brain must be LateUpdate/SmartUpdate/FixedUpdate and < 1000. `CameraExecutionOrderValidator` warns on violations. Relevant for Fortress camera code (`Assets/_Game/Fortress/Runtime/Camera/`).
- `LockInput` only *signals*; game must disable input itself (subscribe `Cutscene.OnInputLockChanged`).

---

## 8. Data essentials (fields worth knowing; full lists in `Runtime/**/*Data.cs`)

**Common base `AssetDataBase`**: `ulong Id` (read-only), `DisplayName`, `Description`, `Category`, `Tags[]`, `Icon`, `Assignee`, `SpecUrl`, auto `Version/Author/UpdatedAt`, `ChangeNote`, `AssetFlags Flags`, `AssetEvent[] Events`, hidden `ImportSourceGuid`, `SchemaVersion`; `virtual IAssetBehaviour CreateBehaviour()`.
**`AssetFlags`**: `PauseMode Pause {PauseWithGame, IgnorePause, UIOnly}`, `LoadMode Load {LazyLoad(default 0), Preload, Streaming}`, `PoolPolicy Pool {Kind None|Pooled, InitialCount, MaxCount}`, `int Priority`, `bool Persistent`, `AssetDomain Domain {Game3D, UI, Both}`, `NetMode Net {Local, Cosmetic, Simulated}`.
**`AssetEvent`**: `EventTrigger Trigger {OnSpawn, OnEnable, OnLoop, OnDisable, OnDestroy, Frame, Time, Custom}`, `float Time` (frames or sec), `CustomKey`, `EventAction Action {PlayAsset, SetParam, SendMessage, Duck}`, `AssetRef Target`, `ParamValue Param`, `EventRepeat Repeat {EveryLoop, Once, KeepWhilePlaying}`. ⇒ "play SE on frame 12 of an anim", "VFX on prefab spawn" are pure data — no code wiring.
**`AnchorDef`** (embedded) / **`AnchorData`** (asset, adds `Parent`, `EulerJitter`, `ScaleRange`, `PositionJitterRadius`, `DelaySec`, `DelayJitterSec`, `SpawnChance=1`): `Space {World, BoneName, NamedObject, ContextTarget}`, `Path`, `LocalOffset`, `LocalEuler`, `LocalScale`, `FollowRotation`, `DetachOnStop`. Priority: code-passed `AnchorId` > Data `AnchorId` > Data embedded `Anchor`.
- **AnchorGroupData**: `OriginAnchorId`/`Origin`, `Layout {Manual, Grid, Circle, Line, Random}` with `GridCountX/Y/Z=3/1/3`, `GridSpacing`, `GridCentered`, `CircleCount=6`, `CircleRadius=1`, `CircleArc=360`, `CircleStartAngle`, `CircleFaceOutward`, `LineCount=3`, `LineLength=2`, `LineDirection`, `LineCentered`, `RandomCount=8`, `RandomRadius=1`, `RandomSeed`, `Points[]`, `DelayPerIndex`, `DelayJitterSec`, `ChancePerPoint=1`, `PositionJitterRadius`, `EulerJitter`, `ScaleRange`, `SharedVfx[]`, `SharedSe[]`, `Overrides[]`, `Children[]`.
- **SeData**: `Clips[]`, `StartOffsetSec`, `Volume=1` (0–1), `SelectMode {Random, RoundRobin, First}`, `Mixer`, `PitchRange`, `Loop`, `Spatial {None, Anchor, AtPosition}`, `AnchorId/Anchor`, `Min/MaxDistance (1/30)`, `Rolloff`, `Spread` (0–180), `DopplerEnabled`, `MaxConcurrent=8` (oldest stopped), `CooldownSec=0.03` (spam guard — rapid repeated PlaySe within 30 ms are dropped).
- **BgmData**: `Intro`, `LoopBody`, `LoopStartSec/EndSec`, `Mixer`, `Volume=1`, `FadeIn/FadeOut` (ValueDef), `Bpm`.
- **VfxData**: `Prefab` (ParticleSystem or VFX Graph), `AnchorId/Anchor`, `LifeMode {OneShot, Loop, Duration}`, `Duration`, `FadeOutSec`, `Render {World3D, UIOverlay}`, `RenderLayer`, `LightLayerMask`, `VfxParam[] Params`.
- **CameraShakeData**: `Pattern {PerlinNoise, DecaySine, Impulse, CustomCurve}`, `PosAmplitude`, `RotAmplitude`, `Frequency` (ValueDef), `Space {CameraLocal, World, FromSource}`, `TraumaWeight`, `MaxStack=3`.
- **HapticsData**: `LowFreq/HighFreq` (ValueDef), `Priority`, `LocalPlayerOnly=true`.
- **PrefabData**: `Prefab`, `Kind`, `GameplayTags[]`, `CollisionLayer=-1`, `Lod`. **ModelData**: `Prefab`, `Slots[]`, `DefaultAnimation`, `Avatar`, `RenderLayer`, `LightLayerMask`, `Lod`.
- **AnimData**: `Clip`, `StateName` (CrossFade target; empty → clip name), `Loop`, `Mask`, `Layer`, `DefaultCrossFade=0.1`, `Ik`, `BlendShapes`.
- **CanvasData**: `Prefab`, `Layer`, `SortOffset`, `Open/CloseTransition`, `CloseOnBack`, `ModalBlocksInput`, `PauseGameWhileOpen` (pushes PauseChannel.Gameplay), `Navigation`, `FirstSelected`, `Buttons` (wire → `UiAction {OpenCanvas, CloseSelf, CloseTop, SendSignal, PlayPresentation, SetOption}`), `Sliders`, `ElementEffects`.
- **MaterialData**: `Shader` (null → URP Lit), `Common` (Albedo/Normal/Mask/Emission/Blend…), `Specific[]`, `RenderQueueOffset`, `Anims[]`.
- **UiTweenData**: `TweenTrack[] Tracks` (props: AnchoredPosition, Scale, Rotation, Alpha, Color, FillAmount, SizeDelta, PathMove…), `TotalDuration`.
- `ValueDef` = unified curve+duration value (`ValueMode {Constant, Parametric, Curve}`, `TimeMode`, `LoopMode`); `ParamValue` typed variant (Float, Int, Bool, Color, Vector, Curve, Gradient, String, Object).

---

## 9. Boot, time, pause

- Place via `Tools > D-Drive > Generate > 起動オブジェクト(DDriveRuntimeBootstrap)をシーンに配置` → GameObject `[D-Drive] Runtime` (+ `GameLoopDriver`), auto-collects catalogs into `Catalogs[]`. One per scene (`DisallowMultipleComponent`; extras destroyed), `KeepAcrossScenes=true` → DontDestroyOnLoad.
- `Awake` (-1000): builds managers, binds facades (if `BindFacades`), resolves net bridge. `Start`: `RegisterCatalogsAsync()` → `IsReady`, `OnReady`, `WhenReady` (UniTask); NGO host/client start (Auto mode).
- Use `await DDriveRuntimeBootstrap.Instance.WhenReady;` before playing IDs at scene start (title BGM etc.); before that they resolve to Placeholder.
- Bootstrap fields: `Catalogs`, `CatalogLabel="DDriveCatalog"` (Addressables label for auto-collect), `LayerSettings`, `TuningTable`, `DefaultNetBridge {Loopback, Ngo}`, `DefaultNetStart {Auto, Manual}`, `DefaultHostAddress`, `DefaultPort`, `ShowNetDebugOverlay`. Accessors: `Loop, Registry, Pool, NetBridge, Audio, Bgm, Vfx, Anim, Models, Materials, Prefabs, Ui, UiTweens, CameraFx, Haptics, Presentation, Cutscene, Options, Groups`.
- `GameLoopDriver.Update`: `TimeService.Tick(Time.unscaledDeltaTime)` → `GameLoop.Tick(TimeService.ScaledDeltaTime(unscaled))`. Managers have no own Update.
  - ⇒ **`Time.timeScale = 0` does NOT pause D-Drive.** Pause with `DDriveRuntimeBootstrap.Instance.Loop.PauseService.Push(PauseChannel.Gameplay)` / `Pop` (stacked per channel `{Gameplay, UI, System}`, `OnPauseChanged` on edges). Assets react per `Flags.Pause`.
  - ⇒ **HitStop does NOT touch `Time.timeScale`.** It only scales D-Drive's dt (`TimeService.TimeScale`). Game logic using `Time.deltaTime` keeps running during a HitStop unless it reads `DDriveRuntimeBootstrap.Instance.Loop.TimeService.TimeScale`.
- Teardown (OnDestroy): Options.SaveIfDirty → StopAll → unbind facades.

## 10. Networking (NGO)
- Game code calls the same facades; managers route by `Flags.Net`: Local (own only), Cosmetic (visual; Host broadcasts to all; client requests are relayed via Host, rate-limited 60/s/client), Simulated (Host-authoritative networked Prefab spawn).
- Bridges: `LocalLoopbackBridge` (single player; IsServer=IsClient=true, LocalClientId=0) / `NgoNetBridge` (NetworkBehaviour; scene needs `NetworkManager` + `NgoNetBridge`).
- CLI: `-ddrive-net host|client|off|manual`, `-ddrive-host <ip>`, `-ddrive-port <n>`, `-ddrive-sim-latency <ms>` (dev), `-ddrive-autotest …`.
- Manual mode (this project: Ngo + Manual): game calls `StartHost(port)` (listens 0.0.0.0) / `StartClient(addr, port)` / `StopNetworking()`; returns false + warning if Loopback/missing NGO objects/shutdown still in progress (**can't restart in the same frame as Stop**). `NetManualConnectOverlay` = runtime OnGUI connect UI (bottom-left; created only in Manual role + Editor/Development Build). **This project hides it** (`FortressConnectUI.Start` sets its public `Visible = false`) because connecting from it sends no PlayerIndex payload and gets rejected; use the Fortress connect UI (top-right). `NetDebugOverlay` (dev builds, fixed `Rect(8,8,260,168)` top-left) stays visible; Fortress overlays stack below it via `DebugOverlayLayout` (reads `NetDebugOverlay.Visible`). Fortress Runtime asmdef references `DDrive.Runtime.Ngo` for these two types (public fields only; package untouched).
- On connect, catalog ContentHash + ProtocolVersion are compared; mismatch → warn (dev) / disconnect (release). All players need identical GameData and D-Drive wire version.
- Haptics `LocalPlayerOnly` uses `INetBridge.IsLocalPlayerObject`.

---

## 11. Editor workflow (exact menu labels)

Root `Tools/D-Drive/`:
- `Asset Browser` (entry point: search/filter/create/preview/delete/usage), `Presentation Editor`, `仕様書と同期` (web order-tool sync), `未使用アセット`, `マニュアルを開く`, `プログラマーマニュアルを開く`.
- `Editors/`: `Audio`, `VFX`, `Anchor`, `Anchor Group`, `Model`, `Prefab`, `Animation (3D)`, `Animation (2D)`, `Material`, `Material 変換`, `Material プレビュー`, `Canvas`, `UI Tween`, `UI Tween · Preset Gallery`, `Button Skin`, `Slider Skin`, `Slider`, `Shake / Haptics`, `共通確認用シーンを開く`, `Canvas確認用シーンを開く`, `Cutscene確認用シーンを開く`, `揺れ・振動確認用シーンを開く`.
- `Generate/`: `Regenerate Asset IDs`, `Regenerate Tuning Keys`, `Addressables 登録を同期(カタログ → グループ)`, `SourceAssets からインポートルールを再実行`, `SourceAssets/Cutscene からインポートルールを再実行`, `SourceAssets の既定フォルダを作成`, `起動オブジェクト(DDriveRuntimeBootstrap)をシーンに配置`, `Preload リストを再集計(現在のシーン)` / `(ビルド設定の全シーン)`, `標準プレハブを生成`, `デフォルトパーティクルマテリアルを生成`, `VFX UI レイヤー/カメラを設定`, `GameData をカテゴリ配置に整理`, `依存関係グラフを再構築`, `Anchor プレハブを生成`, `選択した Transform から Anchor を作成`, `選択した AnchorRig から Anchor を一括生成`, `選択したモデルから MaterialData を生成`, `選択した Material を D-Drive/Lit・Unlit の MaterialData に変換`, `Canvas + Panel と CanvasData を作成`.
- `Validation/Run All`; `Update/更新ウィンドウ`, `Update/マイグレーション(ドライラン)`, `Update/マイグレーション(適用)`; `Setup/セットアップウィザード`; `Debug/確認用プレビューの残骸を掃除`; `Build/実機確認用 Windows 開発ビルド` / `…リリース相当ビルド`; `Compat/スナップショットを更新`.
- `GameObject/D-Drive/`: `SeEmitter`, `AnchorRig`, `AnchorPoint(選択中の子に追加)`, `UiButton`, `UiSlider`, `Canvas + Panel(CanvasData も作成)`, `起動オブジェクト(DDriveRuntimeBootstrap)`, `標準プレハブ...`.
- Inspector of any D-Drive Data: top button `▶ 〜 Editor で開く` opens its dedicated editor; icon tools (自動生成 / シーンから作成…).
- Toolbar `？ マニュアル` button (enable via toolbar right-click → `D-Drive/Manual`).

**Creating assets** (agent cannot author `.asset` YAML by hand — forbidden):
1. Drop source file into `Assets/_Game/DDrive/SourceAssets/<TypeFolder>/<Category...>/<EnglishName>.<ext>` (type folder must be the **first level**, exact case) → Data + ID + catalog + Addressables auto-created. Or Asset Browser `新規` / drag-drop audio onto Asset Browser. Types without SourceAssets folders (Presentation, Shake, Haptics, UiTween, Anchor, AnchorGroup, Skin) → Asset Browser `新規` or the editor's `＋ 新規作成`.
2. Fill content in dedicated editor. 3. `Validation > Run All` → 0 Errors. 4. Save. 5. Use the constant.
- Example: `SourceAssets/Se/Turret/Fire.wav` → `GameData/Audio/SE/Turret/SE_Turret_Fire.asset` → `SEID.TurretFire`.
- Deleting source files does NOT delete Data (reference becomes missing → Validation).
- Standard prefabs / default particle material are created at hard-coded `Assets/GameData/...` in v1.2.1 (ignores the `_Game/DDrive` setting) — expect a stray `Assets/GameData` folder if those menu items are run.
- `SeEmitter` component (from `GameObject/D-Drive/SeEmitter`) = ambient looping SE in scene, no code (SE must be Loop + Spatial=Anchor).

---

## 12. Validation & CI
- `Tools > D-Drive > Validation > Run All` = all `IValidator`s + project checks (catalog registration, Addressables group/address match, LoadMode, schema version, spec diff, camera execution order, forbidden API scan of the package, URP setup). Per-asset "検証" panel at the bottom of every dedicated editor.
- Severity: **Error** = will break/Placeholder (must fix), **Warning** = review (new ones can appear after updates; Errors never added by an update — two-step rule), **Info**.
- `修正` button auto-fixes many (catalog/Addressables/Address/Preload), undoable.
- Common: `カタログ未登録`, `Addressables 未登録`, `Address 不一致` → 修正 or `Generate > Addressables 登録を同期`; `Flags.Load が Preload ではありません` (Canvas etc. resolved synchronously need Preload); Mixer unassigned (SE/BGM warning); legacy shaders (Default-Particle = Warning, Standard = Error under URP).
- CI: `Unity -batchmode -executeMethod DDrive.Editor.CI.ValidateAll` (fails on Error); `DDrive.Editor.CI.RegenerateIds` then `git diff --exit-code`.

## 13. Rules (from AGENTS_CONSUMER.md + source)
1. Never text-edit `.unity/.prefab/.asset`/images/audio; never create/delete `.meta` or change GUIDs. Go through Unity Editor (user or MCP).
2. Never edit generated `AssetIds.g.cs` / `Tuning.g.cs`.
3. Never modify the package (`Library/PackageCache/...`); extend via extension points (§14) or propose upstream.
4. Don't touch `DDrive.*` asmdefs; don't use the `DDrive.*` namespace in project code.
5. For D-Drive-managed assets don't use `Instantiate`, `Resources.Load`, `Addressables.Load*`, `AudioSource.Play` directly — use facades. (`ForbiddenApiScanner` only scans the package, not game code, so nothing will catch you — self-enforce for D-Drive assets only; unrelated game objects may use normal Unity APIs.)
6. Data is read-only at runtime. Editor tools that modify Data: `Undo.RecordObject` + `EditorUtility.SetDirty`.
7. Game code references only `DDrive.Runtime` / `DDrive.Foundation` / `DDrive.Generated` public types, never `DDrive.Editor` (except in Editor asmdefs).
8. Don't move/rename Data by hand or use `[CreateAssetMenu]`/`Assets/D-Drive/Data を作成` for normal creation; the Asset Browser handles naming + catalog + Addressables.
9. Package internals follow "no LINQ/closures/boxing in Tick/Spawn/Play" — mirror in hot game paths that call facades.

## 14. Extension points (implement in project asmdefs)
- `IValidator { AssetType Target; IEnumerable<ValidationResult> Validate(AssetDataBase, ValidationContext); }` — auto-discovered; also `IUniversalValidator`.
- `IImportRuleHandler` (Editor) — new SourceAssets type folder → Data (`TypeFolder, Target, DataType, Extensions, IdentifierFallback, LoadSource, Configure`).
- `IAssetBehaviour { OnSpawn/OnTick/OnDespawn(InstanceContext…) }` via overriding `AssetDataBase.CreateBehaviour()` in a derived Data.
- `IHapticOutput`, `INetBridge`, `[DataEditor(typeof(MyData), label, openMethod)]` for custom editor buttons.

## 15. Update procedure (summary)
`Tools > D-Drive > Update > 更新ウィンドウ`: 1 「最新の版を確認」 → 「manifest を選んだ版に更新する」 (rewrites `#vX.Y.Z`) → wait for compile (errors ⇒ read `Documentation~/migrations/`) → 2 CHANGELOG diff → 3 migration preview → 4 「更新を適用」 (migrate → regen IDs/Tuning → Addressables sync → Run All → record version; stops at first failure) → Run All → commit manifest, lock, changed `.asset`, generated code. Rollback: 「前の参照に戻す」 before applying, else `git revert`. After update, refresh §1/§3 of this doc (hash path changes) and the `ddrive-consumer` skill may be overwritten.

## 16. Gotchas checklist (most valuable section)
1. Nothing plays → check in order: bootstrap in scene? `IsReady` yet? ID constant exists (regen)? Validation errors (catalog/Addressables)? Data content assigned (Clip/Prefab)? Mixer/volume? `CooldownSec`/`MaxConcurrent` swallowing calls?
2. Constant names include category and come from the **file name**; renaming/moving Data changes the constant name (ID value unchanged) → compile errors in code using the old name.
3. Japanese source filenames → identifier fallback (`Se`, `Bgm`, …) → collisions → `_XXXX`-suffixed constants. Use English filenames.
4. Generated ID class for presentations is `PRESENTID` (package skill example says `PRESID` — wrong).
5. `Time.timeScale` neither pauses D-Drive nor is changed by HitStop (§9).
6. OnSignal-only Presentation needs `TotalDuration > 0`.
7. `Interruptible=false` → `Cancel()` ignored.
8. Presentation/Cutscene default `Flags.Net=Local` → not visible to other players until set to `Cosmetic`.
9. t=0 Presentation tracks fire inside `Play()`; use `ctx.OnSignal`.
10. Canvas (and anything resolved synchronously, e.g. `Ui.Open`) must be `Flags.Load=Preload`.
11. Stored Handle fields default to `(0,0)` → harmless but warning-spamming on Stop; initialise to `Invalid`.
12. Camera scripts writing `Camera.main` must run with exec order < 1000 (Cutscene contract).
13. `StopNetworking()` then `StartHost/StartClient` in the same frame fails (NGO async shutdown).
14. Docs in `Documentation~/DesignerManual` are partly stale: Timeline track is implemented; paths say `Assets/GameData` (here `_Game/DDrive`).
15. `DDriveProjectSettings` paths are respected by Asset Browser/import/codegen, but standard prefabs & default particle material still go to `Assets/GameData` (hard-coded const).

## 17. Minimal code templates
```csharp
using DDrive.Generated;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Vfx;
using DDrive.Runtime.Presentation;
using UnityEngine;

public sealed class Example : MonoBehaviour
{
    // Inspector-pickable ID (alternative to hard-coded constants)
    [SerializeField] private DDrive.Foundation.Identity.AssetId<VfxMarker> trailVfx;
    private Handle<VfxMarker> _trail = Handle<VfxMarker>.Invalid;
    private PresentationHandle _attack = PresentationHandle.Invalid;

    void Fire()
    {
        Audio.PlaySe(SEID.Se, transform.position);
        _trail = Vfx.Spawn(trailVfx, transform);            // follows transform
        var ctx = new PlayContext { Self = transform };
        _attack = Presentation.Play(PRESENTID.SomePres, in ctx);
    }
    void OnHit()  => _attack.Signal("hit");
    void OnDisable() { if (Vfx.IsPlaying(_trail)) _trail.Stop(); }
}
```
```csharp
// Wait for catalogs before startup audio (UniTask is a D-Drive dependency)
using Cysharp.Threading.Tasks; using DDrive.Runtime.Loop;
async UniTaskVoid Start() { await DDriveRuntimeBootstrap.Instance.WhenReady; Audio.PlayBgm(BGMID.Title); }
```
