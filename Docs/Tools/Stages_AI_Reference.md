# Stages (ステージ切り替え・ステージ抽選ルーレット) — AI Agent Reference

> Cold-start brief for Claude Code. Written **2026-10-21** with the code (branch `ステージいくつか`). Compiled offline with `dotnet build` (Fortress.Runtime, MS2026.Stages.Runtime/Editor/Tests, UI.Game, UI tests: 0 errors); the pure logic (`StageCatalog.PickIndex`, `StageRouletteMath`) was also executed outside Unity (all checks pass). **Not yet run inside Unity**: the split tool has not been pressed, so `Game.unity` is still the old all-in-one scene and `Assets/Scenes/Stages/` does not exist until the user runs `Tools/ステージ/今の Game を共通と Stage1 に分ける（1回だけ）`.
> Human guide: `Docs/Tools/Stages_ガイド.html`. Maintenance rule (`CLAUDE.md`): when you change anything under `Assets/Stages/`, `GameSession.Stage.cs`, the stage parts of `GameSession*`/`LobbyRoster`/`LobbyMessages`, or `FortressNetworkBootstrap.AutoStartGate`, update both files in the same task. Multiplayer context: `Docs/Multiplayer/MultiplayerProgress.md` §5i.

## 0. Mental model
- User decisions (2026-10-21): several stages; **the stage is random, shown by a roulette screen inserted after character select** (auto spin, auto stop, cards sliding sideways); stages differ in obstacles/walls, background/look, enemies/difficulty and **turret/core placement**; architecture = **one common scene + per-stage content scenes** (not full copies); keep the name `Game` for the common scene; start with Stage1 (= current Game content) + a provisional Stage2.
- **Common scene `Game`** keeps: `[D-Drive] Runtime` (+FortressNetworkBootstrap/ConnectUI), `NetworkManager`, `NgoNetBridge`, `[UI]`, `EventSystem`, `Arduino`, `[Session] Guard`, `[Fortress] NetSync` (all *NetworkHub), `FortressCameraRig`, `[Fortress] Bgm` (kept here because `FortressFxAssigner` places BGM in Game), `Fortress` root (FortressLayoutController + `CoreCrystal` + `Turret_P0..P3`), `RedSkill_Installer`, new `[Stage] Bootstrap` (`StageBootstrap`).
- **Stage scene `StageN`** (`Assets/Scenes/Stages/`): `[Stage]` (Stage Studio `StageRoot` + background prefab + Sun), `[Fortress] Match Rules` (clear kills, core HP), `[Stage] Content` (moved Fortress children: `SpawnPoints`, `EnemySpawnDirector`, `SwarmSystem`, `Obstacles`, `NavigationField`), `[Stage] Layout` (`StageLayout`: anchors `Core`, `Turret_P1..P4`, camera preset). **No NetworkObjects in stage scenes** (checked by `Tools/ステージ/チェック`).
- Cross-references between the two groups: none in the current `Game.unity` (checked by script 2026-10-21), so moving objects keeps all references.
- **Load order is the key invariant: the stage's DestructibleObstacles/SwarmSystem must exist before the NetSync hubs spawn** (`DestructibleNetIndex.Build()` runs in `OnNetworkSpawn`, by hierarchy path).
  - Session (Lobby → Character select → Game): host NGO-loads **stage first, then Game** (`GameSession.LoadStageThenGame`). Clients get the same events in order. Late joiners: NGO's client synchronization loads all scenes and spawns in-scene NetworkObjects only after all of them are loaded (verified in NGO source `NetworkSceneManager.OnClientBeginSync`), so order inside the sync list does not matter.
  - A stage loaded before Game **holds its `[Stage] Content` inactive** (`StageLayout.Awake`, order −900, only when no core/turret is loaded yet) so the swarm does not start, no waves spawn and `FortressMatchRunner` does not begin the match without a core. `StageLayout.TryApply` (called by `StageBootstrap.Awake` in Game, or by the layout itself) moves core/turrets, applies the camera preset, **then reactivates the content** (SwarmSystem `Awake` → `Current` → runner begins with the right core and rules), then marks the nav field dirty.
  - Standalone (Game alone in the editor, test bats `-fortress-start …`, offline character select → `Game`): `StageBootstrap.Awake` (order −1000) loads the stage additively itself (launch arg `-fortress-stage <id|scene|1-based number>` → `catalog.defaultStageIndex` → first playable) and sets `FortressNetworkBootstrap.AutoStartGate` so `-fortress-start` host/client waits until the stage is loaded (max `maxWaitSeconds` 20). It also sets the client's `NetworkSceneManager.VerifySceneBeforeLoading` (on `OnClientStarted`) to **decline catalog stage scenes that are already loaded**: in Single client-sync mode NGO would otherwise load the host's stage a second time (`ClientShouldPassThrough` only passes the active scene).
  - Stage scene opened alone in the editor + Play: `StageLayout` (no core after one frame, network not running) loads `catalog.commonSceneName` additively → Game's bootstrap sees `StageLayout.Active` and applies. Game + a stage opened together: bootstrap finds the loaded stage/`StageLayout` and loads nothing else.
  - Old, unsplit `Game` (its own scene still has a `SwarmSystem`): bootstrap does nothing.

## 1. Paths / asmdefs
```
Assets/Stages/
  Scripts/Runtime/  MS2026.Stages.Runtime (ns MS2026.Stages; refs MS2026.Fortress.Runtime, DDrive.Runtime, DDrive.Foundation, Unity.Netcode.Runtime, UnityEngine.UI)
    StageDefinition.cs  StageCatalog.cs (+ StageRouletteSettings)  StageLayout.cs  StageBootstrap.cs
    Roulette/ StageRouletteMath.cs (pure)  StageRouletteView.cs (runtime-built overlay UI)
  Scripts/Editor/   MS2026.Stages.Editor (ns MS2026.Stages.EditorTools; + MS2026.Stage.Runtime, Unity.Netcode.Runtime)
    StageSplitTool.cs  StageMenu.cs (+ StageChecks)  StageAssets.cs  StageLayoutEditor.cs
  Tests/EditMode/   MS2026.Stages.Tests.EditMode (StageTests: PickIndex, roulette math)
  Resources/StageCatalog.asset   (created by the split tool / Tools menu)
  Data/Stage_1.asset, Stage_2.asset, Stage_N.asset   (StageDefinition, created by the tools)
Assets/Scenes/Stages/Stage1.unity, Stage2.unity, …       (created by the tools)
Assets/_Game/UI/Game/Session/GameSession.Stage.cs        (session side; UI.Game asmdef now references MS2026.Stages.Runtime)
```
Changed existing files: `FortressNetworkBootstrap` (+`AutoStartGate`, start split into `AutoStart`/`AutoStartWhenReady`), `LobbyRoster` (+`LobbyPhase.StageRoulette = 6`, `stageIndex`, `SetStage`), `LobbyMessages` (State: `short stageIndex` after `countdownRemaining` — all PCs must run the same build), `GameSession.CharacterSelect/Match/Auto` (see §4), `LobbyTests` (+`Roster_StageIndexIsCopiedAndCleared`).

## 2. Public API
```csharp
// MS2026.Stages
[CreateAssetMenu("MS2026/ステージ/ステージ")] sealed class StageDefinition : ScriptableObject {
  string stageId="stage1", displayName="ステージ1", subtitle, sceneName="Stage1"; Sprite thumbnail; Color themeColor; bool includeInRoulette=true; string DisplayName; }
[CreateAssetMenu("MS2026/ステージ/ステージの一覧")] sealed class StageCatalog : ScriptableObject {
  const string ResourcePath="StageCatalog"; string commonSceneName="Game"; StageDefinition[] stages; int defaultStageIndex; bool avoidRepeat=true; StageRouletteSettings roulette;
  static StageCatalog Load();  // Resources, cached (reset on SubsystemRegistration)
  int Count; StageDefinition Get(int); int IndexOfScene(string); bool IsStageScene(string); int Find(string key /*id|scene|1-based*/);
  bool IsPlayable(int) /*in list + Application.CanStreamedLevelBeLoaded*/; bool IsInRoulette(int); int RouletteCount; int PickRandom(int previous);
  static int PickIndex(bool[] eligible, int previous, bool avoidRepeat, float random01); Scene FindLoadedStageScene(); }
[Serializable] sealed class StageRouletteSettings { bool enabled=true; float spinSeconds=3.6f, holdSeconds=1.8f; int minLaps=4; float overshootCards=.18f;
  string title="STAGE ROULETTE", caption="ステージ抽選中…", decidedText="決定！"; Color accent=(.35,.95,1), flashColor=(1,.95,.55); Sprite cardFrame; Texture stripes;
  int sortingOrder=900 /*< PasteCurtain 950*/; FortressEffect onStart, onTick, onStop; float TotalSeconds; }
[DefaultExecutionOrder(-900)] sealed class StageLayout : MonoBehaviour { StageDefinition definition; Transform coreAnchor; Transform[4] turretAnchors; CameraViewPreset cameraPreset;
  GameObject content; bool loadCommonSceneWhenAlone=true; static StageLayout Active; static event Action<StageLayout> Applied; bool IsApplied; bool TryApply(bool force=false); Transform AnchorFor(int); }
[DefaultExecutionOrder(-1000)] sealed class StageBootstrap : MonoBehaviour { const string StageArgument="-fortress-stage"; bool loadStageWhenAlone=true; float maxWaitSeconds=20; bool IsLoadingStage; }
static class StageRouletteMath { float FinalPosition(int cards, int chosenSlot, int minLaps); float PositionAt(float t, float spin, float final, float overshoot); int CenterSlot(float p, int cards); int SlotAt(float p, int offset, int cards); }
sealed class StageRouletteView : MonoBehaviour { static bool IsShowing; static StageRouletteView Play(StageCatalog, int chosenIndex, Func<bool> keepVisible); static void Hide(bool immediate=false); }
// MS2026.Fortress.Net
FortressNetworkBootstrap.AutoStartGate  // static Func<bool>; Start() waits (coroutine) while it returns false, only when -fortress-start is given
// MS2026.UI.Game
enum LobbyPhase { …, CharacterSelect = 5, StageRoulette = 6 }   LobbyRoster.stageIndex (-1 = none), SetStage(int)
GameSession.IsStageLoaded
```
- `TryApply`: needs a core or a turret loaded; core → `coreAnchor` position (keeps z); each `LaserTurret` → anchor for its `playerIndex` (position, Z rotation only); `FortressCameraRig.Active.SetPreset(cameraPreset, 0)` + `SnapToTarget()` (skip when the preset is null or already set); reactivate held content; `NavigationField.MarkDirty()`. Idempotent; `Start` and the bootstrap call it again with `force`.
- Roulette math: position in "cards" (0 = first card centred). Final = minLaps·n + chosenSlot. `PositionAt` = final·(1−(1−x)^5) + overshoot·sin(π·(x−0.8)/0.2) for x>0.8 (overshoot then back), exact final at t ≥ spin.

## 3. Session flow (GameSession.Stage.cs + edits)
| Step | Host | Everyone |
|---|---|---|
| Character select done | `ProceedToGame()` → `TryBeginStageRoulette()`: no catalog / no playable stage → `stageIndex = -1`, old flow. Else `Roster.SetStage(ChooseStage())` (`PickRandom(_lastStageIndex)`, static across matches; fallback first playable). If `roulette.enabled` and `RouletteCount ≥ 2`: `ChangePhase(StageRoulette)` + coroutine `WaitForRoulette(TotalSeconds)` → `ProceedAfterRoulette()`. Otherwise `ProceedAfterRoulette()` at once | — |
| Roulette | waits | `OnPhaseReceived` → `OnStageRoulettePhase`: phase became StageRoulette → `StageRouletteView.Play(catalog, Roster.stageIndex, keepVisible = IsOnline && !IsGameLoaded && phase ∈ {StageRoulette, Loading})` |
| Proceed | `ProceedAfterRoulette`: `ChangePhase(Loading)` + `PlayGameStartCover` → `SwapCharacterSelectForGame` (unload Character_Select) → `LoadStageThenGame(FailToRoom)`: NGO `LoadScene(stage, Additive)` (retry while `SceneEventInProgress`), wait until loaded, then `LoadScene(Game, Additive)` (retry), deadline 25 s | clients cover with `PlayGameStartCover` when `before` was CharacterSelect **or StageRoulette** |
| Countdown flow (no character select) | `BeginLoadGame`: `SetStage(ChooseStage())`, `ChangePhase(Loading)`, transition → `LoadStageThenGame` (fail → Waiting + unload stage + message). No roulette | — |
| Game unloaded | `HandleSceneUnloaded(Game)` → `UnloadStageAfterGame()`: host+online → coroutine NGO `UnloadScene(stage)` (retry; fallback local); offline → local; online client → nothing (NGO does it) | — |
| Leaving / auto-recover | `UnloadGameLocally` and `UnloadMatchScenesLocally` also `UnloadStageLocally()` | — |
- `IsMatchSceneLoaded` now includes the stage (transitions wait until the stage is gone too). `UnloadMatchSceneForEveryone` unloads only the stage if Game and Character_Select are not loaded. `FailToRoom` cancels the roulette and unloads a stage that loaded without Game.
- `AutoMatchRules`: StageRoulette is joinable (only Loading/Returning are not); a late joiner during the roulette syncs into Character_Select and then follows.

## 4. Editor tools (menu `Tools/ステージ/…`)
| Menu | What it does |
|---|---|
| `今の Game を共通と Stage1 に分ける（1回だけ）` | Confirm dialog → save prompt → open `Assets/Scenes/Game.unity` → collect: root objects with `StageRoot` or `FortressMatchRules`; children of the `FortressLayoutController` root (fallback: the core's parent) that contain `EnemySpawnPoint`/`EnemySpawnDirector`/`SwarmSystem`/`NavigationField`/`NavigationObstacle`/`DestructibleObstacle` and no core/turret → new additive scene: moved children under `[Stage] Content` (world positions kept), `[Stage] Layout` anchors from the current core/turrets + `rig.preset`, `layout.content` = content root → save `Assets/Scenes/Stages/Stage1.unity`; add `[Stage] Bootstrap` to Game, save. Create `Data/Stage_1` (stage1, ステージ1, 「はじまりの要塞」, cyan), catalog (`Resources/StageCatalog`, `roulette.stripes` = `CharacterSelect/Art/CS_Stripes.png`), copy Stage1 → Stage2, `Stage_2` (stage2, 「ステージ2（仮）」, 「障害物が左右反対」, pink): mirror the top-most transforms with `NavigationObstacle`/`EnemySpawnPoint`/`DestructibleObstacle` under `[Stage] Content` across the core anchor's x (rotation z negated), clear kills × 1.5. Insert stage scenes into the build list right after Game. Opens Game + Stage1. Refuses if `Stage1.unity` exists or nothing to move. Report dialog + log |
| `ステージの一覧を開く` | select/create the catalog |
| `選んだステージを Game と一緒に開く（編集用）` | selected stage scene or StageDefinition (else first stage) + Game, stage scene active |
| `新しいステージを作る（選んだステージを写す）` | copy to the next free `StageN`, `Data/Stage_N` (hue-varied colour), catalog, build list, open with Game |
| `ビルドのシーン一覧を整える` | `StageAssets.SyncBuildSettings` (missing/disabled stage scenes inserted after the common scene) |
| `チェック` | Game has StageBootstrap and no SwarmSystem; each stage: scene exists, in build, exactly one StageLayout/SwarmSystem/NavigationField/EnemySpawnDirector, no NetworkObject, no turret/core |
- `StageLayout` inspector: help text + button `今の砲台・コアの位置を目印に写す` (needs Game open). Gizmos: core circle, turret circles in player colours with a facing line.

## 5. Gotchas
1. **Nothing has run in Unity yet** and the split has not been done: first press the split tool, then Play `Game` (Stage1), set `defaultStageIndex` 1 for Stage2, then test the lobby flow (roulette needs ≥ 2 stages in the roulette).
2. Changing turret/core positions per stage needs a matching `CameraViewPreset` (the current `CameraViewPreset_CoreBottom` bakes per-player rotations for today's turret angles). Stage2 keeps turret/core positions on purpose.
3. Per-stage BGM: `[Fortress] Bgm` stays in Game; use the stage's `StageSet.bgm` (Stage Studio) if stages need their own music (and then empty the Game one).
4. `StageRoot.SwitchAtRuntime` (Stage Studio) is unrelated to this system and still refuses while networked.
5. Stage scenes must not contain NetworkObjects (clients may decline the scene in bat mode; in-scene objects would not spawn).
6. The lobby State message gained 2 bytes (stage index) — mixing builds breaks the lobby protocol (already true for the destructible index).
7. Editor tools that hard-code `Game.unity` (`SessionFlowBuilder`, `FortressFxAssigner`) keep working: the FX assigner processes every build scene (stage scenes included); the session builder only needs the common Game.
8. Stage content stays held (inactive) if a stage is loaded and Game never arrives (e.g. a failed load) — `FailToRoom` unloads it.
9. Other people's art must not be given to AI (CLAUDE.md 2026-10-13); the roulette UI is drawn in code (+ the project's own CS_Stripes texture).

## 6. Related
`MultiplayerProgress.md` §5i · `UIStudio_AI_Reference.md` §7 (GameSession, LobbyPhase, State message) · `CharacterSelect_AI_Reference.md` (ProceedToGame, PasteCurtain) · `StageStudio_AI_Reference.md` (StageRoot / StageSet now live in stage scenes) · `FortressDesigner_AI_Reference.md` (Net: hubs, `AutoStartGate`; layout; match rules).
