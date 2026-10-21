# Green Skill (緑スキル「ミント・サテライト」) — AI Agent Reference

> Cold-start brief for Claude Code. Written **2026-10-20** together with the code (branch `緑キャラスキル`). Compiled offline with `dotnet build` against `Library/ScriptAssemblies` (RedSkill runtime + editor, UI.Game: 0 errors, 0 CS warnings); **not yet run inside Unity** (shaders, visuals, prefab/asset import unverified — check the Unity Console first). The procedural art was previewed by porting `GreenMintArt` to Python (case/tablet/leaf look as intended).
> Human guide: `Docs/Tools/GreenSkill_ガイド.html`. Maintenance rule (`CLAUDE.md`): when you change anything under `Assets/RedSkill/Scripts/Runtime/Green/`, the green shaders, `GreenSkillMenu.cs` or `HudCoreHealFx.cs`, update both files in the same task. Style: `Docs/Tools/DOCS_GUIDE.md`. Shared skill-slot framework: `RedSkill_AI_Reference.md`.

## 0. Mental model
- A green character skill built on the **same skill-slot framework as the red skill** (`CharacterSkill` SO + `CharacterSkillRunner` + `PlayerSkillSlot` + `PlayerSkillInstaller`). Charge = break a smash ball yourself (or `GrantCharge`); fires on the next transition into `TurretState.Firing` (next grip). `replacesNormalLaser` = true → the normal beam + aim line are hidden for the whole skill.
- User's spec (2026-10-19/20 Q&A): 「コアが周囲の敵を攻撃するようになる(射撃援助)、コアを回復」, support/stand type, motif mint tablets / toothpaste. Decisions: one activation, fixed duration (8 s); the green turret "sends power" to the core (gel link, its laser stops); core attack = auto-firing mint-tablet bullets from satellites; heal = burst on arrival + regen; gripping during the skill → thicker gel + more bullets (heat still rises: user chose **not** to touch `LaserTurret`); targets nearest-to-core first; heal 20 % burst + 15 % regen of max HP; bullets hit **enemies only** (never smash balls/destructibles); the burst is big **on every screen**; HUD core bar gets a heal effect; kills count for the green player; **no extra effects** (no slow/freeze/barrier/knockback/cut-in) and **everything green, nothing blue / "frozen"** (user: 「凍えるって表現は青を連想するため」); name 「ミント・サテライト」; no cut-in.

### Flow (`GreenMintSkillRunner`, phases `Idle → Launch → Active → Ending → Idle`)
| Phase | What happens |
|---|---|
| Ready (slot) | `SetReadyVisual(true)` → `GreenMintCase.Show()` (case pops in on the turret, trembles, rattles, tablets bounce in its window, sparkles + heal crosses + aura) + SE `onReady`. `SetReadyVisual(false)` while Idle → `Case.Dismiss()` (shrink/fade). |
| Launch (`timing.launchSeconds` 0.6) | `Activate()`: `Case.Open()` (lid flips open around its top-left hinge, flash/sparkles at the mouth), `Satellites.Launch(mouth, launchSeconds)` (each tablet flies on a quadratic-Bezier arc, alternate sides, staggered 0.07 s, with a trail), gel head extends turret→core over `link.extendSeconds`, scanner active, SE `onActivate`. |
| Active (`timing.activeSeconds` 8) | On entry: `GreenMintBurst.Play` (wave, second wave +0.14 s, core flash, sparkles/leaves/powder/crosses, core shine sweep, screen-edge vignette, camera shake) + **heal burst** + SE `onBurst` + loop VFX `whileActive` following the core. Every frame: regen heal, shooting, regen crosses. |
| Ending (`timing.endSeconds` 0.7) | Remaining regen flushed; satellites spiral into the core (radius → 5 %, faster spin, shrink, trails); gel tail slides into the core and fades. |
| Finish | flash + sparkles + wave at the core, SE `onEnd`, `NotifyFinished()`. Bullets already flying keep flying and land; the scanner stays active until they are gone. |
- `Cancel()` / `OnDisable` → `StopEverything(true)`: bullets cleared (reservations released), satellites/gel/burst hidden, case dismissed.

### Network (LAN, host-authoritative) — no new traffic
- Every PC runs slot + runner itself (smash-ball breaks, turret state and `Thickness01` replicate already, so the case, activation and grip boost look the same everywhere; the HUD reads the replicated core HP).
- **Heal** only where `FortressNet.HasSimulationAuthority`: `GreenMintUtil.Heal` → `CoreCrystalController.ApplyReplicatedHealth(min(max, hp + amount))` (the controller has no heal API; that method sets HP and raises `OnHealthChanged` without damage effects when HP rises). Clients receive the HP through `FortressMatch` replication (≤10/s). Regen is accumulated and applied in ≥0.02 HP steps (the controller ignores changes < 0.001).
- **Damage**: swarm → `SwarmSystem.QueueBeam` one frame per bullet impact (`owner` = green `playerIndex` → kill credit/combos and the existing kill-particle feature go to green); clients queue too (the swarm zeroes damage on replicas → hit flash only). Actor enemies → `ApplyLaserDamage(damage, null, turret)` only with authority.
- Each PC picks targets itself from its own swarm copy (netIds match), so bullet targets can differ slightly between PCs (visual only).

## 1. Paths
```
Assets/RedSkill/
  Scripts/Runtime/Green/   (asmdef MS2026.RedSkill.Runtime, namespace MS2026.Skills.Green; asmdef gained Unity.Mathematics, Unity.Collections, UnityEngine.UI)
    GreenMintSkill.cs          SO + settings classes (Timing/Heal/Attack/Boost/Palette/CaseFx/SatelliteFx/LinkFx/ShotFx/BurstFx/CameraShake/Sounds/Shaders)
    GreenMintSkillRunner.cs    phases, heal, shooting, gel endpoints
    GreenTargetScanner.cs      (internal) swarm/actor target picking + tracking by netId
    GreenTabletVolley.cs       (internal) 64 pooled homing bullets (SpriteRenderer + separate TrailRenderer), impact damage + fx
    GreenMintSatellites.cs     (internal) satellites, membrane, core glow, dashed range ring
    GreenGelLink.cs            (internal) gel strip mesh turret→core + carried tablets + in/outflow glows
    GreenMintCase.cs           (internal) ready-state tablet case
    GreenMintBurst.cs          (internal) burst fx + screen-edge overlay canvas (sortingOrder −100) + core shine
    GreenMintFx.cs             (internal) per-runner fx kit: root `[GreenSkill] Pn` (DontSave, BillboardIgnore), materials, 4 particle systems, 24 pooled flashes, 10 pooled waves
    GreenMintArt.cs            (internal) procedural textures/sprites (no image files)
    GreenMintUtil.cs           (internal) core lookup, Heal, ScreenUp, easing, helpers
  Scripts/Editor/GreenSkillMenu.cs   menus + inspector test tools (hooked via SkillTestButtons.DrawExtraTools)
  Shaders/GreenSkillGel.shader ("MS2026/GreenSkill/Gel", alpha blend) · GreenSkillSprite.shader ("MS2026/GreenSkill/Sprite", alpha blend, _MainTex × vertex colour × _Color)
  Data/GreenMintSkill.asset (guid 57d56c6a3ce242c6a13bb3f71bc24bc8; only skill info + shader refs serialized, everything else = C# field initializers until Unity re-saves)
Assets/_Game/UI/Game/HudCoreHealFx.cs   (MS2026.UI.Game) HUD core-bar heal effect, auto-attached at runtime
```
- Reuses the red shaders `MS2026/RedSkill/Radial` (additive glows/rings) and `MS2026/RedSkill/Particle` (additive sparkles; `_LineMode` 1 for trails).
- Wiring: `RedSkill_Installer.prefab` `perPlayerSkills[2]` (P3, green seat colour) = GreenMintSkill; `CharacterSkillTable.asset` entries `Chara_Alpha (alpha) → RedLaserSkill`, `Chara_Gamma (gamma, green) → GreenMintSkill`. `Game.unity` uses the prefab without overriding these.

## 2. Public API
```csharp
// MS2026.Skills.Green
[CreateAssetMenu("MS2026/スキル/グリーン：ミント・サテライト")] sealed class GreenMintSkill : CharacterSkill {
  timing, heal, attack, boost, palette, tabletCase, satellites, link, shots, burst, cameraShake, sounds, shaders;
  float TotalSeconds; }   // Reset(): skillId "green_mint_satellite", displayName ミント・サテライト, themeColor (.25,.8,.5)
[DefaultExecutionOrder(10)] sealed class GreenMintSkillRunner : CharacterSkillRunner {
  GreenMintSkill Definition; bool IsActive; float Boost01 /*smoothed grip 0–1*/; float HealedThisUse; }
// MS2026.Skills.EditorTools
static class SkillTestButtons { static event Action<CharacterSkill> DrawExtraTools; static void DrawExtra(CharacterSkill); } // new generic hook, invoked at the bottom of CharacterSkillEditor
// MS2026.UI.Game
[DisallowMultipleComponent, AddComponentMenu("UI Studio/コアの体力バーの回復演出 (HudCoreHealFx)")] sealed class HudCoreHealFx : MonoBehaviour {
  Color healColor=(.42,1,.66); float bigHealThreshold=.04, segmentFadeSeconds=.9, streakGapSeconds=.4, ignoreSecondsAfterStart=1.5, sparkleInterval=.22;
  static HudCoreHealFx Attach(GameObject coreBar); }
```

## 3. GreenMintSkill data (defaults; `[SkillLabel]` = Inspector label)
| group.field | default | meaning |
|---|---|---|
| timing.launchSeconds / activeSeconds / endSeconds | 0.6 / 8 / 0.7 | コアに着くまで / 続く時間 / 消えるまで |
| heal.burstPercent / regenPercent | 20 / 15 (% of `maxHealth`) | ドンと回復 / じわじわ回復（合計）; regen spread evenly over activeSeconds; overflow discarded |
| heal.stopWhenMatchEnded | true | no heal after `FortressMatch.HasEnded`; never heals a destroyed core (HP ≤ 0) |
| attack.range | 9 m | 守る範囲の半径 (turrets are 6 m from the core, screen ≈ 28 m tall) |
| attack.shotsPerSecond / damagePerShot | 10 / 14 | normal enemy HP 10 → one shot |
| attack.splashRadius / maxHitsPerShot | 0.55 / 3 | QueueBeam halfWidth / maxHits (0 = unlimited) |
| attack.targetActors / bombPriorityMeters | true / 3 | actors also targeted; bombs scored `distance − 3` (picked earlier, away from the core) |
| attack.bulletSpeed / bulletTurnRate / bulletLifetime | 20 m/s / 900 °/s / 1.4 s | homing: speed ramps 0.75→1.15×, turn limit × (1 + 3·age); arrive within max(step, 0.3 m); expire → burst in place |
| attack.maxImpactsPerFrame | 6 | caps QueueBeam use (SwarmLimits.MaxBeams = 32/frame shared with all lasers) |
| boost.boostedShotsPerSecond / boostedWidthScale / boostedFlowScale / response | 22 / 2.1 / 2.4 / 8 | boost = `Turret.IsFiring ? Turret.CurrentThickness01 : 0` (Launch/Active only), exp-smoothed; also orbit speed × (1 + 0.6·boost), gel/core glow brighter |
| palette.mint / green / deep / outline / tablet / leaf / glow(HDR) | (.55,1,.76) / (.2,.82,.5) / (.04,.42,.27) / (.03,.2,.13) / (.97,1,.98) / (.22,.72,.36) / (.55,1.6,.95) | all colours; art is painted from it **once at bind** (Play restart needed), everything else live |
| tabletCase.enabled / height / width / screenUpOffset | true / 2.2 / 1.45 / 1.7 m | case stands "up" on **each PC's screen** (`Camera.main.transform.up`); root = turret + up·(offset − height/2) |
| tabletCase.appearSeconds / trembleAmount / rattleIntervalMin/Max / rattleJump | 0.35 / 0.035 / 0.45–1.05 / (0.16 m, 9°) | trembles flip side at 45 Hz; rattles = jump + tilt, kick the inside tablets (simple gravity/bounce in the window) |
| tabletCase.tabletsInside / sparkleRate / crossRate / auraScale | 6 / 10 / 1.6 / 1.9 | |
| tabletCase.lidOpenSeconds / fadeOutSeconds / sortingOrder | 0.18 / 0.45 / 140 | lid opens 125° (EaseOutBack), case fades 0.3 s after the lid |
| satellites.count / size / orbitRadius / orbitDegreesPerSecond / bobAmount / shotPunch | 5 / 1.3 / 2.9 / 75 / 0.18 / 0.22 | |
| satellites.membraneRadius / membraneOpacity / showRangeRing / rangeRingOpacity / sortingOrder | 2.1 / 0.55 / true / 0.4 / 160 | dashed ring at `attack.range`, rotating 6°/s |
| link.width / wobbleAmount / wobbleWaves / bow / flowSpeed | 0.6 / 0.22 / 2.2 / 0.8 / 5 | gel from turret centre + 0.55 m to core − 0.8·membraneRadius; width profile: thinner root, wider at the core, travelling bulges every 2.6 m |
| link.carriedTablets / opacity / extendSeconds / segments / sortingOrder | 7 / 0.9 / 0.4 / 40 / 145 | |
| shots.bulletSize / bulletSpin / trailSeconds / trailWidthScale | 0.6 / 540 / 0.16 / 0.75 | |
| shots.powderCount / powderSize / leafCount / leafSize / sparkleCount / ringSize / muzzleFlashSize | 9 / 0.75 / 2 / 0.6 / 7 / 1.7 / 1.4 | impact = powder + leaves + sparkles + small wave + flash |
| burst.waveSize / waveSeconds / flashSize | 40 / 1.1 / 11 | |
| burst.screenEdgeStrength / screenEdgeSeconds | 0.55 / 1.1 | overlay vignette (mint), under the HUD (canvas sortingOrder −100) |
| burst.sparkleCount / leafCount / crossCount / shine / regenCrossRate | 110 / 18 / 10 / true / 2.2 | |
| cameraShake.ownerViewOnly / burst / burstStrength | false / (0.16, 0.35°, 18 Hz, 0.35 s) / 1 | `SimpleShakeModifier` on `FortressCameraRig.Active` |
| sounds.onReady / onActivate / onBurst / whileActive / onShot (+shotInterval .08) / onHit (+hitInterval .08) / onEnd | empty `FortressEffect` | D-Drive slots (project rule); whileActive = `StartFollowing` the core |
| shaders.radial / particle / gel / sprite | red Radial, red Particle, GreenSkill/Gel, GreenSkill/Sprite | fallback `Shader.Find` → `Sprites/Default` + warning `[GreenSkill] シェーダー … が見つかりません` |

## 4. Implementation notes
- **Targeting** (`GreenTargetScanner`): subscribes to `SwarmSystem.StorageReadable` (the only safe moment to read `Storage`), follows `SwarmSystem.Current` changes. Per scan: positions = `pos (+ correction on replicas)`, skip `hp ≤ 0` on host, keep a sorted top-32 by score (distance to core − bomb bias) inside `range`, update tracked (reserved) ids' positions. `BeginFrame()` merges top-32 (minus reserved) + actor candidates (`EnemyController.Active`), sorted. `TryTake` reserves; `Release` frees; a tracked id unseen for 2 scans = gone (bullet flies to the last position).
- **Bullets**: impact damage = `damage / min(Time.deltaTime, swarm.ActiveSettings.maxFrameDelta)` as dps for one frame (the swarm multiplies by its dt) on a 0.1 m capsule with `splashRadius`.
- **Visual scale**: the game camera is far (turret ≈ 20–25 px), so everything is big and coarse (case 2.2 m, satellites 1.3 m, bullets 0.6 m). Powder, leaves, crosses, gel, case and waves are alpha-blended (readable on bright floors); sparkles/glows/trails additive.
- **Art** (`GreenMintArt`, SDF + 1-px AA, no image files): tablet (white, mint rim, dark outline, score line, shine), leaf (two-circle lens, vein), soft dot, cross, wave ring, membrane, dashed ring, vignette, case body 150×200 px (rounded, light stripe left / shade right, window hole, white label with leaf emblem), window back, lid 150×34 (ridges, pivot = hinge). PPU = width → 1 unit wide.
- All objects live under `[GreenSkill] Pn` at scene root (DontSave, `BillboardIgnore` so the billboard system leaves the flat sprites alone); hidden with the runner, destroyed in `OnDestroy`.

## 5. Editor UI
- Menus: `Tools/緑スキル/スキルの設定ファイルを開く`, `Tools/緑スキル/コアを30%減らす（回復を試す・Play中）` (via `ApplyReplicatedHealth`, keeps ≥1 HP), `Tools/緑スキル/緑スキルを今すぐ撃つ（Play中）` (first slot holding a GreenMintSkill: GrantCharge + ActivateNow).
- Skill asset inspector (shared `CharacterSkillEditor`): groups `タイミング`, `コアの回復`, `コアの攻撃（錠剤の弾）`, `握ったときの強化`, `色（ミントの配色）`, `溜めのタブレットケース`, `衛星とコアの膜`, `コアへ送るジェル`, `弾と当たった場所の演出`, `回復の瞬間の演出`, `カメラの揺れ`, `効果音・追加エフェクト（D-Drive）`, `シェーダー（通常は触らない）`; Play: slot rows + **テスト（コア）** (core HP, `コアを30%減らす`, `コアを満タンにする`).
- Create menu `MS2026/スキル/グリーン：ミント・サテライト`.
- Test keys: `3` charges P3 (installer test keys, editor / `-debug-ui`); P3 grip = `O`.

## 6. HUD heal effect (`HudCoreHealFx`, UI Studio side)
- Hidden DDOL `[UI] CoreHealFxFinder` (created AfterSceneLoad) polls every 0.5 s for an active `CoreBar` with a `Fill` descendant under a Canvas (UI Studio §4f layout) and attaches the component if none exists. Hand-adding it to `CoreBar` is fine (values then editable). No prefab rebuild needed.
- Subscribes `UiValues` `core.hp01`; an increase > 0.0002 (not from 0, not within 1.5 s of enabling) = heal. Builds at runtime: `HealSegment` (child of `Fill`, anchors x = old→new hp01, gradient brighter to the right, fades over 0.9 s after the streak ends; consecutive heals within 0.4 s extend it), `HealFrameGlow` (soft glow 28 px larger than `Frame`, placed behind `Gauge`), `HealToothGlow` (behind `Tooth`), 14 pooled `HealCross` RawImages rising from the bar (8 on a big heal ≥ 4 %, one every 0.22 s while regenerating).

## 7. Gotchas
1. Not run in Unity yet (see header). First run: check Console, shader import, sorting vs the core sprite / `CoreGemLook` (satellites 160, membrane 158, glows ≤157, gel 145, case 140), and that the overlay vignette sits under the HUD.
2. Heat still rises while gripping during the skill (user decision 2026-10-20: don't touch `LaserTurret`). `LaserTurretEffects` muzzle VFX also keeps playing while gripping (same as red).
3. Heal uses `ApplyReplicatedHealth` on the host because the core has no heal API; if one is added later, switch `GreenMintUtil.Heal` to it.
4. Installer priority (unchanged): table → perPlayer → default. With character select, a P3 player who picks a character not in the table (beta/delta) gets the green skill from `perPlayerSkills[2]`.
5. `SwarmLimits.MaxBeams` (32/frame) is shared: bullets use ≤ `maxImpactsPerFrame` (6) per frame; extra impacts wait one frame.
6. Scanner cost: one pass over all swarm agents per frame while active (+ HashSet/Dictionary lookups for reserved ids).
7. Art colours are baked at bind; palette edits need a Play restart.
8. Other people's art / other games' footage must not be given to AI as reference (CLAUDE.md, user decision 2026-10-13). All art here is procedural and original.

## 8. Related
- `RedSkill_AI_Reference.md` (slot/installer/table framework, test buttons), `FortressDesigner_AI_Reference.md` (core, swarm, QueueBeam, FortressEffect, Net, match replication), `UIStudio_AI_Reference.md` (HUD `CoreBar`, `UiValues`), `CharacterSelect_AI_Reference.md` (`CharacterDefinition.id`), `DDrive_AI_Reference.md` (VFX/SE IDs for the `sounds` slots).
