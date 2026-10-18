# Red Skill (赤スキル「極太レーザー」) + character skill slots — AI Agent Reference

> Cold-start brief for Claude Code. Written **2026-10-15** together with the code (branch `今川_赤キャラスキル`). Compiled offline against `Library/ScriptAssemblies` (runtime + editor assemblies, 0 errors); **not yet run inside Unity** (shaders, visuals, prefab/asset import unverified at time of writing — check the Unity Console first).
> Human guide: `Docs/Tools/RedSkill_ガイド.html`. Maintenance rule (`CLAUDE.md`): when you change anything under `Assets/RedSkill/`, update both files in the same task. Style: `Docs/Tools/DOCS_GUIDE.md`.
> Listed in `CLAUDE.md`, `Docs/Tools/DOCS_GUIDE.md` and the `Docs/Tools/index.html` hub (added when merging into main, 2026-10-18).

## 0. Mental model
- A **skill** = a `CharacterSkill` ScriptableObject (data + `CreateRunner`). The red skill is `RedLaserSkill` (asset `Assets/RedSkill/Data/RedLaserSkill.asset`).
- Each turret (`LaserTurret`) gets a `PlayerSkillSlot` (state machine, generic for all future skills) which creates the skill's runner component (`RedLaserSkillRunner`) on the same GameObject.
- One scene object `PlayerSkillInstaller` (prefab `Assets/RedSkill/Prefabs/RedSkill_Installer.prefab`, name `[RedSkill] Skill Installer`) adds slots to **all** `LaserTurret`s in `Awake` and assigns skills. Porting to another scene = drop the prefab (or menu `Tools/赤スキル/開いているシーンにスキルを入れる`) and save.
- **No existing file was modified.** Integration uses only public API: `SmashBallModule.AnyBroken`, `LaserTurret.OnStateChanged / IsFiring / MuzzlePosition / AimDirection / rotationSpeedScale / tuning / playerIndex`, `SwarmSystem.QueueBeam`, `ILaserTarget` / `IAttributedLaserTarget`, `FortressNet.HasSimulationAuthority`, `FortressEffectPlayer`, `FortressCameraRig.Active.AddModifier(new SimpleShakeModifier(...))`, `DebugUi.Visible`.

### Flow (PlayerSkillSlot)
| State (`PlayerSkillState`) | Meaning |
|---|---|
| `NoSkill` | no skill assigned |
| `Empty` | not charged |
| `Ready` | charged; charge visuals shown; the normal laser is untouched; the next **transition into `TurretState.Firing`** (= next Q press / next attack) fires the skill instead of the normal laser (user 2026-10-15: 「スキル発動可能時は普通のレーザーは打てなく」→ clarified 「離した後ではなくて次Qキーを押すとき（次の攻撃）」) |
| `Active` | runner is firing (windup → fire → close) |
- Charge: `SmashBallModule.AnyBroken` where `info.Attacker.Turret == Turret` (or `PlayerIndex` match when Turret is null) and `skill.chargeBySmashBall`, or `GrantCharge()`.
- If charged while the turret is firing (normal laser or the skill), `_waitForRelease = true`: the current laser must end (state leaves Firing) before the next Firing start triggers the skill (user requirement). `IsWaitingForRelease` exposes it.
- While `Active` and `skill.ignoreChargeWhileActive` (default true) new charges are ignored; otherwise they are queued (`_pendingCharge`) and applied after the skill.
- Hiding is centralised in `PlayerSkillSlot.RefreshSuppression()` (called on every `SetState`, OnEnable, and when the post-skill hold ends): `NormalLaserSuppressor.Apply(hideBeam, hideAim)` with hideBeam = (Active && `replacesNormalLaser`) || post-skill hold; hideAim = Active && `replacesNormalLaser`. Nothing is hidden while Ready: `Activate()` runs inside `LaserTurret.Update` (order −10) via `OnStateChanged(Firing)`, so `LaserBeamVisual` (order 0) is disabled before it draws that frame — the next attack never shows a normal beam. Hiding the beam = disable `LaserBeamVisual` and its LineRenderers (`lineRenderer`, children `BeamGlow*`); hiding the aim = disable `TurretAimIndicator` + child `AimIndicator`. If the charge arrives mid-shot, the current normal laser keeps firing until the grip is released (user 2026-10-15: 「撃っている最中に溜まった場合は、そのレーザーは離すまで出し続けて、その後は撃てないように」); releasing changes nothing by itself; the next grip (Q press) fires the skill. One charge = one skill use (charges never stack: `GrantCharge` is a no-op while Ready, and ignored while Active unless `ignoreChargeWhileActive` is off; user confirmed 2026-10-15). Heat still rises while gripping (see gotcha 3).
- After the skill: if `hideNormalLaserUntilRelease` and still firing → keep suppressing until the turret leaves Firing.
- Turret logic keeps running during the skill: gripping still adds heat (can overheat after/during the skill), rotation continues at `rotationSpeedScale × timing.rotationSpeedScale` (runner temporarily overwrites the public field and restores it).

### Network (LAN, host-authoritative)
- No new network traffic. Every PC runs the slot + runner itself: `AnyBroken` fires on clients with replicated `DestroyedBy`; turret state comes from `TurretSnapshot` so `OnStateChanged(Firing)` happens on clients too.
- Damage only where `FortressNet.HasSimulationAuthority` (obstacles) / `HasSimulationAuthority || SwarmSystem.IsReplica` (swarm: clients queue 0-dps beams for hit flash). Visuals, SE, shake are local. Small timing differences between PCs are possible (snapshot latency).

## 1. Paths / asmdefs
```
Assets/RedSkill/
  Scripts/Runtime/  MS2026.RedSkill.Runtime (rootNamespace MS2026.Skills; refs MS2026.Fortress.Runtime, MS2026.CharacterSelect.Data, DDrive.Runtime, DDrive.Foundation, Unity.InputSystem)
    Core/ CharacterSkill.cs (abstract SO) · CharacterSkillRunner.cs (abstract MB) · PlayerSkillSlot.cs (+ enum PlayerSkillState) · PlayerSkillInstaller.cs
          CharacterSkillTable.cs (SO) · NormalLaserSuppressor.cs (internal) · SkillLabelAttribute.cs
    Red/  RedLaserSkill.cs (SO + settings classes RedLaserTiming/Shape/Damage/Look/ChargeFx/MuzzleFx/ImpactFx/CameraShake/Sounds/Shaders)
          RedLaserSkillRunner.cs · RedLaserVisuals.cs (internal, procedural visuals) · RedLaserLightning.cs (internal, pooled LineRenderer bolts) · RedLaserLanes.cs (internal, per-lane stop data + strips) · RedLaserTube.cs (internal, the paste tube on the turret)
  Scripts/Editor/   MS2026.RedSkill.Editor (ns MS2026.Skills.EditorTools; refs runtime + MS2026.Fortress.Runtime)
          SkillInspectorGUI.cs (Japanese labels from [SkillLabel], groups as foldouts) · SkillTestButtons.cs (inspectors: PlayerSkillSlot, PlayerSkillInstaller, CharacterSkill (all subclasses), CharacterSkillTable)
          RedSkillMenu.cs (menus)
  Shaders/  RedSkillBeam.shader ("MS2026/RedSkill/Beam") · RedSkillRadial.shader ("MS2026/RedSkill/Radial") · RedSkillParticle.shader ("MS2026/RedSkill/Particle") · RedSkillCommon.hlsl
  Data/     RedLaserSkill.asset (only skillId/displayName/description/themeColor/shaders serialized; everything else = C# field initializers until Unity re-saves it)
            CharacterSkillTable.asset (entries empty)
  Prefabs/  RedSkill_Installer.prefab (defaultSkill = RedLaserSkill.asset, characterTable = CharacterSkillTable.asset, useCharacterSelection on, enableTestKeys on)
Docs/Tools/RedSkill_AI_Reference.md, RedSkill_ガイド.html
```
- Test scene `Assets/Scenes/redskill_testscene.unity` (copy of the game setup: 4 turrets `Turret_P0..P3`, smash ball on `Destructible_0`, swarm, spawn points). The installer is **not** in the scene file yet — the user adds it via the menu (the editor had the scene open, so the YAML was not edited).

## 2. Public API
```csharp
// MS2026.Skills
abstract class CharacterSkill : ScriptableObject {
  string skillId="skill", displayName, description; Color themeColor;
  bool chargeBySmashBall=true, ignoreChargeWhileActive=true, replacesNormalLaser=true, hideNormalLaserUntilRelease=true;
  string DisplayName; abstract CharacterSkillRunner CreateRunner(PlayerSkillSlot slot); }
abstract class CharacterSkillRunner : MonoBehaviour {
  PlayerSkillSlot Slot; LaserTurret Turret; abstract bool IsActive;
  protected virtual void OnBound(); virtual void SetReadyVisual(bool); abstract void Activate(); abstract void Cancel();
  protected void NotifyFinished(); }   // call when done; Cancel must NOT call it
[RequireComponent(LaserTurret), DefaultExecutionOrder(-5)] sealed class PlayerSkillSlot : MonoBehaviour {
  static IReadOnlyList<PlayerSkillSlot> All; static PlayerSkillSlot Find(int playerIndex);
  static event Action<PlayerSkillSlot> AnyStateChanged; event Action<PlayerSkillSlot> StateChanged;
  PlayerSkillState State; CharacterSkill Skill; CharacterSkillRunner Runner; LaserTurret Turret; int PlayerIndex;
  bool IsReady, IsActive, IsWaitingForRelease; bool startReady;
  void SetSkill(CharacterSkill);  // character binding; keeps Ready; cancels an active skill
  void GrantCharge(); void ClearCharge(); bool ActivateNow() /*test*/; void CancelActive(); }
[DefaultExecutionOrder(-20)] sealed class PlayerSkillInstaller : MonoBehaviour {
  CharacterSkill defaultSkill; CharacterSkill[4] perPlayerSkills; bool useCharacterSelection=true; CharacterSkillTable characterTable;
  bool startReady; bool enableTestKeys=true;  // keys 1–4 → GrantCharge P1–P4, only when DebugUi.Visible (editor / -debug-ui)
  void InstallAll(); PlayerSkillSlot Install(LaserTurret); CharacterSkill ResolveSkill(int playerIndex); }
  // ResolveSkill priority: CharacterSelectionResult.HasResult && table.Find(Get(i)) → perPlayerSkills[i] → defaultSkill
sealed class CharacterSkillTable : ScriptableObject { Entry[] entries; /* Entry{CharacterDefinition character; string characterId; CharacterSkill skill} */ CharacterSkill Find(CharacterDefinition); }
// MS2026.Skills.Red
sealed class RedLaserSkill : CharacterSkill { timing, shape, damage, look, charge, muzzle, impact, cameraShake, sounds, shaders; float WidthAt(float distance, float widthScale); }
[DefaultExecutionOrder(10)] sealed class RedLaserSkillRunner : CharacterSkillRunner { RedLaserSkill Definition; float CurrentLength; bool HasImpact; }
```
- Adding another character's skill: subclass `CharacterSkill` (+ `[CreateAssetMenu]`), implement a runner, add `[SkillLabel]` to fields, assign the asset (installer / table). Slot, charging, suppression, test buttons and inspectors work unchanged.

## 3. RedLaserSkill data (defaults; `[SkillLabel]` = Inspector label)
| group.field | default | meaning |
|---|---|---|
| timing.windupSeconds / fireSeconds / openSeconds / closeSeconds | 0.3 / 3 / 0.35 / 0.45 | 撃つ前のため / 撃ち続ける時間 / 太くなるまで / 消えるまで |
| timing.openCurve / closeCurve | 0→1.18@0.55→1 / 1→1.08@0.25→0 | width multiplier over phase 0..1 |
| timing.endWhenGripReleased / rotationSpeedScale | false / 0.3 | end early when turret stops firing / turret rotation × during skill |
| shape.range / rootWidth / tipWidth / spreadDistance | 28 / 1.2 / 5.5 / 14 | metres (normal laser: range 20, max width 0.6) |
| shape.spreadCurve / extendSpeed | ease-out 0→1 / 90 m/s | width(d) = lerp(root, tip, curve(d/spreadDistance)) × widthScale |
| shape.pulseAmount / pulseFrequency / visualWidthScale / visualSegments | 0.06 / 14 / 1.35 / 40 | visual mesh is wider than the hit width |
| damage.swarmDamagePerSecond / obstacleDamagePerSecond | 100 / 60 | × clamp01(widthScale); obstacle dps × tuning.obstacleDamageMultiplier if `useTurretObstacleMultiplier` (true; else tuning=null is passed) |
| damage.laneCount | 20 (4–48) | **per-lane blocking** (2026-10-15, user sketch: 「真ん中が当たったらぶつかるってなってるけど不自然だから、当たっている部分と当たっていない部分分けれますか」). laneCount+1 boundary rays spread over the *visual* width (`Fraction` −1…+1); each follows the cone as a 4-segment polyline (breaks at 0, ¼, ½, 1 × spreadDistance, range) with `Physics2D.Linecast` (non-trigger, `blockerLayers`, own turret ignored, hits sorted by fraction). A lane stops at the first wall, or at a `DestructibleObstacle` when `stopAtDestructibles`; Actor enemies (`ILaserTarget` without `DestructibleObstacle`) never stop a lane. Each lane extends on its own at `extendSpeed` (`RedLaserLanes.Length`), so a lane freed by a broken obstacle regrows from there |
| damage.hitWidthScale / hitColumns / hitSegments | 1 / 3 / 3 | swarm: hitColumns × hitSegments `QueueBeam` capsules (owner = playerIndex, maxHits 0). Columns split the hit width; a column's length = median of its lanes' lengths (a few blocked lanes don't cut the whole column). Joints overlap → slightly more damage there. Obstacles/Actors: every `ILaserTarget` found by a lane inside the hit width (|Fraction| ≤ hitWidthScale / visualWidthScale) and within that lane's length gets damage once per frame |
| damage.blockerLayers / stopAtDestructibles | ~0 / true | (`blockProbeRadius` and the single centre `CircleCast` were removed with per-lane blocking) |
| look.stripeGradient (LDR, 8 keys) | red (.95,.12,.16) / pale pink (1,.84,.86) / deep red (.58,.02,.07) / pale pink, each band 2 keys with soft 0.1 blends (user 2026-10-15: pattern from the reference image, colours red because the skill is red) | しま模様の色（1周ぶん）. Diagonal candy stripes from the user's reference image (2026-10-15: 「添付の写真のような感じのレーザーでこれをグラデーション」). Baked periodic (`EvaluatePeriodic`: the gap after the last colour key blends into the first key), `TextureWrapMode.Repeat` |
| look.stripeBlend | 0.65 | しまのグラデーションの広さ (user 2026-10-15: 「レーザーの中も色はもうちょっとグラデーション」): after baking, the periodic stripe texture is box-blurred twice with radius = blend × 0.25 × resolution (wraps around), so 0 = crisp bands, 1 ≈ all gradient |
| look.stripeStrength / stripePeriod / stripeAngle / stripeScrollSpeed / coreShine | 1 / 3.2 m / 40° / 7 m/s / 0.25 | shader: coord = (dist + acrossMeters·tan(angle) − time·speed) / period; across is in metres so the slant stays constant while the beam widens; shine = white × (1−1.8x)³ |
| look.crossGradient (HDR) | white → (1,.9,.9)@.3 → (1,.68,.66)@.55 → light red → red → deep red, alpha 1→0 at edge (reddens gradually inside the beam) | 断面の色（中心→ふち）, multiplied with the stripe colour (white centre shows the stripes, red edge = glow). Baked to 1D textures (RGBAHalf, converted to linear in linear space) |
| look.lengthGradient (HDR) | white → (1,.88,.86) → (1,.66,.62) at the tip | 長さ方向の色（根元→先端） |
| look.intensity / bodyOpacity / particleBrightness | 1.15 / 0.85 / 2.5 | blend = premultiplied: additive (0) … normal (1); stripes need a high opacity to read on bright floors |
| look.streakStrength / streakDensity / streakSpeed | 0.2 / 10 / 16 | scrolling streaks |
| look.edgeJag / edgeJagFrequency / edgeJagSpeed / flicker | 0.2 / 1.4 / 10 / 0.12 | noisy edges |
| look.tipFadeLength / sortingOrder / gradientResolution | 4 / 150 / 128 | tip fade only when nothing is hit |
| charge.glowColor (`[FormerlySerializedAs("circleColor")]`) | (1.6,.18,.14) | colour of the muzzle glow and the ready/fire rings. **The magic circle was removed** (user 2026-10-15: 「魔法陣はいらない」; fields circleRadius/circleSortingOrder/circleSpinSpeed and the Radial shader's MagicCircle code are gone; `_ShapeC.y` is unused) |
| charge.pulseFrequency / pulseAmount / muzzleGlowSize | 1.6 / 0.35 / 1.5 | |
| charge.emberRate / emberColor / emberRadius / readyBurstSize / windupEmberMultiplier | 40 / red / 2.6 / 6 / 5 | particles converge to the muzzle; ready ring spawns at the muzzle |
| tube.enabled / sprite / tint | true / empty / white | 「溜めの歯磨き粉」 (2026-10-15, user: 「スキル発動可能時キャラの上に強力そうな歯磨き粉（仮素材・赤っぽい）を置いて、今にも放ちそうな演出」「スキル発動時は、その歯磨き粉の先から出ているように」). `RedLaserTube`: a `SpriteRenderer` (+ `BillboardIgnore`, lies flat like the beam) on the turret, nozzle toward `AimDirection`, shown while Ready / windup / firing, fades after. Empty sprite → procedural placeholder (`RedLaserTube.PaintTube`, no image file, 112×352), toon style **modelled on the title-scene opening tube** `Assets/Title/Sprites/Title_Toothpaste.png` + `Title_ToothpasteCap.png` (2026-10-16, user: 「もうちょっと縦に長く」「タイトルシーンの冒頭に出てきたものを参考に」): tall tube tapering from a wide bottom (solid ink bar) to an oval mouth rim, ridged cap on top, thick ink outline with magenta (left) / cyan (right) offset copies like the title art, pale-pink body with a pink and a red vertical stripe, bold red lightning-bolt emblem, flat shadow on the right, thin white shine on the left. Recoloured red (title tube is white/blue). A custom sprite must have its nozzle pointing up; it is scaled to width × length |
| tube.length / width / forwardOffset / sideOffset / sortingOrder | 3.4 / 1 / 0 / 0 / 140 | nozzle anchor = turret centre + aim × (forwardOffset + length/2) (+ side). Kept fixed while the tube squashes (strain/pulse change the length around the nozzle) |
| tube.appearSeconds / fadeOutSeconds | 0.3 / 0.35 | pop-in with overshoot, fade + shrink out |
| tube.shakeAmount / shakeAngle / shakeFrequency / windupShakeMultiplier | 0.04 m / 1.5° / 40 /s / 2.5 | fast back-and-forth tremble (user 2026-10-16: 「もっと振動速く、踏ん張ってるような感じ」): a phase advances at 2 × shakeFrequency per second and every crossing flips the side (offset mostly sideways ±0.6–1 × amount, small random along the aim, opposite tilt) — above half the frame rate it flips every frame. × (1 + surge), × windup/firing multiplier |
| tube.strainAmount / strainSurgeFrequency | 0.06 / 1.3 /s | 踏ん張り: constant squash (length × (1 − strain), width × (1 + 0.7 strain)); surges `pow(max(0, sin), 6)` add up to 1.5 × strain and double the tremble |
| tube.thunderIntervalMin / thunderIntervalMax / thunderLength / thunderWidthScale | 0.35 s / 0.9 s / 4.5 m / 3 | thunder (user: 「雷のようなエフェクト」): while Ready/windup (timer ×3 at full windup) a thick bolt (lightning.width × 3, life × 2, always branches) strikes a random point on the tube from 4.5 m away, with 2 thinner side bolts, a ring and a spark burst, and flashes the charge glow. `RedLaserVisuals.StrikeThunder` |
| tube.pulseAmount / pulseFrequency / recoilDistance | 0.06 / 2.5 / 0.35 m | pressurised swelling (length ×(1+pulse), width ×(1−0.6 pulse)); recoil kick on fire start |
| tube.pinchAmount / pinchHeight / pinchWidth / swellAmount / deflateAmount / windupGrip / swellRange / sliceCount | 0.55 / 0.45 / 0.09 / 0.35 / 0.2 / 0.15 / (0.07, 0.84) / 40 | **title-opening squeeze** (2026-10-16, user: 「使用時に減っていくのは縦方向じゃなくて、タイトルシーンの冒頭のような感じに」; replaced `squeezeWhileFiring`, which shortened the tube from the back). The sprite is cut into sliceCount horizontal strips (`Sprite.Create` per strip, child SpriteRenderers `Slice i` with `BillboardIgnore`, rebuilt when the sprite changes; works for any swapped sprite like `TitleOpening`). Strip width = 1 + swell·bulge·(1−g) − pinch·g, g = exp(−((v−pinchHeight)/pinchWidth)²), bulge = sin(π·body)^0.6 inside swellRange (same formula as `TitleOpening.SetTubeShape`). Windup: grip = windupGrip·w² → pinch = pinchAmount·grip, swell = swellAmount·grip. Firing p (2026-10-17, user: 「真ん中のへこみ具合は徐々に徐々にへこむように」): the pinch deepens over the whole firing, pinch = pinchAmount · lerp(windupGrip, 1, smoothstep p); the bulge grip still eases in during the first third (bulgeGrip) and swell goes from swellAmount·bulgeGrip to −deflateAmount (smoothstep p) = the tube empties. Length never changes; the fade-out keeps the last shape. **Side shrink** (2026-10-17, user: 「放出しているとき、横からも縮むように」): `tube.sideShrinkWhileFiring` 0.4 → every strip width × (1 − sideShrink × (1 − (1−p)²)) while firing (bar/cap strips outside swellRange only × 0.3 of it), so by the end the whole tube is 40 % narrower |
| tube.dripRate / dripColor / lightningRate / auraScale / auraColor | 10 /s / pale pink / 12 /s / 1.5 / HDR red | Ready/windup only (×3 at full windup): paste drops from the nozzle (`Embers`), bolts over the tube surface; aura = elliptical Radial glow behind the tube (also while firing, ×1.4) |
| shape.rootWidth | 0.8 (was 1.2) | narrowed so the beam reads as coming out of the nozzle |
| lightning.color / brightness / width / jitter / boltLife / branchChance | red (1,.16,.12) / 3.5 / 0.08 / 0.22 / 0.09 s / 0.35 | 「ビリビリ（稲妻）」 group, added 2026-10-15 (user: 「溜めているときもっとにビリビリしていいです（赤色）あとレーザーももっとエフェクトつけていいですよ」). Bolts = zig-zag polylines re-jittered every frame (sin envelope, ends fixed), flicker, fade; optional branch |
| lightning.chargeRate / chargeRadius / windupMultiplier | 22 /s / 2.4 / 3 | while Ready: radial bolts muzzle→out and arcs around the turret base; windup: 70 % of bolts converge into the muzzle; each bolt adds a tiny spark and flashes the charge glow (×1.8) |
| lightning.beamRate / beamBoltLength / edgeRate / impactRate | 30 /s / 4 m / 24 /s / 26 /s | while firing: long bolts inside the beam width, short bolts out of the edges, bolts out of the impact point (hemisphere around the normal). Bursts: 8 bolts on Ready, 12 on fire start |
| muzzle.flareColor / flareSize / flareRays / fireBurstSize | (2.2,.6,.5) / 3.6 / 12 / 9 | |
| muzzle.sparkRate / sparkSpeed / sparkSpreadAngle | 120 / 16 / 70° | |
| muzzle.shardRate / shardReach / emberRate / particleColor / shardColor | 70 / 10 / 60 / light red / red | spiky streaks bursting outward from the beam edges |
| muzzle.speedLineRate / speedLineSpeed | 70 /s / 45 m/s | thin fast streaks parallel to the beam (70 % just outside the edge, 30 % on top of the beam), `SpeedLines` ParticleSystem (stretched, no drag) |
| impact.flareColor / flareScale / splashScale | (2.6,.95,.8) / 1.3 / 1.7 | splash = ellipse along the wall tangent |
| impact.sparkRate / sparkSpeed / shockwaveInterval / shockwaveScale / emberRate / sparkColor | 220 / 18 / 0.2 / 1.6 / 50 / pale red | sparks reflect around the hit normal |
| cameraShake.ownerViewOnly / fire / fireStrength / impact / impactStrength / impactInterval | false / (0.35,0.8°,22Hz,0.5s) / 1 / (0.1,0.15°,26Hz,0.18s) / 1 / 0.15 | `SimpleShakeModifier` on `FortressCameraRig.Active` |
| cameraShake.whileFiring / whileFiringStrength / whileFiringInterval | (0.22, 0.7°, 32 Hz, 0.16 s) / 1 / 0.1 s | constant rumble while firing (runner `UpdateImpactFeedback`); raised 2026-10-17 (user: 「打ってるとき画面震わせれますか」) from (0.07, 0.2°, 30 Hz, 0.14 s) / 0.12 s, which was too subtle. Overlapping `SimpleShakeModifier`s on `FortressCameraRig.Active` (each PC shakes its own view; `ownerViewOnly` limits it to the shooter) |
| flash.keepInsideLaser | true | 「演出をレーザーと同じ太さに収める」 (2026-10-17, user: 「すべてレーザーと同じ大きさにしてください」): halo width × 1 (ignores haloWidthScale), beam rings × 1.0 (else 1.15), wall splash = impact-area width (ignores impact.splashScale), shards fly forward with ≤ 0.15 sideways, beam embers drift ≤ 0.4 sideways, edge bolts run forward along the edge and slightly inward, speed lines only inside (±0.85). Off = the previous outward-spreading behaviour |
| flash.pulseStrength / pulseSpacing / pulseSpeed / pulseWidth | 0.9 / 5 m / 45 m/s / 0.25 | 「派手さ（追加の演出）」 (2026-10-17, user: 「レーザーの方もうちょい派手でもいい、エフェクト増やしたり」). Beam shader `_PulseParams`: bright bands travelling root→tip, band = (1 − \|frac((dist − t·speed)/spacing) − ½\| / (width/2))², added as white-pink × (1 − 0.6·across) |
| flash.haloEnabled / haloOnTop / haloWidthScale / haloColor / haloIntensity | true / true / 2 / (1,.22,.28) / 1.5 | outer glow: a second strip mesh (`BeamHalo`; `haloOnTop` (2026-10-17, user: 「横に広がってるエフェクト、レーザーとかぶせれる？」) → sortingOrder +1 and 0.005 nearer the camera so it is layered over the beam; off → −1 behind, only the part sticking out is visible) built from the same lanes/strips at 2 × visual width (so it also stops at walls), drawn with a second Beam-shader material: additive, no stripes/streaks, cross gradient baked from haloColor (alpha .35 centre → .55 → .18 → 0), stronger jag, 60 % pulses |
| flash.beamRingInterval / beamRingSpeed / beamRingThickness / beamRingColor | 0.16 s / 40 m/s / 0.9 m / HDR (2.2,.6,.7) | rings launched from the nozzle that fly along the beam (12 pooled Radial quads, ring mode, scaled to 1.15 × beam width × thickness), fading in over 1.5 m and out towards the centre lane's end (stop at walls) |
| flash.muzzleFlare2Scale / muzzleFlare2Rays | 1.9 / 6 | second, larger counter-rotating muzzle flare with a ring, behind the first |
| sounds.onReady / onWindup / onFire / whileFiring / onImpact / onEnd | empty `FortressEffect` | D-Drive VFX/SE slots (project rule); whileFiring uses `StartFollowing` (use a Loop VFX) |
| shaders.beam / radial / particle | the 3 shaders | fallback `Shader.Find(name)` then `Sprites/Default` with a warning |
- Editor-only live tweaking: visuals re-bake gradients and re-apply sorting orders every 0.25 s while playing in the editor; all other values are read every frame.

## 4. Visuals (RedLaserVisuals)
- One root per runner, `[RedSkill] Pn` (HideFlags.DontSave, at scene root, world space; hidden with `SetActive(false)` when the runner is disabled; destroyed in runner `OnDestroy`).
- `RedLaserVisuals.BuildStripMesh(StripMesh, …, widthMultiplier)` builds both the beam (×1) and the halo (× haloWidthScale) from the same strips.
- Beam: mesh built from **strips** (`RedLaserLanes.Strip*`, capacity laneCount × 2, unused strips collapsed to one point): one strip between neighbouring lane rays, each edge column has its own length and blocked flag (so a slanted wall gives a trapezoid end). When the two rays' hit distances differ by ≥ 0.75 m (an obstacle corner lies between them) `RedLaserSkillRunner.BuildStrips` bisects 4 times with extra lane casts to find the corner and splits the strip there (otherwise a wedge would stretch past the wall). Per strip edge: `visualSegments`+1 vertices. UV0 = (distance / range, v across 0..1, distance m, visual width m), UV1 = (that column's length, blocked 0/1). Shader: tip fade only on unblocked columns; `look.wallGlow` (1.2, label `壁ぎわの光`) brightens the last ~0.6 m of blocked columns. Material renderQueue 3100, sortingOrder `look.sortingOrder`.
- Impact effects are per **impact area** (≤ 4): consecutive blocked lanes whose hit distances differ < 1.5 m. Each area gets its own flare + wall splash (sized by the area width) and its own shockwave ring; sparks/embers/bolts are emitted from random blocked lanes, rate × (blocked lanes / lanes). Beam particles/bolts/speed lines are only emitted inside each lane's current length (`RedLaserLanes.LengthAt`).
- Quads (shared static mesh) with `Radial` shader + MaterialPropertyBlock (`_Color` HDR, `_ShapeA/B/C`): `ChargeGlow`, `MuzzleFlare` (rays), `ImpactFlare`, `ImpactSplash`, 8 pooled `Ring`s (shockwaves: ready burst, fire burst, periodic impact rings).
- Particles: 4 code-built ParticleSystems (`Sparks` stretched, `Shards` stretched long, `Embers` billboard, `SpeedLines` stretched), emission off, manual `Emit(EmitParams)`, world space, colour × `_Intensity` = `look.particleBrightness` (vertex colours are LDR).
- **Fire point**: `RedLaserVisuals.FirePoint` = tube nozzle while the tube is shown, else `LaserTurret.MuzzlePosition`. The runner uses it as the beam origin (lanes, damage, muzzle flare) and for the windup/fire/end SE positions; charge glow, converging particles, charge lightning, ready/fire rings and bursts are centred on it. The tube shake therefore also jitters the beam origin slightly (≤ ~0.13 m while firing). `RedLaserVisuals.Tick` (called first in the runner's Update) updates the tube before `TickBeam` reads the fire point.
- Lightning: `RedLaserLightning` = 64 pooled LineRenderers (`Bolt0..`), material = Particle shader with `_LineMode` 1 (glow only across the line: UV y), `_Softness` 1.3, `_Intensity` = `lightning.brightness`; sortingOrder = look.sortingOrder + 3.
- Phases: charge (muzzle glow + converging particles) → windup (glow grows ×2.4, spins ×6, particles ×`windupEmberMultiplier`) → fire (rings + spark burst + shake; beam/flare/sparks/shards/embers each frame) → impact (flare, splash, sparks, embers, rings every `shockwaveInterval`, light shake every `impactInterval`) → close (width curve to 0) → `NotifyFinished`.

## 5. Editor UI
- Menu `Tools/赤スキル/開いているシーンにスキルを入れる` (instantiates the prefab, or builds the same object if the prefab is missing; selects an existing installer instead of adding a second; disabled in Play) and `Tools/赤スキル/スキルの設定ファイルを開く`.
- Inspectors draw Japanese labels (`SkillInspectorGUI`): groups are foldouts (`タイミング`, `形・大きさ`, `ダメージ・当たり判定`, `レーザーの色・模様`, `溜め（使える状態）の演出`, `発射口の演出`, `壁に当たった場所の演出`, `カメラの揺れ`, `効果音・追加エフェクト（D-Drive）`, `シェーダー（通常は触らない）`). `FortressEffect` fields use the Fortress drawer.
- Play-mode test rows (`SkillTestButtons.DrawRow`): `P{n}  スキル名` + state text, buttons `使えるようにする` / `今すぐ撃つ` / `止める` — on the slot, the installer (all slots) and the skill asset (slots using it).
- Create menus: `MS2026/スキル/レッド：極太レーザー`, `MS2026/スキル/キャラとスキルの対応表`; AddComponent `MS2026/スキル/スキルを砲台に配る`.

## 6. Gotchas
1. Unverified in Unity at writing time (compile was offline). If shaders fail to import, the runner falls back to `Sprites/Default` (looks wrong but no crash) and logs `[RedSkill] シェーダー … が見つかりません`.
2. The skill fires only on a **transition into Firing**. Holding the grip through the moment the charge arrives does nothing until release + re-grip (by design).
3. Heat still accumulates while gripping during the skill and while gripping in Ready with the normal laser blocked (turret state is still `Firing`; turret code untouched). Ask the user before changing (would need a LaserTurret hook).
4. `LaserTurretEffects` muzzle VFX (normal laser) keeps playing during the skill and while gripping in Ready (the beam itself is hidden).
5. Swarm hit capsules overlap at joints (capsule shape of `SwarmLaserJob`), giving up to ~2× dps there. `SwarmLimits.MaxBeams` = 32 per frame for everyone (each skill uses hitColumns × hitSegments = 9; extra beams are dropped silently).
6. Per-lane blocking cost: (laneCount+1) × 4 linecasts per frame + 16 per obstacle corner. Thin obstacles narrower than the lane spacing (visual width / laneCount, ≈0.37 m at the 5.5 m tip) can slip between rays. The swarm only knows 3 columns, so crowd damage right beside an obstacle edge is approximate.
7. `RedLaserSkillRunner` is not `[DisallowMultipleComponent]` on purpose (old runner is destroyed at end of frame when swapping skills).
8. Test keys 1–4 also work in builds started with `-debug-ui`.
9. Other people's art / other games' footage must not be given to AI as reference (CLAUDE.md, user decision 2026-10-13). The visuals here are procedural and original.

## 7. Related
- `FortressDesigner_AI_Reference.md` (LaserTurret, SmashBall, Swarm, Effects, Net), `CharacterSelect_AI_Reference.md` (`CharacterSelectionResult`, `CharacterDefinition.id`), `DDrive_AI_Reference.md` (VFX/SE IDs for the `sounds` slots).
