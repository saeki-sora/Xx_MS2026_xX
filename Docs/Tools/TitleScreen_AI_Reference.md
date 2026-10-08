# Title Screen (タイトル画面) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-07**.
> Human guide: `Docs/Tools/TitleScreen_ガイド.html`. Maintenance rule: when you change anything under `Assets/Title/` (behaviour, Inspector labels/tooltips, defaults, menu, scene layout), update this file + the HTML guide in the same task. Style: `Docs/Tools/DOCS_GUIDE.md`.
> **Status: work in progress.** All art is placeholder. Start key is a temporary `A` key (grip input later). The user directs the animation; the current sequence below is their 2026-10-07 direction. The placeholder logo must NOT contain the text 「握れ、灼ける前に」 (user: not needed) — it reads `TITLE LOGO（仮）`.
> World (from the user's synopsis + rough art): 100 years ahead, people live in a cyber world; bugs/viruses spread like germs (ばいきん); cleaning staff fight them with special toothpaste. Look: pastel cyan/pink/lavender, magenta–cyan RGB offset, glitch, binary digits, sparkles, speed lines.

## 0. Mental model
- `Assets/Scenes/Title.unity` → press start → `SceneManager.LoadSceneAsync("Game")`.
- **Sequence** (user direction):
  1. Fade from black; the **gloomy purple "before" background** appears (slow zoom-out; user 2026-10-08). Floating germs run from the start; bubbles are hidden until the screen is repainted.
  2. **Opening** (`TitleOpening`, user direction 2026-10-07 v2): an upright toothpaste tube (cap on top) pops in near the centre; its middle gets pinched (narrows; no fingers shown — user removed them) while the rest of the tube **swells** (camera zooms in + rumbles, rising) → it can't hold: the **cap blows off** (user 2026-10-07: detached from the tube, launched up with random sideways speed + spin + gravity) and paste **shoots up out of the mouth** (stream grows upward, upward splash particles, shake, camera pulls back, tube deflates) → paste **oozes down from the top over the whole screen** ("どろーっ", user 2026-10-07: screen-space UI split into vertical columns that fall with Perlin-smoothed lags, so the front is uneven and viscous) and covers it → tube/stream/cap are hidden and `SetPainted(true)` swaps `beforeObjects` (gloomy background) off and `afterObjects` (Bubbles, started with Play) on, so the bright background is revealed → the paste flows on down off-screen (same lags) or fades = the screen is "repainted" → `Finished` fires `handOffDelay` after the reveal starts.
  3. On `Finished`, `TitleScreenController` calls `PlayEnter()` on `afterOpening`: **logo** drops from above to top-center (InCubic, lands with flash + shake + squash), two **character cubes** tumble down to bottom-left / bottom-right (each landing shakes the screen), then **press-start** text pops in at bottom-center.
  4. Ready → start key → exit effects (flash, glitch, blink) + camera zoom/shake → fade to black → load Game.
  - Any start input during 1–3 skips to the ready state (opening skipped, all elements to idle).
- Scene content is **generated** by the editor menu `Tools/タイトル/タイトルシーンを組み立てる` (`TitleSceneBuilder`). Re-running rebuilds `[Title]`; swapped sprites (SpriteRenderers by parent name) and the curtain texture (RawImage `PasteCurtain`) and the character `Model` children (by `CharacterLeft`/`CharacterRight`) are always kept. Dialog `数値を引き継いで作り直す` / `やめる` / `初期値で作り直す`: "keep" snapshots every Title component's values + all non-UI transforms under `[Title]` by hierarchy path (`TitleValueSnapshot`) and re-applies them after the rebuild; "initial" resets to §2. A root that fails `IsCurrentLayout` (needs `Opening/PasteStream` and a RawImage on `FadeCanvas/PasteCurtain`; older layout) only gets `作り直す` / `やめる` and always resets. Materials / ShaderFX profiles are created only if missing.
- **Tuning (user decision 2026-10-07: stay in the Inspector, no separate window).** `TitleScreenController` Inspector has a `流れの一覧（ここで全部まとめて調整できます）` overview (opening timings/zoom/rumble/shake + tube position/size, every element's timing/ease/moveFrom/rotateFrom/glitchIn/landing feel + position/size, camera & start settings incl. `画面に映る高さ` = orthographicSize×2). Controller / Opening / ElementMotion / CameraMotion Inspectors show Play-mode buttons `▶ 最初から再生` (`controller.Restart()`) and `再生中に変えた値を残す` (`TitlePlayModeKeeper`: snapshot to `SessionState`, written back to the scene on `EnteredEditMode`, scene marked dirty, undoable). Positions/sizes are edit-mode only (motion overwrites transforms in Play).
- Element = parent GameObject with `TitleElementMotion` (animates parent localPosition/localRotation/localScale, children's SpriteRenderer alpha, ShaderFX instance props; hides all child Renderers until `PlayEnter`) → child `Sprite` (`SpriteRenderer` + `TitleSpriteFit`) **or** child `Model` (3D; currently a cube primitive).
- Motion runs only in Play Mode, so the Scene view shows the final layout. `TitleSpriteFit` is `ExecuteAlways`.
- Effects/SE: `FortressEffect` slots (project rule) played through `TitleEffects.Play` → `FortressEffectPlayer`, **only if `Vfx.IsBound` / `Audio.IsBound`**. The D-Drive bootstrap lives in `Game.unity` only, so Title slots are currently silent (no warnings). Adding a `DDriveRuntimeBootstrap` to Title is an open decision: it is `KeepAcrossScenes` and the first instance wins (Game's would be destroyed with a warning), so its NGO settings would have to match Game's (`DefaultNetBridge: Ngo`, `DefaultNetStart: Manual`, port 7777).

## 1. Paths / asmdefs
```
Assets/Title/
  Scripts/Runtime/ MS2026.Title.Runtime.asmdef (ns MS2026.Title; refs MS2026.Fortress.Runtime, MS2026.ShaderFX.Runtime, DDrive.Runtime, DDrive.Foundation, Unity.InputSystem, UnityEngine.UI)
    TitleScreenController.cs, TitleOpening.cs, TitleElementMotion.cs, TitleSpriteFit.cs, TitleScreenFader.cs, TitleCameraMotion.cs, TitleEase.cs, TitleEffects.cs (internal)
  Scripts/Editor/  MS2026.Title.Editor.asmdef (ns MS2026.Title.EditorTools) TitleSceneBuilder.cs, TitleValueSnapshot.cs (path-keyed value copy; keeps the target's object references and ref-array sizes),
                   TitlePlayModeKeeper.cs ([InitializeOnLoad]), TitleInspectors.cs (custom editors for Controller / Opening / ElementMotion / CameraMotion)
  Sprites/  Title_Background.png (1920x1080 pastel cyber room = after repaint; no binary digits — user removed them), Title_BackgroundBefore.png (1920x1080 same composition, dark gloomy purple, dirty basin, germ shadows, scanlines, glitch bars, vignette = before repaint), Title_Toothpaste.png (480x1300 upright tube WITHOUT cap — mouth on top, side padding for the swell), Title_ToothpasteCap.png (150x170 cap, separate so it can fly off), Title_PasteStream.png (260x1200, base at bottom), Title_PasteCurtain.png (1920x1400, vertical stripes, dripping bottom edge, no label text — user removed it), Title_Logo.png (1400x460 `TITLE LOGO（仮）`),
            Title_PressStart.png (1166x170 「握ってスタート！（Aキー）」), Title_SoftDot.png (64x64, bubbles/splash), Title_Germ.png (128x128 purple germ).
            Placeholders drawn with System.Drawing (white fill, blue outline, magenta/cyan offset). The builder sets new textures in this folder to Sprite / Single / 100 PPU / no mips / Clamp.
  Materials/ Title_SpriteFX.mat (`ShaderFX/Uber Sprite`), Title_Bubble.mat / Title_Germ.mat (`Universal Render Pipeline/2D/Sprite-Unlit-Default`, mainTexture SoftDot / Germ),
             Title_CubeLeft.mat (`ShaderFX/Uber`, _BaseColor (1,.75,.93)), Title_CubeRight.mat (`ShaderFX/Uber`, _BaseColor (.7,.93,1))
  Effects/   TitleFX_Logo.asset (Dissolve edge (.3,.95,1) width .12 noise 6 + Glitch amount 0 block 14 speed 18 split .03 + HitFlash white) — used by Logo and PressStart
             TitleFX_Character.asset (RimLight (.35,.95,1) power 3 ×1.2 + Glitch amount 0 block 10 speed 16 split .025 + HitFlash white) — used by both cubes
             Glitch amount stays 0 in the profiles; TitleElementMotion drives `_GlitchAmount` per renderer.
```
Build list: the builder **appends** `Assets/Scenes/Title.unity` to `EditorBuildSettings` (end of list, so the existing launch flow / net tests are unchanged). Making Title the first scene is a user decision (not done).

## 2. Generated scene (builder defaults)
Camera (`Camera.main`): orthographic, size 5.4 (view 19.2×10.8 world at 16:9), pos (0,0,-10), SolidColor black, + `TitleCameraMotion`. The default Directional Light stays (it lights the cubes).
| Object (under `[Title]`) | Pos | Content | sortingOrder | Settings (non-default) |
|---|---|---|---|---|
| Background | (0,0,5) | Sprite, CoverCamera (overscan 1.12) | 0 | plays at Start: dur 3, OutCubic, fadeIn, scaleFrom 1.12; float (0.08,0.05,0) @0.08Hz; pulse 0.01 @0.12Hz |
| Background / SpriteBefore | (0,0,0) local | Sprite Title_BackgroundBefore, CoverCamera | 1 (over the bright one) | `opening.beforeObjects`; shares the Background motion (fade/zoom) |
| Germs (PS) | (0,2.6,4) | Box 20×5, random dir, Title_Germ.mat | 4 | rate 1.2, life 8–12, speed 0.15–0.45, size 0.45–1, spin ±0.6 rad/s, max 20 |
| Bubbles (PS) | (0,-6.2,3) | Box 22×0.5 emitting up, Title_Bubble.mat; `opening.afterObjects` (inactive until repaint) | 5 | rate 45, life 3.5–7, speed 0.6–1.8, size 0.05–0.25, max 400 |
| Opening (`TitleOpening`) / Toothpaste / Sprite | tube (0,-1.4,0) | Sprite Height 8 (user: bigger; sliced at runtime) | 15 | component defaults (§3) |
| Opening / Toothpaste / Cap / Sprite | (0,3.35,0) local to the tube = where the cap was in the old single image | Sprite Height 1.05 (same px scale as the tube) | 18 | `cap`; child of the tube until eject |
| Opening / PasteStream / Sprite | base (0,2.6,0), moved to the cap on eject | Sprite, child scale (0.55,0.9) → ~1.4×11 world, child offset up by half its height | 16 | `stream`; parent scaleY animates 0→1 |
| [Title] / FadeCanvas / PasteCurtain | sibling 0 (under Fade), full-screen stretch | RawImage template (texture Title_PasteCurtain, disabled); runtime children "Column i" | overlay | `curtain` |
| Opening / BurstSplash (PS) | moved to the cap on eject | cone 25° up, r 0.2, Title_Bubble.mat | 40 | one-shot: bursts 80 @0 + 40 @0.06, speed 7–16, life 0.8–1.6, size 0.15–0.6 shrinking to 20%, gravity 1.5, drag 1.5 |
| Logo | (0,2.4,0) | Sprite Height 3.6, FX + TitleFX_Logo | 20 | delay 0, dur 0.55, InCubic, fadeIn off, moveFrom (0,7,0), glitchIn 0.6, flashOnLand 1, shakeOnLand 0.3, squash 0.18, pulse 0.012 @0.6Hz, glint 3.5s ×0.45, glitch every ~5s ×0.6, flashOnExit 0.8, glitchOnExit 1 |
| CharacterLeft / Model | (-6.2,-2.6,0) | Cube ×2.3, rot (-20,35,0), Title_CubeLeft + TitleFX_Character | (3D) | delay 0.45 |
| CharacterRight / Model | (6.2,-2.6,0) | Cube ×2.3, rot (-20,-35,0), Title_CubeRight + TitleFX_Character | (3D) | delay 0.7 |
| (both characters) | | | | dur 0.5, InCubic, fadeIn off, moveFrom (0,10,0), rotateFrom (90,±200,±60), glitchIn 0.5, flashOnLand 0.6, shakeOnLand 0.35, squash 0.22, float (0,0.08,0) @0.4Hz, flashOnExit 0.6, glitchOnExit 0.7 |
| PressStart | (0,-1.5,0) (user: a little below centre) | Sprite Height 1.1, FX + TitleFX_Logo | 30 | delay 1.5, dur 0.5, OutBack, scaleFrom 0.6, pulse 0.03 @0.8Hz, blinkMinAlpha 0.35 @0.8Hz, exit BlinkOut 0.6s, glitchOnExit 0.8 |
| FadeCanvas/Fade | | Overlay canvas order 1000, full-stretch Image (raycast off, disabled in edit mode) + `TitleScreenFader` | | |
Controller wiring (opening gets `cameraMotion`; no fader any more): `opening`, `background`, `afterOpening = [Logo, CharacterLeft, CharacterRight, PressStart]`, `pressStart`, `ambientParticles = [Bubbles, Germs]`, `fader`, `cameraMotion`. Opening gets `cameraMotion` + `fader`.
Depth: cubes (opaque, z≈0) occlude Background (z 5) / particles (z 3–4) by depth; sprites at z 0 sort by sortingOrder.

## 3. Runtime API (ns `MS2026.Title`)
```csharp
// TitleScreenController
string gameSceneName = "Game"; Key startKey = Key.A;     // Keyboard.current[startKey].wasPressedThisFrame
TitleOpening opening;                                    // null → afterOpening enter immediately
TitleElementMotion background;                           // PlayEnter at Start
TitleElementMotion[] afterOpening;                       // PlayEnter on opening.Finished; enterDelay counts from then
TitleElementMotion pressStart;                           // only used as onStart effect position
ParticleSystem[] ambientParticles;                       // Play(true) at Start
TitleScreenFader fader; TitleCameraMotion cameraMotion;
float fadeInSeconds = 0.8f; Color startFlashColor = (.85,1,1,.9); float startHoldSeconds = 0.55f; float fadeOutSeconds = 0.5f;
float startZoom = 0.12f; float startShake = 0.15f;
FortressEffect onStart;                                  // tint off
void RequestStart();   // Intro → skip (opening.Skip + all SkipToIdle + fader clear); Ready → start; Starting → ignored
// Ready when opening finished AND time >= finishTime + max(afterOpening.EnterEndTime).
// Start: all elements PlayExit; hold = max(startHoldSeconds, longest ExitDuration*0.8); zoom over hold+fadeOut; load after hold+fadeOut.
// Landed(element) → cameraMotion.Shake(element.shakeOnLand).
void Restart();   // reset all elements (ResetToStart), opening (ResetToStart), camera (ResetMotion), clear ambient particles, then Begin() again; no-op once loading started
// Start: AdoptUnlistedElements — any TitleElementMotion under the controller that is not background/afterOpening is appended to
//   afterOpening with a LogWarning (elements stay hidden until PlayEnter, so an unlisted one would otherwise never appear).

// TitleOpening — stages Idle → Waiting(appearDelay) → Appearing → Swelling → Ejecting → Covering → Holding → Revealing → Done
Transform tube; Transform cap; Transform[] pinchers; Transform stream; ParticleSystem burstParticles; RectTransform curtain; TitleCameraMotion cameraMotion;
float appearDelay = 0.3f, appearDuration = 0.45f;                       // tube scale 0→1 OutBack
float swellDuration = 2f; float swellAmount = 0.4f; float pinchAmount = 0.5f [0..0.9]; float pinchHeight = 0.45f [0..1]; float pinchWidth = 0.08f;
Vector2 swellRange = (0.06, 0.84); float swellStretch = 0.08f; float tubeTremble = 0.05f; float pincherTravel = 0.35f;
float zoomIn = 0.15f; float rumble = 0.12f; int sliceCount = 48 [8..96];
//   grip = InCubic(t)*0.65 + t*0.35. Each slice width = 1 + swell*bulge(v)*(1-g) - pinch*g, bulge = sin(pi*body)^0.6 inside swellRange,
//   g = exp(-((v-pinchHeight)/pinchWidth)^2), v = slice centre 0 (bottom) .. 1 (top). Tube scaleY = 1 + swellStretch*grip. Pinchers (optional, builder leaves the array empty — user removed the fingers) move toward x=0 by pincherTravel*grip.
float capLaunchSpeed = 16f; float capLaunchSpread = 3f /*±x speed*/; float capSpin = 900f /*deg/s, random sign*/; float capGravity = 20f;
float ejectDuration = 0.4f /*stream 0→1 OutCubic*/; float ejectShake = 0.4f; float pullBackDuration = 0.5f /*ZoomTo(0, OutBack)*/; float deflateDuration = 0.35f /*swell → -0.1*/;
float curtainDelay = 0.25f /*from eject*/; float curtainDropDuration = 1.3f /*until the slowest column lands*/; TitleEaseType curtainEase = InOutSine;
int curtainColumns = 28 [1..64]; float curtainUnevenness = 0.45f [0..0.9]; float curtainHold = 0.25f;
RevealStyle revealStyle = SlideDown /*or Fade*/; float revealDuration = 0.9f /*SlideDown: columns continue down, InCubic, same lags*/; float handOffDelay = 0.15f /*from reveal start*/;
FortressEffect onPinch /*tube pos*/, onEject /*cap pos*/, onCovered /*screen centre; also a half-strength shake*/;
GameObject[] beforeObjects /*active until covered*/, afterObjects /*inactive until covered; their ParticleSystems Play() when shown*/;
event Action Finished; void Play(); void Skip() /*also SetPainted(true)*/; void ResetToStart() /*SetPainted(false)*/; bool IsDone;
// Awake: slices the tube sprite into `sliceCount` horizontal strips (Sprite.Create, FullRect, child GameObjects "Slice i" under the
//   tube's SpriteRenderer, original renderer disabled) — works with any swapped tube image; give the image side padding so the swell isn't clipped.
// Mouth position (stream base, particles, onEject) = top-centre of the tube sprite bounds; the tube renderer is the child named "Sprite" (not the cap's).
// Cap: Awake stores parent/local pose; ResetToStart re-parents and restores it. Eject: SetParent(opening, worldPositionStays) + ballistic flight in Update until covered (then hidden).
// Curtain: Awake disables the template RawImage on `curtain` and creates `curtainColumns` RawImage children (anchors x i/n..(i+1)/n at the top,
//   pivot bottom, uvRect = matching vertical slice, +1px overlap). lag_i = clamp01((Perlin(i*0.35, randomSeed) - 0.2)/0.6).
//   cover c (linear over time) in 0..1: local_i = clamp01((c - lag_i*u)/(1-u)), u = unevenness; bottom edge drop = coveredDrop*ease(local_i),
//   coveredDrop = screenH + 0.6*(curtainH - screenH), curtainH = 1.35*screenH (screenH = curtain rect height). c in 1..2: drop adds (screenH+curtainH)*InCubic(local).
//   Columns hidden when off-screen. Use a curtain texture whose stripes run vertically (columns offset vertically). Covered → tube, stream, pinchers deactivated.

// TitleElementMotion  [DisallowMultipleComponent] — starts Dormant (child Renderers disabled) until PlayEnter/SkipToIdle
// Enter: enterDelay 0, enterDuration 0.8, enterEase OutCubic, fadeIn true, moveFrom (0,0,0), scaleFrom 1, rotateFrom (0,0,0) (euler, lerped to 0),
//        burnIn false, flashOnLand 0 [0..1], shakeOnLand 0, squashOnLand 0 [0..0.5], onLand (FortressEffect, tint off), glitchIn 0 [0..1]
// Idle:  floatAmplitude (0,0,0), floatSpeed 0.5 (Hz), pulseAmount 0, pulseSpeed 1 (Hz), blinkMinAlpha 1 [0..1], blinkSpeed 1 (Hz),
//        glintInterval 0 (s, 0=off), glintStrength 0.5, glitchInterval 0 (s, 0=off, randomised ×0.7–1.3), glitchStrength 0.6
// Exit:  exitStyle None|FadeOut|BlinkOut|PunchOut, exitDuration 0.5, flashOnExit 0 [0..1], glitchOnExit 0 [0..1]
event Action<TitleElementMotion> Landed;   // natural landing only (not on skip); also plays onLand
void PlayEnter(); void SkipToIdle(); void PlayExit(); void ResetToStart() /*back to hidden Dormant*/; bool IsEntering (Dormant/Waiting/Entering); float EnterEndTime; float ExitDuration;
// Pose: pos = base + offset; rot = Euler(rotation) * baseRot; scale = base * scale * (1+sq, 1-sq, 1+sq),
//   sq = squashOnLand * exp(-7t) * cos(22t) after landing (pivot = object centre).
// ShaderFX per frame: SetInstanceDissolveAmount(burnIn ? 1-t : 0), SetInstanceHitFlashAmount(max(flash², glint));
//   `_GlitchAmount` via SetInstanceFloat ONLY when glitchIn/glitchInterval/glitchOnExit > 0 (non-instanced prop → that renderer leaves SRP batching).
//   Flash decays linearly over 0.35 s; glint = 0.25 s sine at the start of each interval; glitch bursts decay over 0.18 s.
// Alpha affects SpriteRenderers only (3D models don't fade — use moveFrom off-screen instead).

// TitleSpriteFit [ExecuteAlways][RequireComponent(SpriteRenderer)]
// mode None|Height|Width|CoverCamera (default Height), size 3, coverOverscan 1.12, targetCamera (null → Camera.main, must be orthographic)
// Edit mode: refits every LateUpdate. Play mode: only when sprite or camera aspect changes (camera zoom/shake don't rescale the BG).

// TitleScreenFader [RequireComponent(Image)] — unscaled time
void Set(Color c); void FadeTo(Color target, float s); void FadeFromTo(Color from, Color to, float s); bool IsRunning;

// TitleCameraMotion [RequireComponent(Camera)] — additive: removes last frame's offset/zoom in LateUpdate then re-applies,
// so other camera animation composes. shakeDuration 0.35, shakeFrequency 25, rumbleFrequency 40 (Perlin).
void Shake(float strength /*world units, decays*/); void SetRumble(float strength /*continuous until 0*/); void ResetMotion();
void ZoomTo(float amount /*0.1 = 10% closer, holds; negative = wider*/, float seconds, TitleEaseType ease = OutCubic);

// TitleEaseType: Linear, OutCubic, OutBack, OutBounce, InOutSine, InCubic (Japanese InspectorNames); TitleEase.Evaluate(type, t)
```

## 4. Editor UI
- Menu: `Tools/タイトル/タイトルシーンを組み立てる`. Refuses in Play Mode. Opens `Assets/Scenes/Title.unity` (asks to save the current scene). Rebuild dialog: `数値を引き継いで作り直す` / `やめる` / `初期値で作り直す` (old layout: `作り直す` / `やめる`).
- Snapshot covers component types TitleScreenController, TitleOpening, TitleElementMotion, TitleSpriteFit, TitleCameraMotion, ParticleSystem (EditorJsonUtility). Not covered: ShaderFX EffectTarget, renderers/materials (assets keep their own edits), Camera component. Saves the scene. Logs `[Title] タイトルシーンを組み立てました。…`.
- Inspector: Japanese `[Header]`s (`登場`, `待機中のゆらぎ`, `スタート時（退場）`, `遷移`, `オープニング（歯磨き粉）`, `配置物（…）`, `画面全体の演出`, `エフェクト・効果音（D-Drive）`, on TitleOpening `配置物`, `出てくる`, `つまんでふくらむ`, `噴き出す`, `塗り替わる`) and Japanese tooltips; enum values have Japanese `InspectorName`s. `FortressEffect` slots get the Fortress drawer (`▶ 試す` etc.).

## 5. Integration
- Game scene loads normally (single mode). Title has no NetworkManager / D-Drive bootstrap.
- Grip input: not wired. Plan: call `TitleScreenController.RequestStart()` from a grip threshold (see `GripInputBridge_AI_Reference.md`). The pinch/swell could later be driven by the real grip value (idea, not implemented).
- ShaderFX: `EffectTarget` on sprites (Uber Sprite) and on the cubes (3D Uber). Module classes are in `MS2026.ShaderFX.Modules` (editor code needs that `using`).

## 6. Gotchas
1. Profiles/materials are created only if missing: to apply new builder defaults, delete `Assets/Title/Effects/*.asset` (and/or materials) and rebuild. Avoid hand-editing `Title.unity`. When changing builder defaults, tell the user to rebuild with `初期値で作り直す` (keep would re-apply their old numbers over the new defaults).
9. Values changed during Play revert on stop unless `再生中に変えた値を残す` was pressed; positions can't be tuned in Play (motion overwrites transforms every frame).
2. Replacing a character: replace the child named `Model` under `CharacterLeft`/`CharacterRight` (keep the name so rebuilds keep it). Sprite characters work too (any Renderer); add `TitleSpriteFit` yourself if wanted.
3. `TitleSpriteFit` CoverCamera needs an orthographic camera.
4. Particle materials use the URP 2D Sprite-Unlit shader; falls back to `Sprites/Default` if not found.
5. `FortressEffect` slots in Title are silent until a D-Drive bootstrap exists in the Title scene (see §0).
6. Dissolve noise is world-space, so moving/scaling while dissolving slides the pattern (only used if `burnIn` is on; off by default now).
8. Symptom "objects don't show in Play" = the scene was built by an older builder (e.g. no `Opening`/`afterOpening`): rebuild with the menu. The adopt-unlisted fallback makes them appear anyway, but without the opening.
7. Squash pivots around the object centre, so the bottom lifts slightly during the squash (acceptable for placeholders).

## 7. Related
`ShaderFX_AI_Reference.md` (EffectTarget/profiles), `FortressDesigner_AI_Reference.md` §Effects (`FortressEffect`), `DDrive_AI_Reference.md` (bootstrap), `GripInputBridge_AI_Reference.md` (future start input).
