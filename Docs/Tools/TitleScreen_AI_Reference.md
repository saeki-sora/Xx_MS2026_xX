# Title Screen (タイトル画面) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-08** (AE frame sequence for the toothpaste: 2026-10-17; character portraits: 2026-10-22).
> Human guide: `Docs/Tools/TitleScreen_ガイド.html`. Maintenance rule: when you change anything under `Assets/Title/` (behaviour, Inspector labels/tooltips, defaults, menu, scene layout), update this file + the HTML guide in the same task. Style: `Docs/Tools/DOCS_GUIDE.md`.
> **Status: work in progress.** Art is placeholder except the AE toothpaste frames and the two character portraits (2026-10-22). Start = temporary `A` key **or the grip sensor** (since 2026-10-12; the exhibition has no keyboard). The user directs the animation; the current sequence below is their 2026-10-07 direction. The placeholder logo must NOT contain the text 「握れ、灼ける前に」 (user: not needed) — it reads `TITLE LOGO（仮）`.
> World (from the user's synopsis + rough art): 100 years ahead, people live in a cyber world; bugs/viruses spread like germs (ばいきん); cleaning staff fight them with special toothpaste. Look: pastel cyan/pink/lavender, magenta–cyan RGB offset, glitch, binary digits, sparkles, speed lines.

## 0. Mental model
- `Assets/Scenes/Title.unity` → press start → `SceneManager.LoadSceneAsync("Lobby")` (the network lobby, built by UI Studio — `UIStudio_AI_Reference.md` §7) → host starts the match → Character_Select (`CharacterSelect_AI_Reference.md`, since 2026-10-08) → Game. **Exhibition auto-connect (default since 2026-10-12)**: the Lobby scene shows no lobby UI; it covers with the toothpaste curtain, finds/creates a LAN room by itself and goes straight to Character_Select (first PC = host P1, others join in arrival order). The lobby UI appears only with the launch option `-lobby` or with `[Lobby] Session` › `autoConnect` off (`UIStudio_AI_Reference.md` §7 "Exhibition auto-connect"). Title itself is unchanged (still loads "Lobby"). **Test launches** (any command-line arg starting with `-fortress-`, e.g. every `Tools/NetTest/*.bat`) skip Title and Lobby: `TitleScreenController.Awake` loads `directSceneForTestLaunch` ("Game") immediately.
- **Sequence** (user direction):
  1. Fade from black; the **gloomy purple "before" background** appears (slow zoom-out; user 2026-10-08). Floating germs run from the start; bubbles are hidden until the screen is repainted.
  2. **Opening** (`TitleOpening`, user direction 2026-10-07 v2): an upright toothpaste tube (cap on top) pops in near the centre; its middle gets pinched (narrows; no fingers shown — user removed them) while the rest of the tube **swells** (camera zooms in + rumbles, rising) → it can't hold: the **cap blows off** (user 2026-10-07: detached from the tube, launched up with random sideways speed + spin + gravity) and paste **shoots up out of the mouth** (stream grows upward, upward splash particles, shake, camera pulls back, tube deflates) → paste **oozes down from the top over the whole screen** ("どろーっ", user 2026-10-07: screen-space UI split into vertical columns that fall with Perlin-smoothed lags, so the front is uneven and viscous) and covers it → tube/stream/cap are hidden and `SetPainted(true)` swaps `beforeObjects` (gloomy background) off and `afterObjects` (Bubbles, started with Play) on, so the bright background is revealed → the paste flows on down off-screen (same lags) or fades = the screen is "repainted" → `Finished` fires `handOffDelay` after the reveal starts.
  - **AE frame sequence (2026-10-17, user decision):** the tube part (appear → pinch/swell → cap flies off) was made in After Effects as a transparent PNG sequence (120 frames 1920x1080, cap leaves on frame 82, **no paste in it**). When `TitleOpening.tubeSequence` (a `TitleFrameSequence` asset) is set, the opening plays those frames instead of the procedural pop-in/slicing/swell and the separate cap; at the sequence's `ejectFrame` it runs the normal eject (stream from the per-frame mouth, splash, shake, onEject) and everything after is unchanged. Frame rate is a setting on the asset (**30 fps for now; the user will confirm with the artist**). Open questions for the artist (not decided): the label text on the AE tube reads upside-down (cap up), and the fps.
  3. On `Finished`, `TitleScreenController` calls `PlayEnter()` on `afterOpening`: **logo** drops from above to top-center (InCubic, lands with flash + shake + squash), two **characters** (since 2026-10-22 portrait sprites: left = red, right = yellow, tilting ±25° on Z while falling; placeholder cubes tumbling in 3D only when the portrait PNGs are missing) drop down to bottom-left / bottom-right (each landing shakes the screen), then **press-start** text pops in at bottom-center.
  4. Ready → start key or grip → exit effects (flash, glitch, blink) + camera zoom/shake → fade to black → load Lobby.
  - Any start input during 1–3 skips to the ready state (opening skipped, all elements to idle).
- Scene content is **generated** by the editor menu `Tools/タイトル/タイトルシーンを組み立てる` (`TitleSceneBuilder`). Re-running rebuilds `[Title]`; swapped sprites (SpriteRenderers by parent name) and the curtain texture (RawImage `PasteCurtain`) and the character `Model` children (by `CharacterLeft`/`CharacterRight`) are always kept. Dialog `数値を引き継いで作り直す` / `やめる` / `初期値で作り直す`: "keep" snapshots every Title component's values + all non-UI transforms under `[Title]` by hierarchy path (`TitleValueSnapshot`) and re-applies them after the rebuild; "initial" resets to §2. A root that fails `IsCurrentLayout` (needs `Opening/PasteStream` and a RawImage on `FadeCanvas/PasteCurtain`; older layout) only gets `作り直す` / `やめる` and always resets. Materials / ShaderFX profiles are created only if missing.
- **Tuning (user decision 2026-10-07: stay in the Inspector, no separate window).** `TitleScreenController` Inspector has a `流れの一覧（ここで全部まとめて調整できます）` overview (opening timings/zoom/rumble/shake + tube position/size, every element's timing/ease/moveFrom/rotateFrom/glitchIn/landing feel + position/size, camera & start settings incl. `画面に映る高さ` = orthographicSize×2). Controller / Opening / ElementMotion / CameraMotion Inspectors show Play-mode buttons `▶ 最初から再生` (`controller.Restart()`) and `再生中に変えた値を残す` (`TitlePlayModeKeeper`: snapshot to `SessionState`, written back to the scene on `EnteredEditMode`, scene marked dirty, undoable). Positions/sizes are edit-mode only (motion overwrites transforms in Play).
- Element = parent GameObject with `TitleElementMotion` (animates parent localPosition/localRotation/localScale, children's SpriteRenderer alpha, ShaderFX instance props; hides all child Renderers until `PlayEnter`) → child `Sprite` (`SpriteRenderer` + `TitleSpriteFit`) **or** child `Model` (3D; the builder's placeholder is a cube primitive, used for a character only when its portrait PNG is missing).
- Motion runs only in Play Mode, so the Scene view shows the final layout. `TitleSpriteFit` is `ExecuteAlways`.
- Effects/SE: `FortressEffect` slots (project rule) played through `TitleEffects.Play` → `FortressEffectPlayer`, **only if `Vfx.IsBound` / `Audio.IsBound`**. The D-Drive bootstrap lives in `Game.unity` only, so Title slots are currently silent (no warnings). Adding a `DDriveRuntimeBootstrap` to Title is an open decision: it is `KeepAcrossScenes` and the first instance wins (Game's would be destroyed with a warning), so its NGO settings would have to match Game's (`DefaultNetBridge: Ngo`, `DefaultNetStart: Manual`, port 7777).

## 1. Paths / asmdefs
```
Assets/Title/
  Scripts/Runtime/ MS2026.Title.Runtime.asmdef (ns MS2026.Title; refs MS2026.Fortress.Runtime, MS2026.ShaderFX.Runtime, DDrive.Runtime, DDrive.Foundation, Unity.InputSystem, UnityEngine.UI)
    TitleScreenController.cs, TitleCharacterRoster.cs (ScriptableObject, 2026-10-22), TitleOpening.cs, TitleFrameSequence.cs (ScriptableObject: AE frames + fps + eject frame + per-frame mouths), TitleElementMotion.cs, TitleSpriteFit.cs, TitleScreenFader.cs, TitleCameraMotion.cs, TitleEase.cs, TitleEffects.cs (internal)
  Scripts/Editor/  MS2026.Title.Editor.asmdef (ns MS2026.Title.EditorTools) TitleSceneBuilder.cs, TitleValueSnapshot.cs (path-keyed value copy; keeps the target's object references and ref-array sizes),
                   TitlePlayModeKeeper.cs ([InitializeOnLoad]), TitleInspectors.cs (custom editors for Controller / Opening / ElementMotion / CameraMotion),
                   TitleCharacterRosterEditor.cs (inspector of TitleCharacterRoster + menu `タイトルのキャラ一覧を開く`, 2026-10-22),
                   TitleFrameSequenceImporter.cs (menu `歯磨き粉の連番を取り込む` + `ApplyToOpening`, used by the builder; also the TitleFrameSequence inspector)
  Sprites/  Title_Background.png (1920x1080 pastel cyber room = after repaint; no binary digits — user removed them), Title_BackgroundBefore.png (1920x1080 same composition, dark gloomy purple, dirty basin, germ shadows, scanlines, glitch bars, vignette = before repaint), Title_Toothpaste.png (480x1300 upright tube WITHOUT cap — mouth on top, side padding for the swell), Title_ToothpasteCap.png (150x170 cap, separate so it can fly off), Title_PasteStream.png (260x1200, base at bottom), Title_PasteCurtain.png (1920x1400, vertical stripes, dripping bottom edge, no label text — user removed it), Title_Logo.png (1400x460 `TITLE LOGO（仮）`),
            Title_PressStart.png (1166x170 「握ってスタート！（Aキー）」), Title_SoftDot.png (64x64, bubbles/splash), Title_Germ.png (128x128 purple germ),
            Title_Chara_Red.png (1142x2048) / Title_Chara_Yellow.png (1269x2048) / Title_Chara_Green.png (1012x2048) / Title_Chara_Blue.png (883x2048) — team-drawn portraits (2026-10-22),
            cropped by Claude to the alpha bbox + 8 px and scaled to 2048 high (Pillow). Red/Yellow were first added as Title_CharaLeft/Right.png and renamed together with their .meta (GUIDs kept);
            Green/Blue .meta were written by Claude as copies of the Red meta (Sprite, 100 PPU, new GUID). Originals (2894x4093 / 2978x4093) are in Assets/_Game/Characters/Portraits/Chara_{Red,Yellow,Green,Blue}_Portrait.png (unused, Default import).
  Resources/TitleCharacters.asset   (TitleCharacterRoster, §3; 4 entries red/yellow/green/blue, all height 10.35, offsets red (0.3,1.1), yellow (0.6,0.8), green/blue (0.3,1.1) — red/yellow are the user's tuned scene values)
            ToothpasteFrames/ (AE frames, cropped by the importer: 287x877 each for the 2026-10-17 delivery, Sprite / 100 PPU / pivot centre / FullRect / no mips / Clamp) + ToothpasteFrames.asset (`TitleFrameSequence`).
            Placeholders drawn with System.Drawing (white fill, blue outline, magenta/cyan offset). The builder sets new textures in this folder to Sprite / Single / 100 PPU / no mips / Clamp.
  Materials/ Title_SpriteFX.mat (`ShaderFX/Uber Sprite`), Title_Bubble.mat / Title_Germ.mat (`Universal Render Pipeline/2D/Sprite-Unlit-Default`, mainTexture SoftDot / Germ),
             Title_CubeLeft.mat (`ShaderFX/Uber`, _BaseColor (1,.75,.93)), Title_CubeRight.mat (`ShaderFX/Uber`, _BaseColor (.7,.93,1))
  Effects/   TitleFX_Logo.asset (Dissolve edge (.3,.95,1) width .12 noise 6 + Glitch amount 0 block 14 speed 18 split .03 + HitFlash white) — used by Logo and PressStart
             TitleFX_Character.asset (RimLight (.35,.95,1) power 3 ×1.2 + Glitch amount 0 block 10 speed 16 split .025 + HitFlash white) — used by both cubes
             Glitch amount stays 0 in the profiles; TitleElementMotion drives `_GlitchAmount` per renderer.
```
Build list: the title builder **appends** `Assets/Scenes/Title.unity` to `EditorBuildSettings`. Since 2026-10-08 (user decision) UI Studio's `タイトル→ロビー→ゲームの流れを組み立てる` sets the order **Title → Lobby → Game** (→ SampleScene) and sets the scene value `gameSceneName = "Lobby"`; net tests still work because test launches skip to Game.

## 2. Generated scene (builder defaults)
Camera (`Camera.main`): orthographic, size 5.4 (view 19.2×10.8 world at 16:9), pos (0,0,-10), SolidColor black, + `TitleCameraMotion`. The default Directional Light stays (it lights the cubes).
| Object (under `[Title]`) | Pos | Content | sortingOrder | Settings (non-default) |
|---|---|---|---|---|
| Background | (0,0,5) | Sprite, CoverCamera (overscan 1.12) | 0 | plays at Start: dur 3, OutCubic, fadeIn, scaleFrom 1.12; float (0.08,0.05,0) @0.08Hz; pulse 0.01 @0.12Hz |
| Background / SpriteBefore | (0,0,0) local | Sprite Title_BackgroundBefore, CoverCamera | 1 (over the bright one) | `opening.beforeObjects`; shares the Background motion (fade/zoom) |
| Germs (PS) | (0,2.6,4) | Box 20×5, random dir, Title_Germ.mat | 4 | rate 1.2, life 8–12, speed 0.15–0.45, size 0.45–1, spin ±0.6 rad/s, max 20 |
| Bubbles (PS) | (0,-6.2,3) | Box 22×0.5 emitting up, Title_Bubble.mat; `opening.afterObjects` (inactive until repaint) | 5 | rate 45, life 3.5–7, speed 0.6–1.8, size 0.05–0.25, max 400 |
| Opening (`TitleOpening`) / Toothpaste / Sprite | tube (0,-1.4,0) | Sprite Height 8 (user: bigger; sliced at runtime) | 15 | component defaults (§3) |
| (if `ToothpasteFrames.asset` exists) | tube moved to the AE position = camera centre + `screenOffset` ((0.035,1.015) for the 2026-10-17 delivery), tube & Sprite scale 1 | fit mode None (100px = 1, same size as in the AE frame); sprite = frame `ejectFrame-1` for the Scene view | 15 | `tubeSequence` set, Cap GameObject inactive (`ApplyToOpening(placeLikeSource: true)`) |
| Opening / Toothpaste / Cap / Sprite | (0,3.35,0) local to the tube = where the cap was in the old single image | Sprite Height 1.05 (same px scale as the tube) | 18 | `cap`; child of the tube until eject |
| Opening / PasteStream / Sprite | base (0,2.6,0), moved to the cap on eject | Sprite, child scale (0.55,0.9) → ~1.4×11 world, child offset up by half its height | 16 | `stream`; parent scaleY animates 0→1 |
| [Title] / FadeCanvas / PasteCurtain | sibling 0 (under Fade), full-screen stretch | RawImage template (texture Title_PasteCurtain, disabled); runtime children "Column i" | overlay | `curtain` |
| Opening / BurstSplash (PS) | moved to the cap on eject | cone 25° up, r 0.2, Title_Bubble.mat | 40 | one-shot: bursts 80 @0 + 40 @0.06, speed 7–16, life 0.8–1.6, size 0.15–0.6 shrinking to 20%, gravity 1.5, drag 1.5 |
| Logo | (0,2.4,0) | Sprite Height 3.6, FX + TitleFX_Logo | 20 | delay 0, dur 0.55, InCubic, fadeIn off, moveFrom (0,7,0), glitchIn 0.6, flashOnLand 1, shakeOnLand 0.3, squash 0.18, pulse 0.012 @0.6Hz, glint 3.5s ×0.45, glitch every ~5s ×0.6, flashOnExit 0.8, glitchOnExit 1 |
| CharacterLeft / Sprite | parent (-6.2,-2.6,0), child = roster entry 1 LocalPosition | roster's 1st usable entry (red), Height = entry.height, Title_SpriteFX + TitleFX_Character | 10 | delay 0.45, rotateFrom (0,0,25) |
| CharacterRight / Sprite | parent (6.2,-2.6,0), child = roster entry 2 LocalPosition | roster's 2nd usable entry (yellow), same | 10 | delay 0.7, rotateFrom (0,0,-25) |
| (Play) | | sprite/height/offset replaced at Awake by a random pair from the roster (`TitleScreenController.PickCharacters`) | | |
| (fallback when a portrait PNG is missing) CharacterLeft/Right / Model | (∓6.2,-2.6,0) | Cube ×2.3, rot (-20,±35,0), Title_CubeLeft/Right + TitleFX_Character | (3D) | rotateFrom (90,±200,±60) |
| (both characters) | | | | dur 0.5, InCubic, fadeIn off, moveFrom (0,10,0), glitchIn 0.5, flashOnLand 0.6, shakeOnLand 0.35, squash 0.22, float (0,0.08,0) @0.4Hz, flashOnExit 0.6, glitchOnExit 0.7 |
| PressStart | (0,-1.5,0) (user: a little below centre) | Sprite Height 1.1, FX + TitleFX_Logo | 30 | delay 1.5, dur 0.5, OutBack, scaleFrom 0.6, pulse 0.03 @0.8Hz, blinkMinAlpha 0.35 @0.8Hz, exit BlinkOut 0.6s, glitchOnExit 0.8 |
| FadeCanvas/Fade | | Overlay canvas order 1000, full-stretch Image (raycast off, disabled in edit mode) + `TitleScreenFader` | | |
Character choice in the builder (`CreateCharacterElement`, 2026-10-22): a kept `Model` that is NOT the placeholder cube (`IsPlaceholderCube`: MeshFilter mesh "Cube" and material named `Title_Cube*` or none) is kept as before; otherwise `DefaultCharacter(rightSide, keptSprites)`: the kept sprite of the `Sprite` child (SpriteKey = parent name; its roster entry's height/offset if the roster has that sprite, else roster entry 1/2's values) or the roster's 1st (left) / 2nd (right) usable entry → child `Sprite` (`CharacterSortingOrder` 10 = behind Logo 20); otherwise the cube. The builder also sets `controller.characters` (RosterPath `Assets/Title/Resources/TitleCharacters.asset`), `characterLeft`, `characterRight`. A replaced cube is destroyed with the other leftovers, and after a "keep values" snapshot apply its `rotateFrom` is reset to (0,0,±25) (a flat sprite tumbling on X/Y would show its edge/back). Snapshot paths `CharacterX/Model` no longer exist, so their transforms are simply not applied.
Controller wiring (opening gets `cameraMotion`; no fader any more): `opening`, `background`, `afterOpening = [Logo, CharacterLeft, CharacterRight, PressStart]`, `pressStart`, `ambientParticles = [Bubbles, Germs]`, `fader`, `cameraMotion`. Opening gets `cameraMotion` + `fader`.
Depth: cubes (opaque, z≈0) occlude Background (z 5) / particles (z 3–4) by depth; sprites at z 0 sort by sortingOrder.

## 3. Runtime API (ns `MS2026.Title`)
```csharp
// TitleScreenController
string gameSceneName = "Lobby"; Key startKey = Key.A;    // Keyboard.current[startKey].wasPressedThisFrame
// [握力センサーでスタート] (2026-10-12, user: "一定以上握ったらすぐ")
bool startByGrip = true; float gripStartThreshold = 0.35f [.05..1]; float gripReleaseThreshold = 0.2f [0..1];
//   StartPressed() = key pressed || GripPressed(). GripPressed: g = max(GripInputBridge.Provider.GetGripValue(0..3)) (this PC's sensor is index 0; Q/W/O/P simulator keys also count),
//   GripEdgeDetector(start, min(release, start)); returns true only on the rising edge. Not armed until g <= release once (a grip held when the title appears is ignored).
//   Sampled every frame in Intro/Ready (not while Starting). The asmdef references MS2026.GripInputBridge.Runtime.
string directSceneForTestLaunch = "Game";               // loaded in Awake when a "-fortress-…" arg is present (empty = never skip)
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
//   with tubeSequence: Idle → Waiting(appearDelay) → Sequence → Ejecting → … (Appearing/Swelling skipped)
TitleFrameSequence tubeSequence;   // [Header 連番アニメ（After Effects などの絵）] null/empty → procedural tube (below)
// Sequence mode (UseSequence = tubeSequence usable && tube renderer found): Awake does NOT slice; ResetToStart shows frame 0 and hides `cap`
//   (the cap is in the art; LaunchCap is skipped). BeginSequence: tube pose = base (scale 1, no pop-in, no tremble), ZoomTo(zoomIn, EjectTime, InOutSine), onPinch at tube.
//   Sequence stage: frame = FrameAt(sequenceTime) → tubeRenderer.sprite; rumble = rumble*grip with t = sequenceTime/EjectTime (same grip curve);
//   Eject when frame >= ejectFrame (or last frame). After eject the frames keep playing and hold the last one; the stream base follows
//   tubeRenderer.TransformPoint(MouthAt(frame)) + streamBaseOffset every frame; no deflate/shape. Unused in this mode: appearDuration, swell*/pinch*/tubeTremble/pincherTravel/sliceCount, cap*.
Transform tube; Transform cap; Transform[] pinchers; Transform stream; ParticleSystem burstParticles; RectTransform curtain; TitleCameraMotion cameraMotion;
float appearDelay = 0.3f, appearDuration = 0.45f;                       // tube scale 0→1 OutBack
float swellDuration = 2f; float swellAmount = 0.4f; float pinchAmount = 0.5f [0..0.9]; float pinchHeight = 0.45f [0..1]; float pinchWidth = 0.08f;
Vector2 swellRange = (0.06, 0.84); float swellStretch = 0.08f; float tubeTremble = 0.05f; float pincherTravel = 0.35f;
float zoomIn = 0.15f; float rumble = 0.12f; int sliceCount = 48 [8..96];
//   grip = InCubic(t)*0.65 + t*0.35. Each slice width = 1 + swell*bulge(v)*(1-g) - pinch*g, bulge = sin(pi*body)^0.6 inside swellRange,
//   g = exp(-((v-pinchHeight)/pinchWidth)^2), v = slice centre 0 (bottom) .. 1 (top). Tube scaleY = 1 + swellStretch*grip. Pinchers (optional, builder leaves the array empty — user removed the fingers) move toward x=0 by pincherTravel*grip.
float capLaunchSpeed = 16f; float capLaunchSpread = 3f /*±x speed*/; float capSpin = 900f /*deg/s, random sign*/; float capGravity = 20f;
float ejectDuration = 0.4f /*stream 0→1 OutCubic*/; Vector2 streamBaseOffset = (0,-0.04) /*world units added to the mouth when placing the stream base (Eject + every frame in sequence mode); default sinks it 4px into the nozzle because the measured mouth is the rim outline's top pixel, which looked detached (user 2026-10-17)*/; float ejectShake = 0.4f; float pullBackDuration = 0.5f /*ZoomTo(0, OutBack)*/; float deflateDuration = 0.35f /*swell → -0.1*/;
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

// TitleCharacterRoster : ScriptableObject [CreateAssetMenu "MS2026/タイトル/タイトルのキャラ一覧"], ResourcePath "TitleCharacters" (2026-10-22, user: random 2 characters each time the title opens)
bool randomize = true; Entry[] characters;   // Entry { string label; Sprite sprite; float height = 10.35 [Min .1]; Vector2 offset = (0.3, 1.1) /* x = toward the screen edge, y = up */ }
static Vector3 LocalPosition(Entry, bool rightSide) /* (right ? x : -x, y, 0) */; static Vector2 OffsetFrom(Vector3 localPos, bool rightSide);
static (int left, int right) PickPair(int count, Func<int,int> randomRange) /* two distinct, uniform over ordered pairs; count < 2 → (-1,-1) */;
int[] UsableIndices() /* entries with a sprite */; static bool ApplyTo(Transform slot, Entry, bool rightSide) /* slot's child "Sprite": sprite, localPosition, TitleSpriteFit Height + size + Fit(); false if no SpriteRenderer child (3D Model) */
// TitleScreenController additions: [Header キャラ（開くたびにランダム）] TitleCharacterRoster characters (null → Resources.Load), TitleElementMotion characterLeft / characterRight
//   (null → afterOpening element named CharacterLeft / CharacterRight). Awake (after the test-launch check) → PickCharacters(): if roster.randomize and ≥ 2 usable,
//   PickPair with UnityEngine.Random and ApplyTo both slots. Public: Roster, CharacterSlot(bool rightSide). Title.unity was NOT edited for this (fallbacks find everything).
//   TitleElementMotion computes its off-screen start at PlayEnter from the children's localBounds (pose independent), so the swapped sprite's size is used.

// TitleFrameSequence : ScriptableObject (Assets/Title/Sprites/ToothpasteFrames.asset, made by the importer)
Sprite[] frames; float frameRate = 30 [Min 1]; int ejectFrame = 82 [Min 0];
Vector2[] mouths;      // per frame, tube top (mouth) relative to the sprite pivot (centre), 100px = 1 (hidden in its inspector)
Vector2 screenOffset;  // crop centre relative to the exported frame centre, 100px = 1 (used by placeLikeSource)
int Count; bool IsUsable; float Duration; float EjectTime /*clamped ejectFrame / fps*/; int FrameAt(float s) /*clamped, holds last*/; Vector2 MouthAt(int f) /*fallback: sprite top-centre*/;

// TitleElementMotion  [DisallowMultipleComponent] — starts Dormant (child Renderers disabled) until PlayEnter/SkipToIdle
// 2026-10-22: renderers stay hidden during Waiting (enterDelay) too and are shown when Entering starts (was: shown at PlayEnter, so a big
//   portrait parked at moveFrom (0,10,0) poked into the screen before falling). bool startOffscreen = true (after moveFrom):
//   PlayEnter → CaptureRestCorners(): 8 corners of every child renderer's localBounds (particles excluded), mapped through the rest pose
//   (parent matrix × TRS(basePosition, baseRotation, baseScale)) relative to the rest origin — independent of the current pose;
//   then enterFrom = OffscreenMoveFrom(): with Camera.main (orthographic) and the start pose (rotateFrom, scaleFrom),
//   the smallest factor along the move's x/y components that puts the whole box past the view edge; if > 1, moveFrom × factor × 1.03,
//   else moveFrom unchanged (direction always kept). EnterPose lerps from enterFrom. Not applied: moveFrom zero, no renderers, perspective camera.
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
- Menu: `Tools/タイトル/握力センサーの読み取りを置く` (2026-10-12): opens Title.unity, `EnsureGripReader`, saves. Refuses in Play Mode.
- Menu: `Tools/タイトル/タイトルのキャラ一覧を開く` (2026-10-22): selects + pings `TitleCharacters.asset`. Its inspector (`TitleCharacterRosterEditor`): help box, `開くたびにランダム`, `キャラ` list, then (needs a loaded `TitleScreenController`, else `タイトルのシーンを開く`) `今の左のキャラの大きさ・位置を覚える` / `今の右の…` (finds the entry whose sprite == slot sprite; writes TitleSpriteFit.size → height, OffsetFrom(localPosition) → offset; Undo; SaveAssetIfDirty; works in Play) and per entry `左で見る` / `右で見る` (ApplyTo the slot with Undo, marks the scene dirty in edit mode, selects the slot's `Sprite`).
- Menu: `Tools/タイトル/キャラを立ち絵にする` (2026-10-22, `SwapCubesToPortraitsMenu` → `SwapCubesToPortraitsInTitleScene()`): no rebuild. Opens Title.unity additively if it is not loaded (closed again afterwards), runs `ConfigureSpriteImports`, and for `CharacterLeft` / `CharacterRight` whose `Model` is the placeholder cube (and no `Sprite` child yet, portrait PNG present) destroys the cube, adds the portrait `Sprite` child (`CreatePortraitChild`, same constants as the builder) and sets `rotateFrom` (0,0,±25). Everything else (positions, timings) is untouched. Saves the scene unless it was already open with unsaved changes (then only marks dirty + LogWarning). Logs `[Title] 仮のキューブ N 個を立ち絵に替えて、…`. Refuses in Play Mode; dialog with the result.
- Menu: `Tools/タイトル/タイトルシーンを組み立てる`. Refuses in Play Mode. Also runs `EnsureGripReader` (the `Arduino` root is outside `[Title]`, so rebuilding never deletes it). Opens `Assets/Scenes/Title.unity` (asks to save the current scene). Rebuild dialog: `数値を引き継いで作り直す` / `やめる` / `初期値で作り直す` (old layout: `作り直す` / `やめる`).
- The builder also calls `TitleFrameSequenceImporter.ApplyToOpening(opening, seq, placeLikeSource: true)` when `Assets/Title/Sprites/ToothpasteFrames.asset` exists (with "keep values", the snapshot then restores the tuned tube position/fit afterwards; `tubeSequence` is an object ref, so the fresh one is kept).
- Menu: `Tools/タイトル/歯磨き粉の連番を取り込む` (2026-10-17, `TitleFrameSequenceImporter`). Refuses in Play Mode. Folder picker (last folder in EditorPrefs `MS2026.Title.ToothpasteFramesFolder`) → all `*.png` sorted by name; fewer than 2 → error; gaps in the trailing numbers → confirm dialog; different sizes / all transparent → error. Pass 1 finds the union bbox of alpha > 0 over all frames (+2 px), pass 2 crops every frame to it and writes `Assets/Title/Sprites/ToothpasteFrames/<same name>.png` (overwriting keeps GUIDs; old files with other names are deleted), loading one PNG at a time. Per frame: mouth = scan the centre column band (bbox centre of alpha >= 128, ±2 px) upward from the lowest solid row until a 6-row gap; any solid pixel 6+ px above that = detached object → the first such frame becomes `ejectFrame` (detected 82 for the 2026-10-17 delivery; if none, the old value stays). Importer settings: Sprite/Single/100 PPU/Center/FullRect/no mips/Clamp/alphaIsTransparency. Saves `ToothpasteFrames.asset` (creates it; keeps `frameRate`). Dialog `書き出したときと同じ位置・大きさ` (ApplyToOpening placeLikeSource true) / `シーンには入れない` / `今の位置・大きさのまま` (only sets `tubeSequence`, the preview sprite, hides the cap). Opens Title.unity, needs an existing `TitleOpening` (else tells the user to build first), undoable, saves the scene, selects the asset.
- `TitleFrameSequence` inspector: summary help box (`N 枚 ・ X 秒。キャップが飛ぶのは Y 秒の所。`) + frames/frameRate/ejectFrame (mouths/screenOffset hidden). Controller overview `① オープニング` also lists `噴き出す根元のずれ` (streamBaseOffset), and shows `連番アニメ（AE）` and, when set, `連番の1秒の枚数` / `キャップが飛ぶコマ` (these edit the asset, so Play-mode changes persist without the keep button).
- Snapshot covers component types TitleScreenController, TitleOpening, TitleElementMotion, TitleSpriteFit, TitleCameraMotion, ParticleSystem (EditorJsonUtility). Not covered: ShaderFX EffectTarget, renderers/materials (assets keep their own edits), Camera component. Saves the scene. Logs `[Title] タイトルシーンを組み立てました。…`.
- Inspector: Japanese `[Header]`s (`登場`, `待機中のゆらぎ`, `スタート時（退場）`, `遷移`, `オープニング（歯磨き粉）`, `配置物（…）`, `画面全体の演出`, `エフェクト・効果音（D-Drive）`, on TitleOpening `配置物`, `連番アニメ（After Effects などの絵）`, `出てくる`, `つまんでふくらむ`, `噴き出す`, `塗り替わる`) and Japanese tooltips; enum values have Japanese `InspectorName`s. `FortressEffect` slots get the Fortress drawer (`▶ 試す` etc.).

## 5. Integration
- Lobby scene loads normally (single mode); it owns the network stack and later adds Game on top. Title has no NetworkManager / D-Drive bootstrap. Going back to the title from the lobby/game (`GameSession.GoToTitle`) shuts networking down and loads Title single (Title then fades in from black as usual).
- Grip input (2026-10-12, needed for the exhibition — no keyboard at the venue): the controller reads `GripInputBridge.Provider` (`startByGrip`, §3). The real sensor is read by a root `Arduino` object with `ArduinoSerialReader` (`keepAcrossScenes` on, `portName` COM5, `autoFindPort` on) in `Title.unity`, placed by `TitleSceneBuilder.EnsureGripReader` — run from the builder and from the menu `Tools/タイトル/握力センサーの読み取りを置く` (no rebuild; creates it if missing, else only turns `keepAcrossScenes` on; the type is found by name `ArduinoSerialReader, Assembly-CSharp` because asmdefs can't reference Assembly-CSharp). It persists across scenes so Lobby/Game readers stand down (`GripInputBridge_AI_Reference.md` §6). **Until that menu is run once, Title.unity has no reader → only the keyboard simulator works on the title.** Idea (not implemented): the pinch/swell could later be driven by the real grip value. Old note: call `TitleScreenController.RequestStart()` from a grip threshold (see `GripInputBridge_AI_Reference.md`). The pinch/swell could later be driven by the real grip value (idea, not implemented).
- ShaderFX: `EffectTarget` on sprites (Uber Sprite) and on the cubes (3D Uber). Module classes are in `MS2026.ShaderFX.Modules` (editor code needs that `using`).

## 6. Gotchas
1. Profiles/materials are created only if missing: to apply new builder defaults, delete `Assets/Title/Effects/*.asset` (and/or materials) and rebuild. Avoid hand-editing `Title.unity`. When changing builder defaults, tell the user to rebuild with `初期値で作り直す` (keep would re-apply their old numbers over the new defaults).
9. Values changed during Play revert on stop unless `再生中に変えた値を残す` was pressed; positions can't be tuned in Play (motion overwrites transforms every frame).
2. Replacing / adding a character: add a Sprite-type PNG to `Assets/Title/Sprites/` and an entry to the roster (`TitleCharacters.asset`); with `randomize` on, the scene's `Sprite` children are overwritten at Play start, so tune per-character values in the roster (inspector buttons `左で見る`/`右で見る` → adjust the slot's `Sprite` (TitleSpriteFit.size, localPosition) → `今の左（右）のキャラの大きさ・位置を覚える`; works in Play too and persists because the roster is an asset). Tuning the scene child alone is lost on the next random pick. 3D = delete the `Sprite` child and add a child named `Model` (kept on rebuild, wins over the portrait unless it is the placeholder cube). Adding the PNGs alone does not change Title.unity: the cubes stay until `キャラを立ち絵にする` (or a rebuild) runs — this bit the user on 2026-10-22 (the scene had never been rebuilt).
3. `TitleSpriteFit` CoverCamera needs an orthographic camera.
4. Particle materials use the URP 2D Sprite-Unlit shader; falls back to `Sprites/Default` if not found.
5. `FortressEffect` slots in Title are silent until a D-Drive bootstrap exists in the Title scene (see §0).
6. Dissolve noise is world-space, so moving/scaling while dissolving slides the pattern (only used if `burnIn` is on; off by default now).
8. Symptom "objects don't show in Play" = the scene was built by an older builder (e.g. no `Opening`/`afterOpening`): rebuild with the menu. The adopt-unlisted fallback makes them appear anyway, but without the opening.
7. Squash pivots around the object centre, so the bottom lifts slightly during the squash (acceptable for placeholders).
10. AE sequence: the mouth positions (stream base) and `screenOffset` are measured at import time. After the artist re-exports, re-run the import menu; copying PNGs in by hand leaves them stale. The PasteStream sprite (~1.4 world wide) is much wider than the AE tube's nozzle (~0.26), so tune `Opening/PasteStream` scale if it looks off. Clearing `tubeSequence` brings the procedural tube back; the Cap GameObject is inactive in the scene but the runtime `ResetCap` re-activates it.

## 7. Related
`ShaderFX_AI_Reference.md` (EffectTarget/profiles), `FortressDesigner_AI_Reference.md` §Effects (`FortressEffect`), `DDrive_AI_Reference.md` (bootstrap), `GripInputBridge_AI_Reference.md` (future start input).
