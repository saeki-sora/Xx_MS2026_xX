# 録画しながらプレイ / Play Recorder (`com.ms2026.playrecorder` v1.0.0) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-16** (compiled against Unity 6000.3.13f1 and 2022.3.62f2 reference DLLs; **not yet run inside the Unity editor** — first real recording still to be checked by the user).
> Human guide: `Docs/Tools/PlayRecorder_ガイド.html`. Package README (travels with the package to other projects): `Packages/com.ms2026.playrecorder/README.md`.
> Embedded, dependency-free, **editor-only** package meant to be copied into other projects/PCs. If you change it, update this file + the HTML guide + the package README in the same task.

## 0. Mental model
- Adds a **「● 録画プレイ」** button to the main toolbar (Middle dock = next to Play/Pause/Step). Click → enters Play Mode and records the Game view + audio. Stopping play (or clicking the button again) stops recording; back in Edit Mode the session is encoded **in the background, time-sliced** into an MP4 that is guaranteed (best-effort, verified) to be **≤ size limit (default 20 MB = 20,000,000 bytes)**.
- **Never truncates.** Size is met by choosing quality *after* the length is known: `video bps = (limit·0.92 − 64KB − audioBytes/s·duration)·8 / duration`, then pick the best `QualityRung` (height, fps) whose bits-per-pixel ≥ 0.07; cap at 0.2 bpp so short clips stay small. After encoding, the real file size is checked; if over, bitrate is scaled by the measured overshoot (and rung lowered if the encoder ignored the bitrate by >1.4×), audio downgraded Stereo→Mono→None if it eats the budget, and re-encoded (max 5 attempts).
- **Why 20 MB:** matches Discord's free-plan per-file upload cap (raised 10 MB → 20 MB in Aug 2026; per third-party reports of Discord's announcement, checked 2026-10-16; Nitro Basic 50 MB, Nitro 500 MB). The user asked (2026-10-16) that the docs advertise "fits Discord without being rejected". The limit is counted as 20,000,000 bytes (< 20 MiB) with a 0.92 target + post-encode size check, so it stays under whichever MB definition Discord uses. If Discord changes the cap, the docs (guide `#discord` block, `#rc-discord`, FAQ, index card, README, package.json description) all mention it.
- Uses only built-in Unity APIs: `UnityEditor.Media.MediaEncoder` (H.264 + AAC on Win/Mac, VP8 `.webm` on Linux), `ScreenCapture.CaptureScreenshotIntoRenderTexture`, `AsyncGPUReadback`, `ImageConversion.EncodeArrayToJPG` (thread-safe), `OnAudioFilterRead`. **Not** the Unity Recorder package.
- Project status: added 2026-10-16; not referenced by game code (pure editor tool).

## 1. Layout
```
Packages/com.ms2026.playrecorder/          package.json ("unity": "2021.3", no deps), README.md
  Hooks/MS2026.PlayRecorder.Hooks.asmdef    (any platform + defineConstraints ["UNITY_EDITOR"] → compiled only in the editor, never in builds; autoReferenced=false)
  Hooks/ (ns MS2026.PlayRecorder.Hooks, public) — the only MonoBehaviours; they know nothing about the editor code
    PlayRecorderCapture.cs      Action EndOfFrame (WaitForEndOfFrame coroutine), Action Tick (Update)
    PlayRecorderAudioTap.cs     volatile Action<float[],int> Sink (called on the audio thread from OnAudioFilterRead, read-only), volatile bool Paused
  Editor/MS2026.PlayRecorder.Editor.asmdef  (Editor only, autoReferenced=false, references MS2026.PlayRecorder.Hooks)
  Editor/ (ns MS2026.PlayRecorder.Editor, all internal)
    PlayRecorderController.cs   [InitializeOnLoad] state machine: Toggle(), play-mode hooks, encode queue, Progress API
    ActiveRecording.cs          one recording: hidden host GO, capture, audio tap attach, pause-aware clock, Stop()
    FrameStoreWriter.cs         background thread: RGBA → JPEG(q88) → frames.bin/frames.idx; float audio → audio.raw
    SessionFiles.cs             SessionMeta (meta.json), FrameEntry, temp dir Library/PlayRecorder/Sessions/<yyyy-MM-dd_HH-mm-ss>/
    RecordingPlan.cs            pure math: ladder, bitrate budget, rung choice, audio mode choice (Unity-free; testable)
    MovieEncodeJob.cs           time-sliced encoder with size verification + retries; writes final file
    AudioCalibration.cs         measures real AAC bytes/s = size(64×64 black 1 fps video + 6 s noise) − size(same video only); Unity cannot write audio-only files ("MediaEncoder::Create no audio encoder factory registered"). Cached in EditorPrefs per Unity version/ext/rate/ch; failure → 24000/12000 B/s estimate
    AudioReader.cs              reads audio.raw, downmixes to stereo/mono (center ×0.707 into L/R for ≥3ch), pads silence
    GpuScale.cs                 Blit with halving steps (anti-alias) + optional Y flip; RT helpers (ARGB32 sRGB)
    PlayRecorderToolbar.cs      #if UNITY_6000_3_OR_NEWER → [MainToolbarElement]; #else → reflection into UnityEditor.Toolbar m_Root "ToolbarZonePlayMode"
    PlayRecorderUi.cs           label/tooltip/icon ("Animation.Record") + context-menu entries (shared by both toolbars)
    PlayRecorderMenu.cs         Tools/録画しながらプレイ/* menu items + shortcut
    PlayRecorderSettings.cs     EditorPrefs-backed settings (per PC, all projects)
    PlayRecorderSettingsProvider.cs  Preferences page
    PlayRecorderInstaller.cs    copy package to another project; build distributable zip
```

## 2. Flow
1. `Toggle()`: recording → `StopRecording()` (keeps playing). Playing → `StartRecording()`. Edit mode → `SessionState["MS2026.PlayRecorder.RecordOnPlay"]=true` + `EditorApplication.EnterPlaymode()`; `EnteredPlayMode` (or the `Tick` fallback) starts recording. Survives domain reload on enter-play.
2. `ActiveRecording.Start()`: creates session dir, writes meta (`complete=false`) immediately (crash salvage), hidden `HideAndDontSave` + `DontDestroyOnLoad` GO with `PlayRecorderCapture`.
3. Each end-of-frame: skip if paused / interval < 0.9/fps / writer backlog > 6 (counted as dropped; filled by repeating previous frame at encode). Capture full screen → downscale to current capture rung (`RecordingPlan.CaptureRung(elapsed)` — the rung you'd get if you stopped now; monotonic, so later frames are never captured finer than needed) → async readback → writer.
4. Audio: every 0.5 s find an `isActiveAndEnabled` `AudioListener`; (re)attach tap when it changes (scene loads); gaps > 0.2 s vs. `elapsed·sampleRate` are filled with silence (keeps A/V sync). Channel count pre-set from `AudioSettings.speakerMode`.
5. Pause: `EditorApplication.pauseStateChanged` → clock excludes paused time, tap discards samples.
6. Stop (`ExitingPlayMode`, button, `beforeAssemblyReload`, `quitting`): `AsyncGPUReadback.WaitAllRequests()`, writer drains + closes, meta `complete=true` with duration/frames.
7. `Tick` (EditorApplication.update) in Edit Mode only (not while playing/compiling/updating): scans `PendingSessions` every 2 s (dirs with meta.json, without failed.txt), runs `MovieEncodeJob.Step(25 ms)` (150 ms when Unity is not the active app). Progress shown via `UnityEditor.Progress` ("録画を書き出し中"), cancellable → dialog 「捨てる」/「書き出しを続ける」.
8. Success: file moved to `<OutputFolder>/<ProjectName>_<createdAt>.mp4` (suffix `_2…` if exists), session dir deleted, `LastOutputPath` stored, Console log with size/resolution/fps/audio, `RevealInFinder` if enabled. Failure: `failed.txt` written in session dir (kept, not retried). Domain reload mid-encode: job disposed, restarts from scratch afterwards.

## 3. Quality ladder & numbers (`RecordingPlan`)
- Base ladder (clamped to `MaxHeight`, `MaxFps`, captured/source height; duplicates removed): 1080p60, 1080p30, 720p30, 540p30, 480p30, 360p30, 360p24, 288p20, 240p15, 180p12, 144p10. Widths keep the source aspect, rounded to even.
- Constants: `MinBitsPerPixel 0.07`, `MaxBitsPerPixel 0.2`, `SafetyFactor 0.92`, `ContainerOverheadBytes 64KB`, `MinVideoBitsPerSecond 40k`.
- Audio: `ChooseAudio` → Stereo if stereo bytes ≤ 25 % of budget, else Mono if ≤ 40 %, else None. Fallback estimates if calibration fails: 24000 B/s stereo, 12000 mono.
- Example, 20 MB, 720p/30 max, 16:9, audio estimated 192/96 kbps: 10 s → 720p30 5.5 Mbps (~7 MB file); 60 s → 720p30; 90 s → 540p30; 120 s → 480p30; 180 s → 360p30; 300 s → 360p24 mono; 600 s → 240p15 mono; 20 min → 240p15 no audio; 60 min → 144p10.
- H.264 attrs: `gopSize = fps·2`, `numConsecutiveBFrames = 2`, `H264High`, `bitRateMode = Medium`, `targetBitRate` = plan bps (bits/s). Whether MediaFoundation honours `targetBitRate` exactly is unverified → the size-check loop is the safety net.

## 4. UI (exact labels)
- Toolbar element path / name: `録画しながらプレイ`. **Unity 6.3 registers new `[MainToolbarElement]`s as hidden** (confirmed 2026-10-16: package compiled and loaded but no button). Fix: `PlayRecorderToolbar.ShowOnceUpdate` (6.3 branch) polls every 0.5 s (max 40 tries) and, once per project (EditorPrefs `MS2026.PlayRecorder.ToolbarAutoShown.<projectRoot>`), calls internal `MainToolbar.TryGetOverlay(path, out Overlay)` via reflection and sets `overlay.displayed = true` (fallback: internal `MainToolbar.ShowAll(path)`). If the user hides it afterwards it stays hidden. Manual recovery: menu `ツールバーにボタンを表示する` (legacy branch: re-injects). A one-time Console hint per project also points there. Labels: `● 録画プレイ`, `● 録画プレイ（書き出し 45%）`, `● 録画準備中…`, `● ここから録画` (playing, not recording), `■ 録画停止 m:ss`, `‖ 録画一時停止 m:ss`. Tooltip while recording shows the would-be resolution/fps.
- Right-click menu (both toolbars) = status lines first (`PlayRecorderUi.StatusEntries()`, disabled `MenuEntry.Info` rows): `録画中 m:ss（今止めると 720p 30fps）`, `書き出し中 45%・残り約 m:ss（<job.Status>）` + `　m:ss の録画 → 20MB 以内に` (Edit Mode) or `書き出し {p}（プレイ中は休み。止めると再開）` (Play Mode), `書き出し待ち N 本` (`PlayRecorderController.QueuedCount()` = pending session dirs minus current job/recording), else `書き出し中の録画はありません`; then `進み具合の画面を開く` (`Progress.ShowDetails(false)`, enabled only while a job exists), separator, `録画フォルダを開く`, `最後の録画を再生` (disabled if missing), `設定…`, `ほかのプロジェクトに入れる…`, `配布用 zip を作る…`. ETA = `MovieEncodeJob.SecondsRemaining` (wall time of the current attempt / progress; gaps ≥ 2 s between steps, e.g. Play Mode, are not counted; −1 until progress ≥ 3 % and ≥ 1 s of work; resets per retry attempt).
- Menu `Tools/録画しながらプレイ/`: `録画プレイ（開始・停止）` (**Ctrl+Shift+Alt+R**, `%#&r`), `録画フォルダを開く`, `最後の録画を再生`, `ツールバーにボタンを表示する`, `設定…`, `ほかのプロジェクトに入れる…`, `配布用 zip を作る…`.
- Preferences page `Preferences/録画しながらプレイ`: `上限サイズ (MB)` (20, clamp 1–4000), `最大の解像度` (480/720/1080, default 720), `最大の fps` (30/60, default 30), `音も録る` (ON), `保存先フォルダ` (`Recordings`, + `選ぶ…`/`開く`), `書き出したらフォルダを開く` (ON), `上下反転の補正` (`自動`/`反転する`/`反転しない`; auto = flip when `SystemInfo.graphicsUVStartsAtTop`), table 「録画の長さと画質の目安」 (0:30…20:00), buttons `ほかのプロジェクトに入れる…`, `配布用 zip を作る…`, `既定値に戻す`.
- EditorPrefs keys: `MS2026.PlayRecorder.{LimitMB,MaxHeight,MaxFps,RecordAudio,RevealWhenDone,Flip,OutputFolder}`, `…LastOutput.<projectRoot>`, `…HintShown.<projectRoot>`, `…ToolbarAutoShown.<projectRoot>`, `…AudioBps.<unityVersion>.<ext>.<rate>.<ch>`, `…LastInstallTarget`.

## 5. Distribution
- `InstallToOtherProject()`: folder picker (accepts the project root, or its Assets/Packages folder) → validates `Assets` + `ProjectSettings` → copies the package root (found via `PackageInfo.FindForAssembly`, or the asmdef location if dropped under Assets) to `<target>/Packages/com.ms2026.playrecorder` (asks before overwrite) → warns if target `ProjectVersion.txt` < 2021.3.
- `ExportZip()`: SaveFilePanel (default Desktop `録画しながらプレイ_PlayRecorder.zip`) → `ZipArchive` with `入れ方.txt` + `com.ms2026.playrecorder/**`.
- Embedded packages need no manifest entry; this project does **not** list it in `Packages/manifest.json`.
- Optional: git URL `https://github.com/saeki-sora/Xx_MS2026_xX.git?path=Packages/com.ms2026.playrecorder` (only if the repo is reachable from that PC).

## 6. Gotchas
1. **Recording not yet verified in-editor** (UnityMCP was down when written; the package does compile and load in this project). Things to check on first run: image orientation (→ `上下反転の補正`), colour/gamma (capture/encode RTs are ARGB32 sRGB; JPEG round-trip is byte-exact), audio sync. The 6.3 auto-show uses internal APIs → may break on future Unity versions (fails soft with a warning; menu still works).
2. Captures what the **Game view** renders (incl. overlay UI). If the Game view tab is hidden (Scene view in front), frames may be stale. Game view resize mid-recording → later frames are stretched to the first frame's aspect.
2b. **Never read `Screen.width/height` outside the player loop.** When recording is started from the toolbar button mid-play, `Screen.*` returns the *toolbar's* size (seen: 2560×54 → plan width 22756 → "Texture has out of range width" and the encode failed; fixed 2026-10-17). Source size/aspect are now taken from the first `OnEndOfFrame` (meta `sourceWidth/Height` written there), and `MovieEncodeJob` takes the aspect from `frames[0]` (clamped 0.25–4). A failed session can be retried by deleting its `failed.txt`.
3. Encoding pauses during Play Mode and restarts from scratch after a domain reload (e.g. entering Play with domain reload on while a previous encode is running).
4. Temp data: ~3–5 MB/s at 720p30 early on, shrinking as the capture rung drops; stored under `Library/` and deleted after success. Failed sessions stay with `failed.txt` (delete manually).
5. **MonoBehaviours must NOT live in the Editor-only assembly**: `AddComponent` returns null with "Can't add script behaviour … because it is an editor script" (hit on 2026-10-16 → NullReferenceException in `ActiveRecording..ctor`). That is why `Hooks/` exists: a separate asmdef for all platforms with `defineConstraints: UNITY_EDITOR` (so builds skip it). Hooks expose delegates; `ActiveRecording` wires `EndOfFrame`/`Tick`/`Sink` and clears them on stop. They are added at runtime with `HideAndDontSave` (never serialized). If `AddComponent` still returns null, the ctor throws a readable error and disposes the writer.
6. `OnAudioFilterRead` tap is added to the listener's GameObject; game code that enumerates components on that GO could see `PlayRecorderAudioTap` while recording.
7. Linux editor → `.webm` (VP8/Vorbis); the size loop still applies.
