# Blue Skill (青スキル「うがい薬の渦」) — AI Agent Reference

> Cold-start brief for Claude Code. Written **2026-10-19** together with the code (branch `今川_青キャラクタースキル`). Compiled offline with dotnet against `Library/ScriptAssemblies` (runtime with and without `UNITY_EDITOR`, editor assembly: 0 errors). **Verify inside Unity first** (shaders, Burst job, visuals were not run in the editor when this was written — check the Console).
> Human guide: `Docs/Tools/BlueSkill_ガイド.html`. Maintenance rule (`CLAUDE.md`): when you change anything under `Assets/BlueSkill/`, update both files in the same task. Style: `Docs/Tools/DOCS_GUIDE.md`.
> **Not yet listed** in `CLAUDE.md`, `Docs/Tools/DOCS_GUIDE.md` and `Docs/Tools/index.html` (shared files left untouched on the feature branch to avoid merge conflicts — add the rows when merging, as was done for the red skill).
> Builds on the red skill's generic framework — read `RedSkill_AI_Reference.md` §0–2 for `PlayerSkillSlot` / `PlayerSkillInstaller` / `CharacterSkillTable`.

## 0. Mental model
- Skill asset `BlueVortexSkill` (subclass of `CharacterSkill`, asset `Assets/BlueSkill/Data/BlueVortexSkill.asset`). It uses the **shared** slot/installer/table from `Assets/RedSkill/Scripts/Runtime/Core` (no copy, no edit).
- Flow (user request 2026-10-19): smash ball broken by this player → slot `Ready` → a blue gargle bottle (うがい薬) appears above the turret with charge FX → the **next transition into `TurretState.Firing`** (next attack) starts the skill; if the charge arrived while firing, the current laser fires to the end, then the next attack is the skill (slot `_waitForRelease`, unchanged red logic).
- **Throw modes (`multi.mode`, `BlueThrowMode`; 2026-10-24)**. User: 「スキル三個一気に投げるみたいなことできる？一応戻せるようにしておいて」 then 「一気に３つ投げるのじゃなくて、３つあってひとつづつ投げる」 (answer: one smash ball = 3 bottles, one per attack).
  - `Single` 「1本だけ（元の動き）」 = the original behaviour (**revert = pick this**).
  - `AllAtOnce` 「まとめて一気に投げる」: holds `count` bottles fanned out, one marker per landing point, throws all on the next attack (centre first, outer ones `staggerSeconds` later), each vortex `area.radius × radiusScale`.
  - `OneByOne` 「1本ずつ順番に投げる」 (**default**): one charge = `count` (3) throws. Held bottles = bottles left (3 → 2 → 1, fanned). Each attack throws one bottle from the centre at the aim (full radius, one marker). The runner calls `NotifyFinished()` right at the throw and, if bottles remain, `Slot.GrantCharge()` (with `_rearming` so no Ready burst/SE and no refill) → the slot is Ready again with `_waitForRelease` because the turret is still firing → release and grip again for the next bottle. The flying bottle is independent of the held ones. A smash ball broken while still Ready does nothing (the slot ignores charges while Ready), so bottles are not refilled until all are thrown.
- Skill: windup (bottle pulled back, shakes) → bottle thrown along a real 3D parabola (towards the camera = −Z) in the aim direction → bursts where it lands → spawns a `BlueVortexArea` (water whirlpool debuff area) that lives on its own for `area.durationSeconds`, then collapses. The slot is `Active` only during windup + flight (~0.9 s); `NotifyFinished()` is called at landing, so the normal laser comes back while the vortex is still up.
- Debuff (all applied by `BlueVortexField`, host/offline only): **hold** (enemies stay in place), **swirl** (pushed around the vortex), **pull** (towards the centre, not inside `pullInnerRadius`), **damage taken ×** (amplifies other damage), **tick damage** (every `tickInterval`, credited to the thrower).
- Landing point = `BlueVortexSkill.PredictLanding(turret)` (distance / side offset / measure from muzzle / optional stop at walls). Shown in game as a dashed ring + dotted arc while Ready, and in the Scene view when the asset is selected.
- **No existing file was modified.** Integration via public API only: `PlayerSkillSlot`/`CharacterSkill`/`CharacterSkillRunner` (red Core), `LaserTurret.AimDirection / MuzzlePosition / rotationSpeedScale / playerIndex`, `SwarmSystem.Current / StorageReadable / Storage (pos, vel, hp, speedScale, netId, lastHit) / IsReplica / QueueBeam`, `EnemyController.Active / ApplyLaserDamage / IsReplica`, `FortressNet.HasSimulationAuthority`, `FortressEffectPlayer`, `FortressCameraRig.Active.AddModifier(SimpleShakeModifier)`, `BillboardIgnore`.

### Network (LAN, host-authoritative)
- No new traffic. Every PC runs the slot + runner + area visuals itself (smash-ball break and turret state already replicate). The landing point is computed on each PC from its replicated aim, so it can differ slightly between PCs.
- Effects on enemies only where `FortressNet.HasSimulationAuthority && !swarm.IsReplica`; clients see held / swirling enemies through the normal position snapshots. Tick flash = a 0-dps `QueueBeam` (radius = vortex) queued on every PC → white hit flash everywhere. The field also runs on clients in read-only mode to collect "trapped enemy" sample positions for bubbles.

## 1. Paths / asmdefs
```
Assets/BlueSkill/
  Scripts/Runtime/  MS2026.BlueSkill.Runtime (rootNamespace MS2026.Skills.Blue; refs MS2026.RedSkill.Runtime, MS2026.Fortress.Runtime, DDrive.Runtime, DDrive.Foundation, Unity.Burst, Unity.Mathematics, Unity.Collections)
    BlueVortexSkill.cs        SO + settings classes (BlueThrowTiming/ThrowTarget/VortexAreaSettings/VortexDebuff/VortexLook/BottleSettings/ChargeFx/BurstFx/CameraShake/SkillSounds/SkillShaders) + PredictLanding
    BlueVortexSkillRunner.cs  runner (windup → flight → land → spawn area)
    BlueThrowVisuals.cs       internal: bottle, charge FX, landing marker, flight FX
    BlueBottle.cs             internal: SpriteRenderer bottle + ground shadow; procedural placeholder sprite
    BlueVortexArea.cs         MonoBehaviour per vortex (burst → expand → hold → collapse → linger), visuals, ticks
    BlueVortexField.cs        MonoBehaviour singleton applying the debuff + BlueVortexZone struct + BlueVortexJob (Burst IJob)
    BlueVortexVisual.cs       internal: one vortex quad (Vortex shader)
    BlueFx.cs                 internal helpers: quad, materials, renderers, particles, BlueGlow, BlueRingPool, BlueSpiralParticles, Shake
  Scripts/Editor/   MS2026.BlueSkill.Editor (ns MS2026.Skills.Blue.EditorTools; refs blue runtime, red runtime, Fortress runtime)
    BlueSkillMenu.cs          menus (§5)
    BlueSkillSceneGuide.cs    [InitializeOnLoad] Scene-view guide (§5)
    SkillAssignWindow.cs      colour-agnostic window `Tools/スキル/スキルの割り当て（プレイヤー・キャラ）` (§5); asmdef also refs MS2026.CharacterSelect.Data
  Shaders/  BlueSkillVortex.shader ("MS2026/BlueSkill/Vortex") · BlueSkillGlow.shader ("MS2026/BlueSkill/Glow") · BlueSkillParticle.shader ("MS2026/BlueSkill/Particle") · BlueSkillCommon.hlsl
  Data/     BlueVortexSkill.asset (only skillId/displayName/description/themeColor/base flags/shaders serialized; the rest = C# initializers until Unity re-saves it)
  Prefabs/  BlueSkill_Installer.prefab ("[BlueSkill] Skill Installer": PlayerSkillInstaller with defaultSkill = BlueVortexSkill.asset, characterTable = Assets/RedSkill/Data/CharacterSkillTable.asset, useCharacterSelection on, enableTestKeys on)
Docs/Tools/BlueSkill_AI_Reference.md, BlueSkill_ガイド.html
```
- Test scene `Assets/Scenes/blueskill_test.unity` (user-made copy of the game setup, untracked at writing time). The installer is **not** in the scene file — the user adds it with the menu (the editor had the scene open, so the YAML was not edited).
- Editor inspectors come from the red editor assembly: `CharacterSkillEditor` (`[CustomEditor(typeof(CharacterSkill), true)]`) draws `[SkillLabel]` Japanese labels, foldout groups (any `[Serializable]` class in a `MS2026.Skills*` namespace) and Play-mode test buttons (`使えるようにする` / `今すぐ撃つ` / `止める`) for the blue asset too.

## 2. Public API
```csharp
// MS2026.Skills.Blue
[CreateAssetMenu("MS2026/スキル/ブルー：うがい薬の渦")] sealed class BlueVortexSkill : CharacterSkill {
  timing, target, area, debuff, look, bottle, charge, burst, cameraShake, sounds, shaders;   // ctor: skillId "blue_vortex", themeColor (0.2,0.55,1)
  Vector3 PredictLanding(LaserTurret);   // floor point (z = turret z); centre bottle when multi-throwing
  int PredictLandings(LaserTurret, List<Vector3> results);   // all landing points, left → right (1 when multi is off)
  int ThrowCount /* bottles per attack */; int ThrowsPerCharge /* attacks per charge */; float AreaRadiusScale; }
enum BlueThrowMode { Single, AllAtOnce, OneByOne }   // [InspectorName] 1本だけ（元の動き）/ まとめて一気に投げる / 1本ずつ順番に投げる
sealed class BlueMultiThrow { const int MaxCount = 7; BlueThrowMode mode = OneByOne; int count = 3; float spreadAngle, outerDistanceScale, staggerSeconds, radiusScale, extraShakeScale; int ThrowCount, ThrowsPerCharge; }
[DefaultExecutionOrder(10)] sealed class BlueVortexSkillRunner : CharacterSkillRunner {
  static event Action<BlueVortexSkillRunner, BlueVortexArea> AnyVortexSpawned;
  BlueVortexSkill Definition; BlueVortexArea LastArea; int BottlesLeft; Vector3 CurrentTarget; IReadOnlyList<Vector3> CurrentTargets;
  Vector3 PredictLanding(); IReadOnlyList<Vector3> PredictLandings(); }
sealed class BlueVortexArea : MonoBehaviour {
  static IReadOnlyList<BlueVortexArea> Active; static event Action<BlueVortexArea> AnySpawned;
  static BlueVortexArea Spawn(BlueVortexSkill, Vector3 floorCenter, int ownerPlayerIndex, float radiusScale = 1, float shakeScale = 1);   // usable without a runner
  BlueVortexSkill Definition; int OwnerPlayerIndex; Vector3 Center; float BaseRadius /* area.radius × radiusScale */; float CurrentRadius; float Age; bool IsFinished;
  void Dismiss();                       // start collapsing now
  bool TryGetZone(out BlueVortexZone); }
[DefaultExecutionOrder(-50)] sealed class BlueVortexField : MonoBehaviour {   // auto-created "[BlueSkill] Vortex Field", self-destroys when idle
  static int AffectedCount; static IReadOnlyList<Vector2> TrappedSamples; }
struct BlueVortexZone { float2 center; float radius, softWidth, strength, hold, swirl, pull, pullInner, direction, damageTakenMultiplier, tickDamage; int owner; }
```
- Character binding: add a row to `CharacterSkillTable` (character → `BlueVortexSkill`) — easiest via the assignment window (§5) — or `PlayerSkillSlot.Find(i).SetSkill(blueAsset)`. Nothing blue-specific is needed. Red and blue (and later yellow/green) coexist in one scene: one installer, different skills per seat.

## 3. BlueVortexSkill data (defaults; Inspector label in brackets)
| group.field | default | meaning |
|---|---|---|
| timing.windupSeconds / flightSeconds | 0.35 / 0.55 | 振りかぶる時間 / 飛んでいる時間 |
| timing.arcHeight / spinTurns / rotationSpeedScale | 2.4 m / 1.75 / 0.3 | parabola peak (real −Z height); bottle spins; turret rotation × during windup (restored at throw) |
| target.distance / sideOffset / measureFromMuzzle | 7 m / 0 / true | 落ちる距離 / 左右のずれ (+ = right of aim) / 発射口から測る (else turret centre) |
| target.stopAtWalls / wallPadding / wallLayers | false / 0.5 / ~0 | 壁の手前で落ちる: `Physics2D.Linecast` muzzle→goal (non-trigger, own turret ignored), lands `wallPadding` before the first hit |
| multi.mode / count | OneByOne / 3 (1–7) | `投げ方` / `ボトルの数` (group label `投げ方（1本・まとめて・1本ずつ）`). ThrowCount = AllAtOnce ? count : 1; ThrowsPerCharge = OneByOne ? count : 1; AreaRadiusScale = radiusScale only when ThrowCount > 1. The next 5 fields sit under the header `まとめて一気に投げるとき` |
| multi.spreadAngle / outerDistanceScale | 32° / 0.85 | angle between neighbours (+ = right, same sign as sideOffset); outermost distance × 0.85, interpolated by \|offset\|/centre (arc shape). Defaults at 7 m: centre (0,7), sides ≈ (±3.15, 5.05) → ~3.7 m apart |
| multi.staggerSeconds / radiusScale / extraShakeScale | 0.06 s / 0.75 / 0.35 | release delay = ceil(\|i − centre\|) × stagger; each vortex radius × 0.75 (2.4 m); 2nd+ vortices' burst/active/collapse shakes × 0.35 |
| target.showMarker / markerColor / markerDots | true / HDR (0.35,0.85,1.6,.55) / 14 | dashed ring (radius = area.radius) + centre glow + dotted arc flowing from the bottle (Ready/windup only) |
| area.radius / durationSeconds / expandSeconds / collapseSeconds | 3.2 / 5 / 0.45 / 0.6 | 渦の半径 / 続く時間 / 広がる / 消える |
| area.expandCurve / warnSeconds / affectWhileExpanding | overshoot 0→1.12@0.6→1 / 1 / true | radius × curve while expanding; rim blinks faster during the last warnSeconds; effects apply from the burst |
| debuff.holdStrength / edgeSoftness | 1 / 0.2 | speedScale × (1 − hold·weight); weight = smoothstep over the outer `edgeSoftness × radius` × zone strength |
| debuff.swirlSpeed / clockwise / pullSpeed / pullInnerRadius | 1.4 m/s / false / 0.7 m/s / 0.3 | velocity lerped towards tangent·swirl − dir·pull (pull only outside pullInner·radius); the visual spin uses the same direction. Swarm finalize caps speed at 1.6 × enemy maxSpeed |
| debuff.damageTakenMultiplier | 1.5 | extra = (hp drop since last frame) × (mult − 1), weighted; swarm only; the vortex's own tick is not amplified |
| debuff.tickDamage / tickInterval / tickFlash | 1.5 / 0.5 s / true | flat damage per tick (sets `lastHit` = owner → kill credit), plus 0-dps flash beam on every PC. Swarm enemy hp 10, boss 150 |
| debuff.lingerSeconds / affectActors | 0.4 / true | after the collapse the hold fades 1→0 (no swirl/pull); Actor enemies get hold + swirl + tick (no damage-taken multiplier) |
| look.radialGradient (HDR) | near-black navy → navy → blue → cyan → light cyan; alpha .95/.9/.75/0 | 渦の色（中心→ふち）, baked to a 64-px RGBAHalf texture (linearised) |
| look.foamColor / rimColor / eyeRingColor | HDR light blue / cyan / white-blue | additive streaks, rim ring, eye ring |
| look.arms / twist / spinSpeed / armSharpness / turbulence / eyeSize | 4 / 4.5 / 0.55 rev/s / 2.6 / 0.45 / 0.17 | shader: twirl = dir·(twist·(1−r)² + 0.35·twist·(1−r) + 6·suck·(1−r)) + rotation; arms = cos(atan2·arms + noise) |
| look.opacity / intensity / counterLayer | 0.85 / 1.2 / true | premultiplied blend (0 = additive only, 1 = paints the floor); counter layer = 0.62 × radius, opposite spin, 45 % opacity |
| look.spiralDropletRate / bubbleRate / rimSparkleRate / rippleInterval / trappedBubbleRate | 55 / 26 / 40 /s / 0.55 s / 30 /s | droplets spiralling into the eye (`BlueSpiralParticles`), rising bubbles, rim sparkles, inward ripple rings, bubbles at trapped enemies (`BlueVortexField.TrappedSamples`) |
| look.particleBrightness / particleColor / floorLift | 2.2 / (0.55,0.85,1) / 0.03 m | |
| look.vortexRenderQueue / overlaySortingOrder | 2980 / 150 | vortex + marker/ripples (2981) draw **under** swarm enemies (2990); bursts/particles at 3100 |
| bottle.sprite / tint / length / width | empty / white / 1.6 / 0.95 | empty → procedural placeholder (`BlueBottle.PaintBottle`, 112×208): ink outline with cyan/violet offset copies (same toon style as the red tube), blue liquid with bubbles, white label with a water drop + wave, ridged navy cap, right-side shade, left shine. Custom sprite: cap/mouth pointing up |
| bottle.forwardOffset / sideOffset / holdHeight / holdTilt | 0.2 / 0.55 / 0.6 m / −20° | held position relative to the turret (height = towards camera) |
| bottle.bobAmount / bobFrequency / sloshAngle | 0.08 / 1.1 / 5° | idle float + slosh wobble |
| bottle.windupPullBack / windupShake / appearSeconds / sortingOrder / shadowOpacity | 0.45 / 0.05 / 0.3 / 160 / 0.4 | windup: pulled back + raised 0.35 m + tilted +35° + ×1.15; ground shadow only in flight (renderQueue 2985) |
| charge.auraColor / auraScale / pulseFrequency / pulseAmount | HDR (0.25,0.8,2.2) / 2.1 / 1.6 / 0.3 | glow behind the bottle (renderQueue 2995 = above enemies, under the bottle sprite) |
| charge.miniVortexRadius / miniVortexOpacity | 1.7 / 0.6 | small vortex under the turret while Ready (same shader/look) |
| charge.orbitRate / orbitRadius / bubbleRate / rippleInterval / readyBurstSize | 34 /s / 2.2 / 14 /s / 0.8 s / 5 m | droplets orbiting into the bottle, bubbles, ripples, rings on Ready |
| charge.windupMultiplier / particleColor / particleBrightness | 3 / light blue / 2.4 | windup strengthens rates/spin/glow |
| burst.throwSplashCount / trailRate / trailGlowSize | 26 / 110 /s / 1.8 | hand splash on throw; droplets falling from the flying bottle; glow around it |
| burst.flashScale / flashColor / crownSpikes | 1.5 / HDR / 14 | burst flash with water-crown spikes (Glow shader rays) |
| burst.shockwaveCount / shockwaveScale / shockwaveColor | 3 / 1.7 / HDR | staggered rings (0.07 s apart) |
| burst.dropletCount / dropletSpeed / shardCount / bubbleCount / collapseSplashCount | 150 / 14 / 14 / 40 / 70 | + 2×crownSpikes big crown droplets flying towards the camera |
| burst.dropletColor / shardColor / particleBrightness | | |
| cameraShake.ownerViewOnly / onThrow / onBurst / whileActive(+Interval) / onCollapse | false / (0.12,0.3°,20Hz,0.2s) / (0.45,1.1°,20Hz,0.55s) / (0.04,0.15°,14Hz,0.3s) every 0.25 s / (0.2,0.5°,22Hz,0.3s) | each with a strength multiplier (0 = off) |
| sounds.onReady / onWindup / onThrow / whileFlying / onBurst / whileVortex / onTick / onEnd | empty `FortressEffect` | D-Drive VFX/SE slots (project rule); loops use `StartFollowing` (bottle transform / area transform) |
| shaders.vortex / glow / particle | the 3 shaders | fallback `Shader.Find(name)` then `Sprites/Default` with warning `[BlueSkill] シェーダー … が見つかりません` |
- All values are read every frame (gradients re-baked every 0.25 s in the editor), so Play-mode tweaking works.

## 4. Runtime details
- **Runner phases**: `Activate` → `Windup` (if windupSeconds > 0; `OverrideRotation`) → `Throw()` (targets = `PredictLandings()` at that moment, rotation restored, `_bottlesLeft` −1 (OneByOne) or 0, `visuals.Throw(targets, stagger, keepHolding, flightIds)`, onThrow SE once, whileFlying loop on the centre flight, shake). OneByOne → Idle + `NotifyFinished` + re-arm (see §0); otherwise → `Flight` until all bottles of this throw landed, then `NotifyFinished`. Every frame `LandDueBottles()` checks all bottles in the air (`visuals.IsDue(id)` = elapsed ≥ delay + flightSeconds) and spawns `BlueVortexArea.Spawn(def, target, owner, AreaRadiusScale, firstOfThrow ? 1 : extraShakeScale)` + `AnyVortexSpawned`. `Cancel` / disable → `StopEverything` (bottles in the air vanish without a vortex, bottles left = 0). `SetReadyVisual(true)` (not re-arming) refills `_bottlesLeft = ThrowsPerCharge`.
- **BlueThrowVisuals** root `[BlueSkill] Pn` (DontSave + `BillboardIgnore`, destroyed by the runner's `OnDestroy`, hidden when the runner is disabled). Modes Hidden/Ready/Windup for the **held** bottles (`HeldBottle`: bottle, marker ring, marker core; count = runner's `HeldCount` = AllAtOnce ? ThrowCount : OneByOne ? bottles left : 1), and an independent pool of **flying** bottles (`FlightBottle`: bottle + shadow, trail glow; id-based, reused). Held layout: centre at the usual hold position, others offset along the aim's right by `(i−c)·width·0.8`, pulled back `|i−c|·0.15`, tilted `−(i−c)·14°`; aura grows +20 % per extra bottle. Markers: one per landing point of the next attack; the dotted arc starts at the matching held bottle when throwing all at once, else at the centre one. `Throw` launches from the matching held bottle (all at once) or the centre one, delay = ceil(\|i − c\|) × stagger; `keepHolding` keeps the remaining held bottles visible (OneByOne). Flight position = lerp(from, to, t) with height `h0·(1−t) + 4·arc·t(1−t)` along −Z; trail droplets at `trailRate / √(bottles in the air)`; non-centre bottles get a small hand splash when released.
- **BlueVortexArea** GameObject `[BlueSkill] Vortex Pn` is a **plain scene object** (not DontSave, children reset to HideFlags.None) so it disappears when Play stops. Phases Expanding → Holding → Collapsing (visual radius shrinks with `suck`, pull ×(1+1.5t), effect radius stays full) → Lingering (hold fades over lingerSeconds; object destroyed after max(linger, 1.2 s)) → Done. Ticks only in Expanding(if affectWhileExpanding)/Holding; `_tickFrame = Time.frameCount` marks the frame read by the field.
- **BlueVortexField** (`[BlueSkill] Vortex Field`, plain scene object, created by `Ensure()` on spawn; destroys itself when no areas, no held agents, no tracked actors). Subscribes to `SwarmSystem.Current.StorageReadable` (fired inside `SwarmSystem.Update`, after `CompleteSimulation`, before scheduling — the only safe moment). Areas update at order 0, the swarm at 100, so the tick of this frame is seen.
  - `BlueVortexJob` (Burst `IJob.Run()`, single thread over all agents, ≤16 zones): per-agent memory indexed by `netId` (0–65535, persistent NativeArrays: origSpeed, prevHp, seenFrame, held). An agent is "continuous" if it was seen in the previous run (the frame counter skips one number when a Unity frame was missed, so stale memory is never used). Order per agent inside a zone: amplify hp drop → tick damage (`lastHit = owner`) → store prevHp → hold (`origSpeed` saved on entry, `speedScale = orig × (1 − hold·w)`) → velocity lerp to swirl/pull by w. Outside every zone a held agent gets `speedScale = origSpeed` back. When the last zone ends the job keeps running until nothing is held.
  - Actors: `LateUpdate`, host only — position = prev + moved·(1 − hold·w) + (tangent·swirl + pull)·w·dt; tick → `ApplyLaserDamage(tick, null, PlayerSkillSlot.Find(owner).Turret)`.
- Rendering: vortex quads `renderQueue = look.vortexRenderQueue` (2980), sortingOrder 0/1; marker + ripples 2981; ground shadow 2985; aura/trail glow 2995; bottle SpriteRenderer 3000 (sortingOrder 160); everything else 3100 with `overlaySortingOrder`+n. Glow/Particle shaders are additive; Vortex is premultiplied alpha.

## 5. Editor UI
- Menu `Tools/青スキル/開いているシーンにスキルを入れる` (instantiates the prefab, or builds it if missing; if a `PlayerSkillInstaller` already exists (e.g. the red one in Game.unity) asks 「青スキルに変える」 to set its `defaultSkill` with Undo; disabled in Play), `Tools/青スキル/スキルの設定ファイルを開く`, `Tools/青スキル/全員を使える状態にする（Play中）` (GrantCharge on every slot holding a BlueVortexSkill), `Tools/青スキル/出ている渦を消す（Play中）` (`Dismiss` all).
- Scene view (`BlueSkillSceneGuide`): while a `BlueVortexSkill` asset is selected, every `LaserTurret` shows a dotted line to each landing point (all bottles), a filled disc (radius) and the pull-inner circle, label `P1 青スキルの渦（半径 3.2m）`. In Play, active vortices are outlined with `渦 Pn  つかまっている敵 N`.
- **Skill assignment window** `Tools/スキル/スキルの割り当て（プレイヤー・キャラ）` (`SkillAssignWindow`, added 2026-10-19; user: 「赤スキルも使えるように…4種類ほど」「今はプレイヤーごとに設定しますが、後々キャラクターに紐づけするのでそれもできるように」). Works for every `CharacterSkill` asset in the project (found by `AssetDatabase.FindAssets("t:CharacterSkill")`, sorted by path — yellow/green appear automatically) and every `CharacterDefinition`. Targets the open scene's `PlayerSkillInstaller` (button `スキルを配る係をシーンに置く` if none).
  - ① `プレイヤーごと（今のテスト用）`: popups `全員に配るスキル` and `P1（Q で握る）`…`P4（P で握る）` (option `（全員に配るスキルを使う）`) write `defaultSkill` / `perPlayerSkills` via SerializedObject (Undo, scene dirty → user saves). Buttons `見つかったスキルを P1 から順に配る`, `席ごとの指定を全部消す`. In Play, `Play 中に変えたら、すぐ全砲台に配り直す` (default on) calls `InstallAll()`; Play-time edits revert when Play stops.
  - ② `キャラごと（キャラ選択を通ってきたときに使う）`: `キャラ選択の結果を使う`, `キャラとスキルの対応表` (installer fields), then one popup per character (colour swatch + `file（id: x）`) that edits the table's `entries` (update the row matching the character or id, append, or delete on `（なし）`) and saves the asset immediately (shared `Assets/RedSkill/Data/CharacterSkillTable.asset`).
  - ③ `Play 中に試す`: per player skill + state + `使えるようにする`; `キャラを選んだことにする` popups P1–P4 → `このキャラで配り直す` (`CharacterSelectionResult.Store` + `InstallAll`) / `キャラ選択の結果を消す` (`Clear` + `InstallAll`).
  - Lives in the blue editor folder only to avoid touching shared files; it can move to the shared Core folder when the branches are merged.
- Create menu `MS2026/スキル/ブルー：うがい薬の渦`. Test keys 1–4 (installer, editor / `-debug-ui` only) and inspector test buttons come from the red framework.

## 6. Gotchas
1. Not run inside Unity at writing time. Watch for Burst compile errors on `BlueVortexJob` (falls back to managed if Burst fails in the editor) and shader import errors.
2. Two installers in one scene fight (both assign skills in `Awake`). Use one installer and change its `defaultSkill` / `perPlayerSkills` / table instead — the menu does that.
3. `CharacterSkillTable.asset` lives in `Assets/RedSkill/Data` (shared); binding the blue character means editing that shared asset (coordinate with whoever owns it).
4. Heat still rises while gripping during windup/flight (turret code untouched), same as the red skill.
5. Hold works through `speedScale`; if another system later writes `speedScale` while an enemy is held, the field restores the value it saved on entry. Agents that die while held leave a stale `held` flag that is cleared (and the reused id's speed set to the old value, ±15 % variance) when the id is seen again.
6. The swirl uses the swarm's own steering/separation, so dense crowds still push; very strong `swirlSpeed` is capped by the swarm (1.6 × maxSpeed).
7. Damage-taken multiplier and tick damage only reach swarm enemies on the host; clients only see the result. Actor enemies don't get the multiplier.
8. Multi-throw: overlapping vortices add their tick damage (an enemy in two vortices gets both ticks); hold/swirl/damage-taken use the strongest zone only. `BlueVortexField` handles up to 32 zones (4 players × 7 bottles would exceed it; extra zones are ignored).
9. Landing point is decided per PC at throw time (aim replicated via snapshots) → the vortex can be slightly offset between PCs; enemy positions follow the host's vortex.
10. Other people's art / other games' footage must not be given to AI as reference (CLAUDE.md, 2026-10-13). The user attached a screenshot of a game effect as a look reference; the visuals here are procedural and original (generic whirlpool shapes), nothing was traced or fed to an image generator.

## 7. Related
- `RedSkill_AI_Reference.md` (slot/installer/table, test buttons, inspector), `FortressDesigner_AI_Reference.md` (Swarm storage/StorageReadable, LaserTurret, SmashBall, Effects, Net), `CharacterSelect_AI_Reference.md`, `DDrive_AI_Reference.md`.
