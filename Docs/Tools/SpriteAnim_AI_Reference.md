# SpriteAnim (`com.ms2026.spriteanim` v0.1.0) — AI Agent Reference

> Cold-start brief for Claude Code. Verified against source on **2026-10-03**.
> Human guide: `Docs/Tools/SpriteAnim_ガイド.html`. Package README: `Packages/com.ms2026.spriteanim/README.md` (menu path there is stale; real ones below).
> Embedded, self-contained package (deps: `com.unity.ugui` only). If you change it, update this file + the HTML guide in the same task.

## 0. Mental model
- Lightweight 2D sprite animation **without Animator/AnimationClip**. `SpriteAnimationSet` (SO, one per character) holds `SpriteAnimation`s (SO **sub-assets** of the set) made of `SpriteAnimationFrame { Sprite sprite; float duration; string eventName; }`. `SpriteAnimator` component plays by **name** on a `SpriteRenderer` or UI `Image` (abstracted as `ISpriteAnimationTarget`: `SpriteRendererTarget` / `ImageTarget`).
- Cross-fade = temporary child "`<name> (Blend Layer)`" GameObject duplicating the target and alpha-blending; destroyed when blend ends.
- **Project status:** not used by game code yet. Fortress swarm enemies use their own GPU sprite-sheet system (not this).

## 1. Layout
```
Packages/com.ms2026.spriteanim/
  Runtime/ (ns MS2026.SpriteAnim)
    Core/ SpriteAnimation, SpriteAnimationFrame, SpriteAnimationSet, SpriteLoopMode, TransitionRule
    Playback/ SpriteAnimator, ISpriteAnimationTarget, SpriteRendererTarget, ImageTarget, SpriteAnimationPlayback, SpriteAnimationStringEvent (UnityEvent<string>)
  Editor/ (ns MS2026.SpriteAnim.Editor)
    Windows/SpriteAnimStudioWindow.cs   Slicing/SpriteSheetSlicer.cs, TestSheetGenerator.cs   CodeGen/SpriteAnimNameCodeGenerator.cs
    Inspectors/SpriteAnimatorEditor.cs (animation-name dropdown + Play/Stop test buttons in Play Mode), SpriteAnimationSetEditor.cs
```

## 2. Runtime API
```csharp
// [AddComponentMenu("MS2026/Sprite Anim/Sprite Animator")] [DefaultExecutionOrder(-1)]
// serialized: animationSet, defaultAnimation, playOnEnable=true, useUnscaledTime=false, randomizeStartFrame=false,
//             speed=1 [0..5], targetSpriteRenderer / targetImage (auto-resolved)
void Play(string name, float crossFadeSeconds = -1f);  // same anim already playing → no-op (safe every frame)
                                                       // -1 → set.GetCrossFadeSeconds(prev, next); 0 → instant
void PlayOneShot(string name, string returnTo, float crossFadeSeconds = -1f); // after completion → Play(returnTo)
void Stop(); void Pause(); void Resume(); bool HasAnimation(string name);
bool IsPlaying; string CurrentAnimationName; float NormalizedTime; float Speed {get;set;}; SpriteAnimationSet AnimationSet {get;set;}
event Action<string> AnimationStarted, AnimationCompleted, AnimationLooped, FrameEvent;   // arg = anim name / frame eventName
SpriteAnimationStringEvent onAnimationStarted, onAnimationCompleted, onAnimationLooped, onFrameEvent;  // Inspector wiring
```
- Unknown set/name → `Debug.LogWarning` ("[SpriteAnimator] …") and return (no exception).
- `SpriteAnimation`: `AnimationName` (falls back to asset name), `Frames`, `LoopMode`, `Speed` (≥0.01), `FrameCount`, `TotalDuration`; editor setters `EditorSet*`.
- `SpriteLoopMode { Once (stop at end, fires Completed), Loop, PingPong (1,2,3,2,1,2…), ClampForever (hold last frame, IsPlaying stays true) }`.
- `SpriteAnimationSet` (CreateAssetMenu `MS2026/Sprite Anim/Sprite Animation Set`): `animations`, `defaultCrossFadeSeconds = 0.1`, `transitionRules: List<TransitionRule{ from, to, crossFadeSeconds }>` — first rule where `(from=="*"||from==prev) && (to=="*"||to==next)` wins, else default. `Find(name)`. No dedicated GUI for rules (edit array in Inspector).
- `SpriteAnimation` also has CreateAssetMenu `MS2026/Sprite Anim/Sprite Animation` (standalone), but the Studio creates them as sub-assets.

## 3. Sprite Anim Studio (`MS2026/スプライトアニメ/スプライトアニメスタジオ`)
Sections (exact labels):
1. `① スプライトシートを読み込んでスライス` — `Sprite Sheet`, `Columns`, `Rows`, `Padding X/Y`, `Margin X/Y`, `Pixels Per Unit`; `スライス実行` (`SpriteSheetSlicer.SliceGrid` → sets importer `spriteImportMode = Multiple`, writes sprite rects, `SaveAndReimport` — **modifies the texture's .meta**); `テスト用シートを生成 (実素材が無くても動作確認できます)` → `TestSpriteSheet_{c}x{r}.png` (64 px cells, HSV colors + index dots) saved next to the current sheet or in `Assets/`, auto-sliced. Result grid: click / Shift / Ctrl select → `選択した N 枚を現在のアニメーションにフレーム追加`.
2. `② アニメーションセット` — `Animation Set` field, `新規セットを作成` (SaveFilePanelInProject), `Default Cross Fade (sec)`; ReorderableList `Animations` (+ creates sub-asset via `AssetDatabase.AddObjectToAsset`); selected anim: `Name`, `Loop Mode`, `Speed`, `FPS` + `全フレームに一括適用`; ReorderableList `Frames (ドラッグで並べ替え可能)` rows = sprite | duration | eventName.
3. `③ プレビュー` — play/pause, `|<` `Stop` `>|`, scrub, speed, onion skin.
4. `④ 遷移（クロスフェード）テスト` — `From`, `To`, `Cross Fade (sec)` 0–2, `テスト実行`, shows `Blend: n%`.
5. `⑤ ツール` — `選択中のスプライトから自動でアニメーションを作成` (Project-selected sprites grouped by regex `^(.*?)[_\-\s]?(\d+)$`, sorted by number); `アニメーション名の定数クラスを生成 (C#)` → SaveFilePanel (default `<SetName>Anims.cs`), class name = file name, no namespace, `public const string X = "AnimName";` with auto-generated header.

## 4. Gotchas
1. Blend layer duplicates renderer → special materials (additive, ShaderFX) may look different during cross-fade; a ShaderFX `EffectTarget` would not be on the blend layer.
2. Slicing rewrites import settings of the source texture (affects other users of that texture).
3. `Play(name)` is ignored only while that same anim is **currently playing** (`next == _current && _isPlaying`) — to restart a playing anim use `Stop()` then `Play()`; after a `Once` anim completed, `Play` restarts it.
4. Frame events fire by name only; no payload.
5. README says menu `MS2026 > Sprite Anim > Sprite Anim Studio` — actual is `MS2026/スプライトアニメ/スプライトアニメスタジオ`.

## 5. Porting
Copy `Packages/com.ms2026.spriteanim` + add `"com.ms2026.spriteanim": "0.1.0"` to manifest (already embedded here).
