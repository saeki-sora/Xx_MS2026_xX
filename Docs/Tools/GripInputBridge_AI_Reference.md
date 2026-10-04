# Grip Input Bridge (握力入力ブリッジ) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-03** (working tree includes uncommitted edits to `GripDeviceConfig`, `SerialGripTransport`, `GripSerialProtocol`, tests).
> Human guide: `Docs/Tools/GripInputBridge_ガイド.html`. Design intent (may describe unimplemented phases): `Docs/Tools/GripInputBridge_設計仕様書.md`, `…_AI実装版.md` — **code is the source of truth**; this file states what is actually implemented.
> If you change anything under `Assets/_Tools/GripInputBridge/` or `Assets/ArduinoSerialReader.cs`, update this file and the HTML guide in the same task.

## 0. Mental model
- Input layer between grip sensors (4 players, fixed: `GripInputBridgeConstants.PlayerCount = 4`) and the game. Game reads one normalized value 0–1 per player; source can be keyboard simulator, serial hardware, or a recorded log — hot-swappable at runtime.
- Pipeline per player, sampled **once per frame**: `transport.GetRawValue` (0–1) → `IGripSignalFilter.Filter` → `GripCalibrationProfile.Normalize` (dead zone → min–max → response curve) → `GripEdgeDetector.Update` (hysteresis) → cached values + `OnGripStarted/OnGripReleased`.
- Zero setup: `GripInputBridge.Provider` lazily creates a provider with the shared `GripInputBridge.Simulator` (one `SimulatedGripTransport` for the whole process); hidden `GripInputBridgeRuntimeDriver` (`[DefaultExecutionOrder(-1000)]`, `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, DontDestroyOnLoad, HideInHierarchy) calls `GripInputBridge.Tick()` every Update.

## 1. Files / asmdefs
```
Assets/_Tools/GripInputBridge/
  Runtime/ (asmdef MS2026.GripInputBridge.Runtime, rootNamespace MS2026.GripInputBridge, refs Unity.InputSystem)
    GripInputBridge.cs (static facade) · GripInputBridgeRuntimeDriver.cs · GripInputBridgeConstants.cs · GripDeviceStatus.cs
    IGripTransport.cs · IGripInputProvider.cs
    Provider/GripInputProvider.cs
    Processing/ IGripSignalFilter, PassthroughFilter, MovingAverageFilter, ExponentialMovingAverageFilter, MedianFilter, MedianThenEmaFilter, GripFilterFactory, GripEdgeDetector
    Data/ GripDeviceConfig, GripFilterSettings, GripCalibrationProfile  (ScriptableObjects)
    Transports/ SimulatedGripTransport, CompositeGripTransport, GripSimulationMath, GripWaveformPreset(+Math), GripSerialProtocol
    Recording/ GripSessionRecorder, GripLogEntry, ReplayGripTransport
    SerialTransport/ (asmdef MS2026.GripInputBridge.SerialTransport, ns MS2026.GripInputBridge.Transports) SerialGripTransport.cs  — whole file #if MS2026_GRIP_SERIAL_ENABLED
  Editor/ (asmdef MS2026.GripInputBridge.Editor) GripInputBridgeWindow/GripInputBridgeWindow.cs + .uss
  Tests/EditMode/ (asmdef MS2026.GripInputBridge.Tests.EditMode) — protocol, filters, edge detector, calibration, simulation math, waveform, recording, provider, device config
Assets/ArduinoSerialReader.cs   (Assembly-CSharp; scene component for Arduino hookup; present in Game.unity)
CalibrationProfiles folder: Assets/_Tools/GripInputBridge/CalibrationProfiles/ (created on first save; does not exist yet)
GripLogs/ (project root, outside Assets) — recordings
```
- ProjectSettings: `MS2026_GRIP_SERIAL_ENABLED` defined for **Standalone**; `apiCompatibilityLevel: 3` (.NET Framework) → serial compiled for PC target. Changing ProjectSettings is a human task.

## 2. Public API
```csharp
namespace MS2026.GripInputBridge
static class GripInputBridge {
  IGripInputProvider Provider { get; }              // lazy: new GripInputProvider(Simulator)
  SimulatedGripTransport Simulator { get; }         // the ONE keyboard simulator; window presets + composite use it. Never `new SimulatedGripTransport()` for the game.
  IGripTransport CurrentTransport { get; }          // check before restoring: only undo your own SetTransport
  void SetTransport(IGripTransport t);              // throws on null; last caller wins (single global slot)
  void SetFilterSettings(GripFilterSettings s);     // rebuilds 4 filters + edge detectors (state reset)
  void SetCalibrationProfiles(GripCalibrationProfile[] p); // null entries → CreateDefault(i) (linear, deadZone 0.03)
  void StartRecording(string dir = null);           // restarts if already recording; default <project>/GripLogs
  void StopRecording();
  string CurrentRecordingFilePath { get; }  bool IsRecording { get; }
  internal void Tick();                              // driver only; records raw/filtered/normalized for 4 players when recording
}
interface IGripInputProvider {
  float GetGripValue(int p);   // final 0-1 (game uses this)
  float GetRawValue(int p);    // transport value before filter/calibration
  bool  IsGripping(int p);     // edge-detector state
  event Action<int> OnGripStarted, OnGripReleased;
  GripDeviceStatus GetStatus(int p);  // Connected / Disconnected only (Stale, Suspicious defined but unused)
}
// GripInputProvider also exposes Transport, GetFilteredValue(p), Update(), SetTransport/SetFilterSettings/SetCalibrationProfiles.
interface IGripTransport { bool IsConnected(int p); float GetRawValue(int p); event Action<int> OnConnected, OnDisconnected; }
```
- Invalid player index → 0 / false / Disconnected (no exceptions). Reads before first Update trigger one sample (`EnsureSampledAtLeastOnce`).
- Filters/edge detectors are stateful → never call `Filter`/`Update` more than once per sample (provider caches per frame for this reason).

## 3. Transports
| Transport | Construct | Behaviour |
|---|---|---|
| `SimulatedGripTransport` | `(Key[] keys = {Q,W,O,P}, rise = 3.0/s, decay = 1.5/s)` | Input System `Keyboard.current`; held → +rise·dt, released → −decay·dt, clamp 0–1; updates once per frame per player. `SetPreset(p, GripWaveformPreset)` overrides keys; `SetConnected(p, bool)` fakes disconnect. Presets: `None`, `Soft` (0.2–0.3 sine 0.5 Hz), `Firm` (0.5–0.6), `Full` (0.9–1.0), `RhythmTestPulse` (0.4 s beats: 0.8 / 0). |
| `SerialGripTransport` (define-gated, `IDisposable`) | `(GripDeviceConfig)` | Opens `SerialPort(name, baud)`, ReadTimeout 500, NewLine "\n"; background thread `ReadLoop` parses with `GripSerialProtocol.TryParseSamples`; values via `Volatile`. `IsConnected` = received at least once **and** last sample ≤ `heartbeatTimeoutMs` ago. `GetRawValue` = `config.ToGripRawValue(received)`; `GetReceivedValue` = pre-conversion (diagnostics). Open failure → `Debug.LogError`, transport inert, `IsPortOpen == false` (callers must check and not install it). `OnConnected` fires once per player on first sample (from background thread!). `OnDisconnected` never fired. **Must `Dispose()` when swapping** (frees COM port). |
| `CompositeGripTransport` | `(IGripTransport device, IGripTransport fallback)` | Per player: device connected → `max(device, fallback)`, else fallback. `IsConnected` = either; `IsDeviceConnected(p)` = device only. Forwards device `OnConnected/OnDisconnected`. Always reads fallback each call (keeps simulator ramp advancing). Does **not** dispose the device. Used by `ArduinoSerialReader` and the window's `実機` with `fallback = GripInputBridge.Simulator`, so keyboard always works alongside hardware. Tested (`CompositeGripTransportTests`). |
| `ReplayGripTransport` | `(string jsonlPath)` | Loads all `GripLogEntry` lines; plays `raw` values by wall-clock (`Stopwatch` started in ctor) — feed goes through current filter/calibration again. `Restart()`. Connected iff player has samples. |

### Serial protocol (`GripSerialProtocol`, pure, tested)
- Line formats (trimmed): `"<int 0-1023>"` → P1 (simple, Arduino Uno 10-bit); `"a,b[,c[,d]]"` (≤4 ints 0-1023) → P1..P4 (`TryParseSamples`); `"G,<player 0-3>,<raw 0-4095>,<deviceMs>"` (spec format, 12-bit). Invalid lines → false (skipped). `BuildLine(p, raw, ms)` builds G-format. Constants `RawValueMax=4095`, `SimpleFormatRawValueMax=1023`.

## 4. Data assets (CreateAssetMenu `MS2026/Grip Input Bridge/…`) — none exist in the project yet
| Asset | Fields (default) | Notes |
|---|---|---|
| `GripDeviceConfig` (Device Config) | `serialPortName="COM3"`, `baudRate=115200`, `heartbeatTimeoutMs=200`, `autoAssignPlayerIndex` (reserved, no effect), `centeredSensor=false`, `sensorCenter01=0.5` | `ToGripRawValue(v)`: centered → `|v−center| / max(center, 1−center)` clamp 0–1 (for linear Hall sensors like 49E idling ~512). |
| `GripFilterSettings` (Filter Settings) | `filterType=MedianThenExponentialMovingAverage`, `windowSize=3`, `emaAlpha=0.3`, `onThreshold=0.12`, `offThreshold=0.08` | **Not applied anywhere by default** → provider uses `PassthroughFilter` + default thresholds 0.12/0.08. Call `SetFilterSettings` to enable. `GripEdgeDetector` throws if on < off. |
| `GripCalibrationProfile` (Calibration Profile) | `playerIndex`, `minRawValue=0`, `maxRawValue=1`, `responseCurve=linear`, `deadZone=0.03` (0–0.3), `lastCalibratedAt` | `Normalize(f)`: `f<=deadZone→0`; `(f−min)/(max−min)` clamp; curve; clamp. Range 0 → 0. |

## 5. Editor window (`Tools/握力入力ブリッジ/ウィンドウを開く`, title 握力入力ブリッジ, UI Toolkit + USS)
- Tabs: `はじめに` (3-step intro), `シミュレータ` (per-player dot/status/bar % + `波形プリセット` dropdown `なし(キーボード操作)/そっと/しっかり/渾身/リズムテスト` + `キーボード操作キー: Q`), `キャリブレーション` (Play-only; player dropdown; `計測開始(3秒間、最大握力で握ってください)` records max **raw** over 3 s; `この結果を保存` → `Assets/_Tools/GripInputBridge/CalibrationProfiles/P{n}_CalibrationProfile.asset`, `maxRawValue = max(observed, 0.01)`, then `SetCalibrationProfiles` from disk), `診断` (4-row status/value table).
- Persistent top bar: `入力元:` dropdown `シミュレータ` / `実機` (only if define) / `記録再生` (file picker in GripLogs, `.jsonl`) and `● REC` / `■ 停止` toggle.
- Player colors P1 #E5484D, P2 #348CF2, P3 #40B373, P4 #F2CC27 (reused by `FortressColors`).
- **Opening/recreating the window never changes the transport** (fixed 2026-10-03: it used to call `SetTransport(simulator)` in `CreateGUI`, which — because windows are recreated after the Play-mode domain reload — overwrote `ArduinoSerialReader`'s hardware input = the "device doesn't respond while the window is open" bug). The `入力元` dropdown only mirrors `CurrentTransport` (synced every tick: Replay → 記録再生, Composite → 実機, Simulated → シミュレータ). Choosing `シミュレータ` sets `GripInputBridge.Simulator`. `実機` opens the **first** `GripDeviceConfig` (`AssetDatabase.FindAssets`; dialog if none); if `IsPortOpen` is false → dialog and no switch; else installs `CompositeGripTransport(serial, Simulator)`. The window disposes only ports it opened itself (on switching away and in `OnDisable`). Waveform presets act on `GripInputBridge.Simulator` (so they also apply while on 実機). Diagnostics tab shows `実機 接続中` / `実機なし(キーボード)` while a composite is active.
- `rootVisualElement.schedule.Execute(OnTick).Every(100)` drives the calibration countdown (delta from `EditorApplication.timeSinceStartup`) and refreshes the visible tab's table.

## 6. `ArduinoSerialReader` (Assets root, Assembly-CSharp)
- Scene component (in `Game.unity`). Fields: `deviceConfig` (optional; overrides inline), `portName="COM3"`, `baudRate=9600`, `heartbeatTimeoutMs=1000`, `centeredSensor=true`, `sensorCenter01=0.5`, debug `logValues=true`, `logIntervalSeconds=0.3`, `logPlayerCount=2`.
- `Start()` → opens `SerialGripTransport(config)`; **if the port can't be opened (no device, wrong port, busy) it disposes it, logs a warning and leaves the input as is (keyboard keeps working)** — previously it installed the dead transport and killed keyboard input (cause of "no laser in builds without the device", 2026-10-03). On success installs `CompositeGripTransport(serial, GripInputBridge.Simulator)`. `OnDestroy` → restores `Simulator` only if `CurrentTransport` is still its composite, then `Dispose()`. Game.unity currently uses port `COM5`. Logs `P{n}: arduino=<0-1023> raw=<0-1> grip=<0-1> <status>` only while the serial port is actually in use (`IsDeviceActive`; since 2026-10-04 nothing is logged when the port could not be opened or serial is disabled — the 0.3 s Debug.Log was a constant GC/CPU cost in builds). Without define: logs how to enable and stays on simulator.

## 7. Consumers
- `Assets/_Game/Fortress/Runtime/Laser/LaserTurret.cs` — `IsGripping(playerIndex)` + `GetGripValue(playerIndex)` each frame (→ thickness/heat) when offline. In network play, `Assets/_Game/Fortress/Runtime/Net/TurretNetworkHub.cs` reads sensor `localGripSensorIndex` (default 0) for the local player on every PC and the host feeds remote players' values to the turrets. `Fortress/Editor/.../TestTabView.cs` displays values. Fortress designer opens this window via `EditorApplication.ExecuteMenuItem("Tools/握力入力ブリッジ/ウィンドウを開く")`.

## 8. Gotchas (current implementation gaps — mention when relevant, don't "fix" silently)
1. Calibration profiles are only applied right after saving in the window; **not loaded on Play start or in builds**. No runtime loader exists.
2. `GripFilterSettings` never applied → no noise filtering in practice.
3. `GripDeviceStatus.Stale/Suspicious`, `autoAssignPlayerIndex`, `SerialGripTransport.OnDisconnected` unused.
4. Serial `OnConnected` invoked on background thread — don't touch Unity API in handlers.
5. COM port must be released (Dispose); Arduino IDE serial monitor blocks the port.
6. Baud defaults differ: ArduinoSerialReader 9600 vs GripDeviceConfig 115200; heartbeat 1000 vs 200 ms.
7. Simulator uses `Time.deltaTime` (scaled) and `Keyboard.current` (Game view focus needed).
8. Replay plays raw values; filter/calibration are re-applied (intended for A/B tuning).
9. Recording writes 4 lines per frame (`{"t","p","raw","filtered","normalized"}`), AutoFlush off — call `StopRecording()` to flush.
10. The transport is a single global slot (last `SetTransport` wins). Undo only your own install (compare `CurrentTransport`), and never replace the input with a transport that has no data (check `SerialGripTransport.IsPortOpen`).
11. `CompositeGripTransport` forwards the serial `OnConnected` as-is, so it can also arrive on the background thread.

## 9. Tests
EditMode tests cover protocol parsing/round-trip, filters, edge detector, calibration normalize, simulation math, waveform math, recording/replay, provider caching, device config conversion, composite (device+keyboard) transport. Run them after touching Runtime.
