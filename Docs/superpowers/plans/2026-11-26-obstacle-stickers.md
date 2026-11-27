# 障害物シール 作る手順の計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ステージの壁・壊せる壁にデザイナーの PNG のシールを自動でばらまいて貼り（手で直せる）、ふだんはキラキラ、スキルの攻撃が当たると光り、当て続けると焼けこげるようにする。見え方は A 紙・B ホログラム・C ぷっくりの3種類で、絵柄ごとに選ぶ。

**Architecture:** スキル（赤のレーザー・黄色の矢）は Fortress に置く静的な知らせ口 `SkillSurfaceHits` に当たった点を報告し、Stage 側の `StageStickerLayer` がそれを聞いて 1 枚ずつの光り・焦げ（`StickerHeatState`）を進める。シールは障害物の `Visual` の下の子「Stickers」に 1 つのメッシュとしてまとめ（1枚＝表面に沿わせた 5×5 の格子）、光り・焦げは UV1 でシェーダー `MS2026/Stage/Sticker` に渡す。透け・溶けは既存の `StagePropRenderer` と `StagePropEffects.hlsl` の `StageClip` にそのまま乗る。エディタ側は「絵柄の一覧（`StickerLibrary` の Inspector、PNG → Texture2DArray＋SDF）」「ばらまき（`StickerScatter`）」「スタジオの欄（`PropStickerSection`）」「シーンで1枚ずつ直す（`StickerSceneTool`）」に分ける。

**Tech Stack:** Unity 6 / URP 17.3、C#、HLSL（URP の ShaderLibrary）、UI Toolkit（StudioKit `StudioUi`）、NUnit（EditMode テスト）。

**Spec:** `Docs/superpowers/specs/2026-11-26-obstacle-stickers-design.md`（作る前に必ず読む）。見え方の見本は `Docs/superpowers/specs/2026-11-26-obstacle-stickers-look-demo.html`（ブラウザで開く。シェーダーはこの見え方に寄せる）。

## Global Constraints

- **最初に読む**: リポジトリ直下の `CLAUDE.md`、`Docs/Reference/Beautiful_2D_Analysis.md` の `[CHECK]`、`Docs/Reference/Tool_UI_Analysis.md` の `[CHECK]` と `[T2]`、`Docs/Tools/StageStudio_AI_Reference.md`（古い所はソースが正しい）。
- **ブランチ**: main から切る → `git switch -c 障害物シール main`。今いるブランチの変更は入れない。作業ツリーに他の人の未コミットの変更があっても触らない。
- **やりとり・画面の言葉は日本語**。画面にクラス名・フィールド名を出さない。どの項目にも Tooltip（日本語）を付ける。
- **最初に見える項目は少なく**（T2）。スタジオのシール欄で開いた瞬間に見えるのは「量」「使う絵柄」「ばらまき直す」と枚数の表示だけ。残りは閉じた Foldout の中。
- **反応するのはスキルだけ**。ふだんの砲台のレーザー（`LaserBeamVisual` / `StageLaserHeatScanner` / `StagePropHeat`）はシールに一切つながない。シールのシェーダーは `_StageHeat*`（障害物の熱）を使わない。
- **通信しない**。`SkillSurfaceHits` は各 PC の中だけで流れる。ネットのメッセージを足さない。
- **貼れるのは `StagePropRole.Wall` と `StagePropRole.DestructibleWall` だけ**。
- **焦げはメモリだけ**（保存しない）。シーン・Prefab に保存するのは配置データと設定だけ。
- **`.unity` `.asset` `.prefab` `.meta` をテキストで書き換えない**。アセットの変更は Unity の API（`Undo.RecordObject`・`EditorUtility.SetDirty`・`PrefabUtility.RecordPrefabInstancePropertyModifications`・`AssetDatabase`）で行う。
- **D-Drive のパッケージと DDrive の asmdef は改造しない**。演出（SE・VFX）は今回足さない。
- **ツールの資料（`Docs/Tools/`）・`Docs/Multiplayer/MultiplayerProgress.md` は更新しない**（CLAUDE.md の決まり）。
- **1 タスク = 1 コミット**。コミットの最後の行に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` を付ける。Unity が作った `.meta` も同じコミットに入れる（Unity を開けずに `.meta` が無いときは、その旨を最後の報告に書く）。
- **名前空間**: ランタイム `MS2026.Stage`（`Assets/_Game/Stage/Runtime/Stickers/`）、エディタ `MS2026.Stage.EditorTools`（`Assets/_Game/Stage/Editor/Stickers/`）、テスト `MS2026.Stage.Tests.EditMode`（`Assets/_Game/Stage/Tests/EditMode/`）、知らせ口 `MS2026.Fortress`、Fortress のテスト `MS2026.Fortress.Tests.EditMode`。新しい asmdef は作らない。
- **座標の決まり（このゲーム）**: 遊ぶ面はワールドの XY。**上（カメラ側）は −Z**。シールは「上から下へ」＝ワールドの **+Z 向き**に投影して貼る。スキルの当たりの距離は **XY だけ**で測る。
- **確かめ方**:
  - UnityMCP がつながっていれば、それで Unity のコンパイルと Test Runner（EditMode）を回す。
  - つながっていなければ、Unity が作った csproj（リポジトリ直下の `MS2026.Stage.Runtime.csproj` など）を scratchpad に写し、新しいファイルを `<Compile Include>` に足して `dotnet msbuild` でコンパイルする（参照は `Library/ScriptAssemblies`）。テストはコンパイルまで確かめ、実行と見た目の確認はユーザーに頼む。できなかった事はそのまま報告する。
  - シェーダーは dotnet では確かめられない。Unity が使えなければ「未確認」と報告する。

## Review Focus

- **絵柄を一覧から消した・PNG が見つからない**: そのシールは見えなくなるだけで、例外やほかのシールのずれは起きない（光り・焦げの番号はずれない）→ Task 4 にテスト `Build_MissingDesign_CollapsesToCenterAndKeepsIndices`。
- **壊せる壁が壊れている間にスキルが当たる / 再生した**: 壊れている間は何も起きない。再生したらシールは新品（焦げ 0）→ Task 6 にテスト `ReceiveHit_WhileDestroyed_DoesNothing` 相当の判定関数 `StickerHitFilter.ShouldReceive` のテスト。
- **赤レーザーの複数の筋が同じシールに同時に当たる**: 筋の本数ぶん速く焦げない（そのフレームの一番強い当たりだけ数える）→ Task 3 のテスト `Sustain_TakesMaxNotSum`。
- **障害物の角・ふちでの自動ばらまき**: シールが角からはみ出して宙に浮かない（4 隅のどれかが面から外れる候補は捨てる）→ Task 8 のテスト `Scatter_CubeTop_CornersStayOnTopFace`。
- **モデルを差し替えた・傾けた後**: 古いシールが宙に浮いたままでも、スタジオが「ばらまき直してください」と知らせる → Task 9 で `StickerSurfaceSignature` を比べる手順と、Task 8 のテスト `Signature_ChangesWhenModelMoves`。

---

### Task 1: スキル → シールの知らせ口 `SkillSurfaceHits`

**Files:**
- Create: `Assets/_Game/Fortress/Runtime/Effects/SkillSurfaceHits.cs`
- Test: `Assets/_Game/Fortress/Tests/EditMode/SkillSurfaceHitsTests.cs`

**Interfaces:**
- Produces:
  - `public enum SkillSurfaceHitKind { Sustained, Pulse }`（Sustained＝毎フレーム当たり続ける、Pulse＝一回だけ）
  - `public readonly struct SkillSurfaceHit { public readonly Vector2 Point; public readonly SkillSurfaceHitKind Kind; public readonly int PlayerIndex; public SkillSurfaceHit(Vector2 point, SkillSurfaceHitKind kind, int playerIndex) }`
  - `public static class SkillSurfaceHits { public static event System.Action<SkillSurfaceHit> Hit; public static void Report(Vector2 point, SkillSurfaceHitKind kind, int playerIndex); }`
  - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` で `Hit = null` に戻す（`FortressMatch.ResetStatics` と同じやり方）。クラスの summary に「スキルが壁・障害物に当たった点を流すだけ。通信しない。ふだんの砲台のレーザーは報告しない」と書く。

- [ ] **Step 1: ブランチを切る** — `git switch -c 障害物シール main`
- [ ] **Step 2: 失敗するテストを書く**

```csharp
[Test] public void Report_RaisesHitWithSameValues()
{
    SkillSurfaceHit received = default; var count = 0;
    void OnHit(SkillSurfaceHit h) { received = h; count++; }
    SkillSurfaceHits.Hit += OnHit;
    try { SkillSurfaceHits.Report(new Vector2(1f, 2f), SkillSurfaceHitKind.Pulse, 3); }
    finally { SkillSurfaceHits.Hit -= OnHit; }
    Assert.AreEqual(1, count);
    Assert.AreEqual(new Vector2(1f, 2f), received.Point);
    Assert.AreEqual(SkillSurfaceHitKind.Pulse, received.Kind);
    Assert.AreEqual(3, received.PlayerIndex);
}
[Test] public void Report_WithoutListeners_DoesNotThrow()
{
    Assert.DoesNotThrow(() => SkillSurfaceHits.Report(Vector2.zero, SkillSurfaceHitKind.Sustained, -1));
}
```

- [ ] **Step 3: コンパイルして失敗を確かめる**（`MS2026.Fortress.Tests.EditMode.csproj` を写して）→ Expected: `SkillSurfaceHits` が無いというエラー
- [ ] **Step 4: `SkillSurfaceHits.cs` を書く**（上の Interfaces どおり）
- [ ] **Step 5: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 6: コミット** — `git commit -m "シール: スキルが当たった点を知らせる口（SkillSurfaceHits）を足す"`

---

### Task 2: 赤のレーザーと黄色の矢が当たった点を報告する

**Files:**
- Modify: `Assets/RedSkill/Scripts/Runtime/Red/RedLaserSkillRunner.cs`（`TickBeam` の `UpdateImpactFeedback(direction, dt);` の直後。main では 257 行あたり）
- Modify: `Assets/_Game/Fortress/Runtime/Effects/YellowSkillArrow.cs`（はね返りのループの `PlayFlash(new Vector3(position.x, position.y, transform.position.z));` の直後。main では 516 行あたり）

**Interfaces:**
- Consumes: Task 1 の `SkillSurfaceHits.Report(Vector2, SkillSurfaceHitKind, int)`
- Produces: なし（以後、シールは `SkillSurfaceHits.Hit` を聞くだけ）

- [ ] **Step 1: 赤** — `RedLaserSkillRunner` に `private void ReportSurfaceHits()` を足し、`TickBeam` で `if (_phase == Phase.Firing) ReportSurfaceHits();` と呼ぶ。中身: `for (lane < _lanes.Count)` で `_lanes.Blocked[lane]` の筋だけ `SkillSurfaceHits.Report(_lanes.HitPoint[lane], SkillSurfaceHitKind.Sustained, Turret.playerIndex)`。`_glowLanes`（見た目用の太い筋）は使わない。
- [ ] **Step 2: 黄** — はね返った点で `SkillSurfaceHits.Report(nearest.point, SkillSurfaceHitKind.Pulse, _attacker != null ? _attacker.playerIndex : -1)`。寿命が来て消えるときの `PlayFlash` では報告しない（壁に当たっていないため）。
- [ ] **Step 3: どの PC でも動くか確かめる** — `TickBeam` と `YellowSkillArrow.Update` が Client でも走るか（Host だけの処理の `if` の中に入れていないか）をソースで確かめる。どちらも見た目の処理なので全 PC で走るはず。もし Host だけなら、報告をその `if` の外に置く。結果を最後の報告に書く。
- [ ] **Step 4: コンパイルを確かめる**（`MS2026.RedSkill.Runtime.csproj` と `MS2026.Fortress.Runtime.csproj`）
- [ ] **Step 5: コミット** — `git commit -m "シール: 赤のレーザーと黄色の矢が壁に当たった点を知らせる"`

---

### Task 3: 光り・焦げの計算と、調整の値（`StageLookProfile.stickers`）

**Files:**
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerHeatState.cs`
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerLookSettings.cs`
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerShaderIds.cs`
- Modify: `Assets/_Game/Stage/Runtime/Stage/StageLookProfile.cs`（フィールド追加と `ApplyGlobals` の末尾）
- Modify: `Assets/_Game/Stage/Editor/Inspectors/StageLookProfileEditor.cs`（見出しの表に「シール」を足す）
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerHeatStateTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct StickerHeatRates { public readonly float GlowPerSecond, CoolPerSecond, BurnStartGlow, BurnPerSecond, PulseGlow, PulseBurn; public StickerHeatRates(float glowPerSecond, float coolPerSecond, float burnStartGlow, float burnPerSecond, float pulseGlow, float pulseBurn); public static StickerHeatRates From(StickerLookSettings s); }`
  - `public sealed class StickerHeatState`:
    - `public StickerHeatState(int count)` / `public int Count { get; }`
    - `public float GetGlow(int index)` / `public float GetBurn(int index)`（どちらも 0..1）
    - `public void Sustain(int index, float strength)` — このフレームの当たりを覚える。同じフレームに何度呼ばれても**一番大きい値だけ**残す。
    - `public void Pulse(int index, float strength, in StickerHeatRates rates)` — すぐ反映: `glow = max(glow, PulseGlow*strength)`、`burn = min(1, burn + PulseBurn*strength)`。
    - `public bool Tick(float deltaTime, in StickerHeatRates rates)` — 各シール: 覚えた当たり `s>0` なら `glow = min(1, glow + GlowPerSecond*dt*s)`、そのあと `glow >= BurnStartGlow` なら `burn = min(1, burn + BurnPerSecond*dt*s)`。当たりが無ければ `glow = max(0, glow - CoolPerSecond*dt)`。覚えた当たりは 0 に戻す。値が 1 つでも変われば true。
    - `public bool IsHot { get; }` — glow > 0 か、覚えた当たりがあるシールが1つでもある。
    - `public void Clear()` — 全部 0。
  - `public static class StickerHitMath { public static float Falloff(float distance, float radius) }` — `radius <= 0 || distance >= radius` で 0、それ以外 `1 - distance / radius`。（`StickerHeatState.cs` に同居でよい）
  - `[System.Serializable] public sealed class StickerLookSettings`（全項目に日本語 Tooltip。初期値は見本ページに合わせたもの）:

| フィールド | 型 | 初期値 | 意味（Tooltip の中身） |
|---|---|---|---|
| `glowPerSecond` | float `[Min(0)]` | 3.2 | 当たり続けたときに光る速さ |
| `coolPerSecond` | float `[Min(0)]` | 1.6 | 当たらなくなってから冷める速さ |
| `burnStartGlow` | float `[Range(0,1)]` | 0.55 | この光りを超えると焦げ始める |
| `burnPerSecond` | float `[Min(0)]` | 0.42 | 焦げが進む速さ |
| `pulseGlow` | float `[Range(0,1)]` | 1.0 | 一回だけの当たり（黄色の矢）で光る量 |
| `pulseBurn` | float `[Range(0,1)]` | 0.15 | 一回だけの当たりで焦げる量 |
| `sustainedRadius` | float `[Min(0.01)]` | 0.5 | 当たり続けるスキルが届く半径（m） |
| `pulseRadius` | float `[Min(0.01)]` | 0.6 | 一回だけの当たりが届く半径（m） |
| `borderWidthPx` | float `[Range(0,24)]` | 10 | 白いふちの太さ（絵柄 256px あたり） |
| `borderColor` | Color | (0.984, 0.973, 0.949, 1) | 白いふちの色 |
| `paperShineEverySeconds` | float `[Min(0.5)]` | 6 | 紙: 1枚が光の帯で光る平均の間隔 |
| `paperShineSeconds` | float `[Min(0.05)]` | 0.6 | 紙: 光の帯が横切る長さ |
| `holoCyclesPerSecond` | float `[Min(0)]` | 0.11 | ホログラム: 虹色が流れる速さ |
| `holoTwinklePerSecond` | float `[Min(0)]` | 0.27 | ホログラム: 星がまたたく速さ |
| `puffyBevelPx` | float `[Range(1,24)]` | 12 | ぷっくり: ふちの盛り上がりの幅（px） |
| `puffyShadowOffset` | Vector2 | (0.03, -0.05) | ぷっくり: 影のずれ（シールの大きさに対する割合） |
| `puffyGlintEverySeconds` | float `[Min(0.5)]` | 4.8 | ぷっくり: つやがきらっと光る間隔 |
| `paperGlowColor` | Color(HDR) | #FF9640 | 紙が当たって光る色 |
| `holoGlowColor` | Color(HDR) | #FFF0FF | ホログラムが当たって光る色 |
| `puffyGlowColor` | Color(HDR) | #FFB060 | ぷっくりが当たって光る色 |
| `scorchColor` | Color | #2E221C | 焦げの色 |
| `paperTanColor` | Color | #C9A16A | 紙のふちが黄ばむ色 |
| `emberColor` | Color(HDR) | #FF6E28 | ぷっくりの熱いひびの色 |
| `holoWarnShare` | float `[Range(0,1)]` | 0.3 | ステージ全体でホログラムがこの割合を超えたらスタジオで知らせる |

  - `public static class StickerShaderIds`: `Border = "_StickerBorder"`（値は **px のまま** `borderWidthPx`）、`BorderColor = "_StickerBorderColor"`、`PaperShine = "_StickerPaperShine"`（x=間隔, y=長さ）、`Holo = "_StickerHolo"`（x=虹の速さ, y=星の速さ）、`Puffy = "_StickerPuffy"`（x=盛り上がり幅 **px のまま** `puffyBevelPx`, y,z=影のずれ, w=つやの間隔）、`PaperGlow`/`HoloGlow`/`PuffyGlow`/`Scorch`/`PaperTan`/`Ember` = `"_StickerPaperGlow"` など同じ形の名前。すべて `Shader.PropertyToID` の `static readonly int`。**幅はすべて px で渡し、シェーダーも px の距離 `d` と比べる**（Task 5）。
  - `StageLookProfile` に `public StickerLookSettings stickers = new StickerLookSettings();`（Tooltip:「障害物に貼るシールの光り方・焦げ方・見え方」）。`ApplyGlobals()` の最後で `stickers` の値を上の全体の値（`Shader.SetGlobal*`）に入れる。

- [ ] **Step 1: 失敗するテストを書く**（`rates = new StickerHeatRates(3.2f, 1.6f, 0.55f, 0.42f, 1f, 0.15f)`）

```csharp
[Test] public void Sustain_GlowRisesThenBurnStarts()
{   var s = new StickerHeatState(1);
    s.Sustain(0, 1f); s.Tick(0.1f, rates);   // 光り 0.32 → まだ焦げない
    Assert.AreEqual(0.32f, s.GetGlow(0), 1e-4f); Assert.AreEqual(0f, s.GetBurn(0), 1e-6f);
    s.Sustain(0, 1f); s.Tick(0.1f, rates);   // 光り 0.64 → 焦げ 0.042
    Assert.AreEqual(0.64f, s.GetGlow(0), 1e-4f); Assert.AreEqual(0.042f, s.GetBurn(0), 1e-4f); }
[Test] public void Sustain_TakesMaxNotSum()
{   var s = new StickerHeatState(1);
    s.Sustain(0, 0.5f); s.Sustain(0, 1f); s.Sustain(0, 0.25f); s.Tick(0.1f, rates);
    Assert.AreEqual(0.32f, s.GetGlow(0), 1e-4f); }
[Test] public void Cooling_LowersGlowButKeepsBurn()
{   var s = new StickerHeatState(1);
    for (var i = 0; i < 10; i++) { s.Sustain(0, 1f); s.Tick(0.1f, rates); }
    var burn = s.GetBurn(0); Assert.Greater(burn, 0f);
    for (var i = 0; i < 20; i++) s.Tick(0.1f, rates);
    Assert.AreEqual(0f, s.GetGlow(0), 1e-6f); Assert.AreEqual(burn, s.GetBurn(0), 1e-6f); Assert.IsFalse(s.IsHot); }
[Test] public void Pulse_SetsGlowAndAddsBurnOnce()
{   var s = new StickerHeatState(2);
    s.Pulse(1, 0.5f, rates);
    Assert.AreEqual(0.5f, s.GetGlow(1), 1e-6f); Assert.AreEqual(0.075f, s.GetBurn(1), 1e-6f);
    Assert.AreEqual(0f, s.GetGlow(0), 1e-6f); }
[Test] public void Clear_ResetsEverything()
{   var s = new StickerHeatState(1); s.Pulse(0, 1f, rates); s.Clear();
    Assert.AreEqual(0f, s.GetGlow(0)); Assert.AreEqual(0f, s.GetBurn(0)); Assert.IsFalse(s.IsHot); }
[Test] public void Falloff_IsLinearAndZeroOutside()
{   Assert.AreEqual(1f, StickerHitMath.Falloff(0f, 0.5f), 1e-6f);
    Assert.AreEqual(0.5f, StickerHitMath.Falloff(0.25f, 0.5f), 1e-6f);
    Assert.AreEqual(0f, StickerHitMath.Falloff(0.5f, 0.5f), 1e-6f);
    Assert.AreEqual(0f, StickerHitMath.Falloff(0.1f, 0f), 1e-6f); }
```

- [ ] **Step 2: コンパイルして失敗を確かめる**（`MS2026.Stage.Tests.EditMode.csproj` を写して）
- [ ] **Step 3: `StickerHeatState` / `StickerHeatRates` / `StickerHitMath` / `StickerLookSettings` / `StickerShaderIds` を書き、`StageLookProfile` に `stickers` と `ApplyGlobals` の追記をする**
- [ ] **Step 4: `StageLookProfileEditor` の見出しの表に足す** — 既存の `("熱（レーザーが当たった所）", new[] { ... })` と同じ形で、`("シール（スキルで光る・焦げる）", new[] { ("stickers.glowPerSecond", "光る速さ"), ... })` と `("シールの見え方", new[] { ("stickers.borderWidthPx", "白いふちの太さ"), ... })` の2つ。上の表の全フィールドを、表の「意味」を短くした日本語の名前で並べる。
- [ ] **Step 5: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 6: コミット** — `git commit -m "シール: 光り・焦げの計算と調整の値を足す"`

---

### Task 4: 絵柄の一覧・配置データ・メッシュ作り

**Files:**
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerStyle.cs`
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerLibrary.cs`（`StickerDesign` も同居）
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerPlacement.cs`
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerMeshBuilder.cs`
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerMeshBuilderTests.cs`

**Interfaces:**
- Consumes: Task 3 の `StickerHeatState`
- Produces:
  - `public enum StickerStyle { Paper = 0, Holo = 1, Puffy = 2 }` と `public static class StickerStyleNames { public static string Display(StickerStyle s) }` →「紙」「ホログラム」「ぷっくり」
  - `[CreateAssetMenu(menuName = "Stage/Sticker Library", fileName = "StickerLibrary")] public sealed class StickerLibrary : ScriptableObject`
    - `public const int TileSize = 256; public const int SdfRangePx = 24; public const int PaddingPx = 28;`
    - `public List<StickerDesign> designs = new List<StickerDesign>();`
    - `public Texture2DArray atlas;`（Task 7 の詰める係が作る。このアセットの子アセット）
    - `public Material material;`（シール用の共通マテリアル）
    - `public StickerDesign Find(string id)` — 無ければ null。
  - `[System.Serializable] public sealed class StickerDesign { public string id; public string displayName; public string sourceGuid; public StickerStyle style; [Min(0.05f)] public float sizeMeters = 0.35f; public bool border = true; public int slice = -1; }`
    - `sourceGuid` は PNG のアセット GUID の文字列（Texture2D を直接参照しないのは、ビルドに PNG を入れないため）。`slice` は `atlas` の何枚目か（詰める係が書く。-1＝まだ／失敗）。
  - `[System.Serializable] public sealed class StickerPlacement`
    - `public const int GridSide = 5;`（1枚＝5×5頂点）
    - `public string designId; public Vector3 center; public Vector3 normal; public float rotationDegrees; public float size; public float seed; public bool handEdited; public Vector3[] gridPositions; public Vector3[] gridNormals;`
    - 座標はすべて `StageStickerLayer`（子「Stickers」）のローカル。`gridPositions[j*GridSide + i]` が列 i・行 j（u = i/4, v = j/4）。
  - `public static class StickerMeshBuilder`
    - `public const int VerticesPerSticker = 25; public const int TrianglesPerSticker = 32;`
    - `public static void Build(Mesh mesh, IReadOnlyList<StickerPlacement> placements, StickerLibrary library)` — mesh を Clear して作り直す。頂点の並びは placement の順。UV0 = (u, v, slice, (float)style)、UV1 = (0, 0, seed, border ? 1 : 0)、法線 = gridNormals。三角形の向きは gridNormals の側を表にする。**デザインが見つからない・slice < 0 の placement は、25頂点すべてを center に置き（面積0で見えない）、番号はずらさない**。`mesh.indexFormat` は頂点が 65535 を超えたら UInt32。最後に `RecalculateBounds()`。
    - `public static void WriteHeat(List<Vector4> uv1, IReadOnlyList<StickerPlacement> placements, StickerLibrary library, StickerHeatState heat)` — uv1 を placement 順に作り直す（x=glow, y=burn, z=seed, w=border）。`mesh.SetUVs(1, uv1)` は呼び出し側が行う。

- [ ] **Step 1: 失敗するテストを書く**（テスト用に 1×1 の平らな格子を作る関数 `FlatPlacement(string id, Vector3 center)` をテスト内に置く。z = 0 の平面、法線 (0,0,-1)）

```csharp
[Test] public void Build_TwoStickers_Makes50VerticesAnd64Triangles()
{   var lib = Library(("a", StickerStyle.Holo, 2)); var mesh = new Mesh();
    StickerMeshBuilder.Build(mesh, new[] { FlatPlacement("a", Vector3.zero), FlatPlacement("a", Vector3.right * 3) }, lib);
    Assert.AreEqual(50, mesh.vertexCount); Assert.AreEqual(64 * 3, mesh.triangles.Length);
    var uv0 = new List<Vector4>(); mesh.GetUVs(0, uv0);
    Assert.AreEqual(2f, uv0[0].z); Assert.AreEqual((float)StickerStyle.Holo, uv0[0].w);
    Assert.AreEqual(new Vector4(1f, 1f, 2f, 1f), uv0[24]); }
[Test] public void Build_TrianglesFaceTheStoredNormal()
{   var mesh = new Mesh(); StickerMeshBuilder.Build(mesh, new[] { FlatPlacement("a", Vector3.zero) }, Library(("a", StickerStyle.Paper, 0)));
    var v = mesh.vertices; var t = mesh.triangles;
    for (var i = 0; i < t.Length; i += 3)
    {   var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
        Assert.Greater(Vector3.Dot(n, new Vector3(0, 0, -1)), 0f); } }
[Test] public void Build_MissingDesign_CollapsesToCenterAndKeepsIndices()
{   var mesh = new Mesh(); var c = new Vector3(5, 0, 0);
    StickerMeshBuilder.Build(mesh, new[] { FlatPlacement("gone", c), FlatPlacement("a", Vector3.zero) }, Library(("a", StickerStyle.Paper, 0)));
    var v = mesh.vertices; Assert.AreEqual(50, v.Length);
    for (var i = 0; i < 25; i++) Assert.AreEqual(c, v[i]);
    Assert.AreNotEqual(v[25], v[49]); }
[Test] public void WriteHeat_PutsGlowBurnSeedBorder()
{   var p = FlatPlacement("a", Vector3.zero); p.seed = 0.7f; var lib = Library(("a", StickerStyle.Puffy, 0)); lib.designs[0].border = false;
    var heat = new StickerHeatState(1); heat.Pulse(0, 1f, new StickerHeatRates(1, 1, 0.5f, 1, 0.8f, 0.2f));
    var uv1 = new List<Vector4>(); StickerMeshBuilder.WriteHeat(uv1, new[] { p }, lib, heat);
    Assert.AreEqual(25, uv1.Count); Assert.AreEqual(new Vector4(0.8f, 0.2f, 0.7f, 0f), uv1[0]); }
```
（`Library(params (string id, StickerStyle style, int slice)[])` は `ScriptableObject.CreateInstance<StickerLibrary>()` に designs を足すテスト内の関数）

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: 上の Interfaces のファイルを書く**
- [ ] **Step 4: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 5: コミット** — `git commit -m "シール: 絵柄の一覧・配置データ・メッシュ作りを足す"`

---

### Task 5: シールのシェーダー `MS2026/Stage/Sticker`

**Files:**
- Create: `Assets/_Game/Stage/Runtime/Shaders/StageSticker.shader`

**Interfaces:**
- Consumes: Task 3 の全体の値（`_Sticker*`）、Task 4 の頂点の並び（UV0 / UV1）、既存の `Assets/_Game/Stage/Runtime/Shaders/StagePropEffects.hlsl`（`StageClip`・`_StageDarken`・`_StageFlash`・`_StageFlashColor`・`StageNoise`）
- Produces: プロパティ `_Atlas`（`2DArray`）だけを持つマテリアル用シェーダー。名前は `"MS2026/Stage/Sticker"`（Task 7 がこの名前で `Shader.Find` する）。`_Smoothness` プロパティは**持たない**（`MaterialGlossPanel` の対象にしないため）。

テクスチャの中身（Task 7 が作る）: RGB＝絵の色（形の外も近い色で埋めてある）、**A＝形からの符号付き距離**（`0.5 + 距離px / (2*24)`、0.5 が形のふち、1 が内側 24px 以上、0 が外側 24px 以上）。

- [ ] **Step 1: パスの形を決めて書く**
  - `Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }`、`Cull Back`、`ZWrite On`、`Offset -1, -1`。
  - パス1 `UniversalForward`（`LightMode`=`UniversalForward`）、パス2 `DepthOnly`、パス3 `DepthNormals`。影を落とすパスは作らない（Renderer 側でも影を切る）。どのパスも同じ切り抜き（下の「形」と `StageClip`）をする。
  - `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` と `_SHADOWS_SOFT` 系だけ入れる（受ける影はほしい。追加ライト・ライトマップは不要）。
  - include は `Core.hlsl`、`Lighting.hlsl`、`"Assets/_Game/Stage/Runtime/Shaders/StagePropEffects.hlsl"`。
- [ ] **Step 2: 共通の計算を書く**（見本ページ `2026-11-26-obstacle-stickers-look-demo.html` の `sticker()` 関数が同じ見え方の手本）
  - 距離: `d = (tex.a - 0.5) * 2 * 24`（px、+が内側）。`borderPx = _StickerBorder`、`bevelPx = _StickerPuffy.x`（どちらも px のまま届く）。白いふちあり（uv1.w=1）なら、見える範囲は `d > -borderPx`、ふちの部分（`-borderPx < d < 0`）は `_StickerBorderColor`。ふちなしなら `d > 0`。境目は `fwidth` で 1px なめらかにし、`clip(alpha - 0.5)`。
  - 光: `GetMainLight(shadowCoord)` で「メインの光 × 2段のトゥーン（`smoothstep(0.0, 0.08, NdotL)` 程度）× 影」＋ `SampleSH(normal)`。最後に `albedo *= 1 - _StageDarken`、`emission += _StageFlashColor.rgb * _StageFlash`（壊せる壁が被弾したときに壁と一緒に光る）。
  - `StageClip(positionWS, positionCS)` を必ず通す（透け穴・全体の薄さ・溶け）。**`StageModifySurface` は呼ばない**（障害物の熱を使わないため）。
  - glow = uv1.x、burn = uv1.y、seed = uv1.z、style = round(uv0.w)。キラキラは `* (1 - burn)` で焦げると止める。
- [ ] **Step 3: スタイルごとの見え方を書く**（同じシェーダーの中で style で分ける。キーワードは増やさない）
  - **紙 (0)**: 絵の色そのまま。光の帯: `t = frac((_Time.y + seed*97) / 間隔)`、`t*間隔 < 長さ` の間だけ、uv の斜め方向に動く白い帯を足す（同時に光るのは全体の一部になる）。当たる: ふちに近いほど強く `_StickerPaperGlow * glow` を発光に足す。焦げる: 色を `_StickerScorch` へ `burn*0.85` 混ぜる、白いふちは burn 0→0.5 で `_StickerPaperTan`、0.5→1 で `_StickerScorch` 寄りの暗い茶色へ。burn > 0.5 では `StageNoise(uv*8 + seed)` で形のふちを少し削ってぎざぎざにする。
  - **ホログラム (1)**: 色相を `(_Time.y*虹の速さ + dot(positionWS.xy, (0.7,0.4)) + seed)` で回した虹色を絵の色に 55% 重ねる。星: シールの中の決まった位置（seed から決める）に、`pow(max(0, sin(_Time.y*2π*星の速さ + seed*20)), 16)` の明るさで 4 本の光の筋を描く。当たる: 全体に `_StickerHoloGlow * glow`。焦げる: 彩度を `1-burn` に落として暗くし、burn > 0.35 で seed から決めた 3 か所に黒い丸（ぷつぷつ）。
  - **ぷっくり (2)**: 内側の距離 `d` から高さ `h = smoothstep(0, puffyBevelPx, d)` を作り、`ddx/ddy` で傾きを出して法線を曲げる（ふちが丸く盛り上がる）。左上の決まった所につやの楕円と、`puffyGlintEverySeconds` ごとにきらっとする点。影: uv を `puffyShadowOffset` だけずらした距離で「形の外の影」を少し暗く描く（形の外でも影の所は clip しない）。当たる: 中心ほど強い `_StickerPuffyGlow * glow`。焦げる: `burn` で色を `_StickerScorch` へ、形を内側へ最大 3px 縮める（d から引く）、`glow > 0.1 && burn > 0.2` の間は `_StickerEmber` の細いひび（`StageNoise` のしきい値で線を作る）。
- [ ] **Step 4: 確かめる** — Unity が使えるなら、空のシーンに板を置き、Task 7 の前なので仮に色だけのテクスチャ配列で 3 スタイル・glow/burn を変えて見る（スクリーンショットを撮って報告）。使えなければ「シェーダー未確認」と記録して次へ進み、Task 11 でまとめて確かめる。
- [ ] **Step 5: コミット** — `git commit -m "シール: シールのシェーダー（紙・ホログラム・ぷっくり、光る・焦げる）"`

---

### Task 6: 障害物にシールを持たせる `StageStickerLayer`

**Files:**
- Create: `Assets/_Game/Stage/Runtime/Stickers/StageStickerLayer.cs`
- Create: `Assets/_Game/Stage/Runtime/Stickers/StickerHitFilter.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerMeshAssets.cs`
- Modify: `Assets/_Game/Stage/Editor/Core/StageMaterialConverter.cs`（`Renderers(StageProp)` でシールの Renderer を飛ばす）
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerHitFilterTests.cs`

**Interfaces:**
- Consumes: Task 1 `SkillSurfaceHits`、Task 3 `StickerHeatState` / `StickerHeatRates` / `StickerHitMath` / `StageLookProfile.Current.stickers`、Task 4 `StickerLibrary` / `StickerPlacement` / `StickerMeshBuilder`
- Produces:
  - `public static class StickerHitFilter { public static bool ShouldReceive(bool isDestroyed, Rect worldBoundsXY, Vector2 point, float radius) }` — 壊れていたら false。`worldBoundsXY` を radius だけ広げた中に point が入っていれば true。
  - `[ExecuteAlways] [DisallowMultipleComponent] [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))] public sealed class StageStickerLayer : MonoBehaviour`
    - `public const string ChildName = "Stickers"; public const int MaxStickers = 120;`
    - 保存する設定（全部 日本語 Tooltip）: `public StickerLibrary library;` `public List<string> designIds = new List<string>();`（自動でばらまくときに使う絵柄）`[Range(0.2f, 2f)] public float density = 1f;` `[Range(0.5f, 2f)] public float sizeScale = 1f;` `[Range(0f, 180f)] public float rotationJitterDegrees = 35f;` `public int seed = 1;` `public List<StickerPlacement> placements = new List<StickerPlacement>();` `[HideInInspector] public int surfaceSignature;`
    - `public static IReadOnlyList<StageStickerLayer> All { get; }`（OnEnable/OnDisable で出し入れ、SubsystemRegistration で空にする。`StageProp` と同じやり方）
    - `public StickerHeatState Heat { get; }`
    - **メッシュの持ち方（Prefab に保存できるように）**: 編集中は、MeshFilter が**プロジェクトに保存したメッシュのアセット**（Task 6 の `StickerMeshAssets` が作る）を指す。保存しない（`HideFlags.DontSave`）メッシュを MeshFilter に入れてはいけない（ステージの Prefab への書き戻し `StageSceneService.SaveToPrefab` で壊れた参照になるため）。Play 中だけ、`OnEnable` でそのアセットを `Instantiate` した複製を `MeshFilter.sharedMesh` に入れ、光り・焦げはその複製の UV1 に書く（アセットそのものは書き換えない）。複製は `OnDestroy` で `Destroy`。
    - `public void ReceiveHit(Vector2 worldPoint, SkillSurfaceHitKind kind)` — 半径は kind で `sustainedRadius` / `pulseRadius`。`StickerHitFilter.ShouldReceive(壊れているか, MeshRenderer.bounds の XY, point, radius)` が false なら何もしない。各シールについて、**シールの格子の 25 点（`transform.TransformPoint(gridPositions[k])` の XY）のうち point に一番近い点までの距離**で `Falloff` を出す（中心で測ると、壁のふちに当たった攻撃が内側のシールに届きにくいため）。Sustained は `Heat.Sustain(i, f)`、Pulse は `Heat.Pulse(i, f, rates)`（f>0 のときだけ）。格子のワールド XY は Play 開始時に 1 度だけ計算して持っておき、揺れ（`StagePropWobble`）のずれは無視してよい。
    - `public void ClearMarks()` — `Heat.Clear()` して UV1 を書き直す。
    - `Heat` は placements の数で作る（`OnEnable`）。`MeshRenderer.shadowCastingMode = Off`、`receiveShadows = true` は作るとき（Task 9 の `Create`）に設定して保存する。
  - ふるまい:
    - `OnEnable`: 登録（編集中も。スタジオがシーンのシールを数えるため）。**Play 中だけ**: メッシュの複製、`SkillSurfaceHits.Hit += OnHit`、親の `DestructibleObstacle`（`GetComponentInParent`）があれば `Regenerated` に `ClearMarks` を呼ぶ関数を付ける（外せるようにフィールドに持つ）。`OnDisable` で全部外す。
    - `Update`（Play 中だけ）: `Heat.IsHot` でも UV1 の書き直し待ちでもなければ何もしない。`Heat.Tick(Time.deltaTime, rates)` が true なら `StickerMeshBuilder.WriteHeat` → `mesh.SetUVs(1, ...)`。`Pulse` を受けたフレームも書き直す。
    - 透け・溶け・暗さ・被弾の光り・壊れて消える（`SetVisible(false)`）は、Renderer が `Visual` の下にあるので `StagePropRenderer` が自動で面倒を見る。シール側で同じことをしない。
  - `StageMaterialConverter.Renderers` は `renderer.GetComponent<StageStickerLayer>() != null` の Renderer を返さない（「専用シェーダーに切り替え」でシールのマテリアルが障害物用に変わらないように）。
  - `public static class StickerMeshAssets`（エディタ）
    - `public const string MeshFolder = "Assets/_Game/Stage/Stickers/Meshes";`
    - `public static void Rebuild(StageStickerLayer layer)` — **配置を変えたら必ずこれを呼ぶ**（Task 7・9・10）。MeshFilter の `sharedMesh` が `MeshFolder` のアセットで、**シーンのほかの `StageStickerLayer` と共有していなければ**、それを `StickerMeshBuilder.Build` で作り直して `EditorUtility.SetDirty`。無い・共有している（障害物を複製したとき）なら、`MeshFolder` に新しいアセット（名前 `Stickers_<GUID の短い文字>.asset`、`AssetDatabase.CreateAsset`）を作って `Undo.RecordObject(meshFilter)` してから入れる。メッシュの Read/Write は切らない（`UploadMeshData(true)` を呼ばない。Play 中に UV1 を書くため）。最後に `PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter)` と `AssetDatabase.SaveAssets()`。
    - ステージの Prefab に書き戻すと、MeshFilter はこのアセットを指したまま保存される（壊れた参照にならない）。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test] public void ShouldReceive_FalseWhileDestroyed()
{   Assert.IsFalse(StickerHitFilter.ShouldReceive(true, new Rect(0, 0, 2, 2), new Vector2(1, 1), 0.5f)); }
[Test] public void ShouldReceive_UsesBoundsGrownByRadius()
{   var b = new Rect(0, 0, 2, 2);
    Assert.IsTrue(StickerHitFilter.ShouldReceive(false, b, new Vector2(2.4f, 1f), 0.5f));
    Assert.IsFalse(StickerHitFilter.ShouldReceive(false, b, new Vector2(2.6f, 1f), 0.5f)); }
```

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: `StickerHitFilter`・`StageStickerLayer`・`StickerMeshAssets` を書き、`StageMaterialConverter.Renderers` に除外を足す**
- [ ] **Step 4: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 5: コミット** — `git commit -m "シール: 障害物がシールを持ち、スキルで光る・焦げるようにする"`

---

### Task 7: 絵柄の一覧の画面と、PNG を詰める係

**Files:**
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerPackMath.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerSdf.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerLibraryPacker.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerLibraryEditor.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerAssets.cs`
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerPackTests.cs`

**Interfaces:**
- Consumes: Task 4 `StickerLibrary` / `StickerDesign`、Task 6 `StageStickerLayer.All` / `StickerMeshAssets.Rebuild(layer)`
- Produces:
  - `public static class StickerPackMath { public static RectInt FitRect(int sourceWidth, int sourceHeight, int tileSize, int padding) }` — 縦横比を保ち、`tileSize - 2*padding` の正方形に収めて真ん中に置く。幅か高さが 0 以下なら `new RectInt(0,0,0,0)`。
  - `public static class StickerSdf { public static float[] Compute(bool[] inside, int width, int height, float maxDistance) }` — 画素ごとの符号付き距離（px）。内側の画素は「一番近い外側の画素の中心までの距離 − 0.5」、外側の画素は「−(一番近い内側の画素の中心までの距離 − 0.5)」。±maxDistance で止める。距離は 2 パスのチャンファー（縦横 1、斜め √2）で求める。内側が無いときは全部 −maxDistance、外側が無いときは全部 +maxDistance。
  - `public static class StickerLibraryPacker { public static bool Pack(StickerLibrary library, out string message) }`
    - 各デザイン: `AssetDatabase.GUIDToAssetPath(sourceGuid)` の PNG を `File.ReadAllBytes` → `Texture2D.LoadImage`（インポート設定に頼らない）→ `FitRect(w, h, 256, 28)` の範囲へ `GetPixelBilinear` で写す → `alpha >= 0.5` を内側として `StickerSdf.Compute(..., 24)` → 外側の色は内側の色で埋める（隣の色の平均で 8 回広げればよい）→ A に `clamp01(0.5 + d/48)`。
    - `Texture2DArray(256, 256, 数, TextureFormat.RGBA32, mipChain: true, linear: false)`、`wrapMode = Clamp`、`filterMode = Bilinear`、`anisoLevel = 4`、`Apply(true)`。ライブラリのアセットの子アセットとして保存（`AssetDatabase.AddObjectToAsset`。前の配列があれば取り除いてから）。`design.slice` を書く。PNG が見つからないデザインは `slice = -1` にして `message` に名前を並べ、ほかは続ける。デザインが 0 個なら配列を消して `atlas = null`。
    - 最後に `library.material.SetTexture("_Atlas", atlas)`、`EditorUtility.SetDirty`、`AssetDatabase.SaveAssets()`、シーンの `StageStickerLayer.All` をすべて `StickerMeshAssets.Rebuild(layer)`（slice が変わるため）。
  - `public static class StickerAssets`
    - `public const string Folder = "Assets/_Game/Stage/Stickers"; public const string LibraryPath = Folder + "/StickerLibrary.asset"; public const string MaterialPath = Folder + "/StageSticker.mat";`
    - `public static StickerLibrary EnsureDefaultLibrary()` — 無ければフォルダ（`StudioAssets.EnsureFolder`）・マテリアル（`Shader.Find("MS2026/Stage/Sticker")`）・ライブラリを作り、`library.material` を入れて返す。あれば読み込んで返す。
  - `[CustomEditor(typeof(StickerLibrary))] public sealed class StickerLibraryEditor : Editor`（UI Toolkit、`CreateInspectorGUI`、StudioKit の `StudioUi` を使う）
    - 上: 説明 1 行「PNG をここに落とすと絵柄になります。見え方は絵柄ごとに選べます。」と、PNG を落とせる枠（`DragAndDrop` で `Texture2D` のアセットを受け取る。同じ GUID は足さない）。足すときは `id = System.Guid.NewGuid().ToString("N")`、`displayName` = ファイル名、`sourceGuid` = その GUID。
    - 行（1 絵柄 1 行）: 小さな絵（`AssetPreview` か PNG そのもの）・名前・見え方（紙／ホログラム／ぷっくり の選択）・大きさ（m）・白いふち（オン／オフ）・消すボタン。消すときに使っているシールがあれば `EditorUtility.DisplayDialog` で「使っている N 枚が見えなくなります」と聞く。
    - 変更のたびに `Undo.RecordObject` → 0.3 秒まとめてから `StickerLibraryPacker.Pack` を呼ぶ（`schedule.Execute(...).StartingIn(300)` を置き直す）。`message` があれば注意の帯で出す。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test] public void FitRect_WideImage()
{   Assert.AreEqual(new RectInt(28, 78, 200, 100), StickerPackMath.FitRect(512, 256, 256, 28)); }
[Test] public void FitRect_TallImage()
{   Assert.AreEqual(new RectInt(103, 28, 50, 200), StickerPackMath.FitRect(100, 400, 256, 28)); }
[Test] public void FitRect_EmptyImage()
{   Assert.AreEqual(new RectInt(0, 0, 0, 0), StickerPackMath.FitRect(0, 10, 256, 28)); }
[Test] public void Sdf_SinglePixel()
{   var inside = new bool[81]; inside[4 * 9 + 4] = true;
    var d = StickerSdf.Compute(inside, 9, 9, 10f);
    Assert.AreEqual(0.5f, d[4 * 9 + 4], 1e-4f);
    Assert.AreEqual(-0.5f, d[4 * 9 + 5], 1e-4f);
    Assert.AreEqual(-(Mathf.Sqrt(2f) - 0.5f), d[5 * 9 + 5], 1e-3f); }
[Test] public void Sdf_HalfPlaneAndClamp()
{   var inside = new bool[10]; for (var x = 0; x < 5; x++) inside[x] = true;   // 10×1、左半分が内側
    var d = StickerSdf.Compute(inside, 10, 1, 2f);
    Assert.AreEqual(0.5f, d[4], 1e-4f); Assert.AreEqual(-0.5f, d[5], 1e-4f);
    Assert.AreEqual(2f, d[0], 1e-4f); Assert.AreEqual(-2f, d[9], 1e-4f); }
[Test] public void Sdf_NoInside_AllNegativeMax()
{   var d = StickerSdf.Compute(new bool[4], 2, 2, 3f); foreach (var v in d) Assert.AreEqual(-3f, v, 1e-6f); }
```

- [ ] **Step 2: コンパイルして失敗を確かめる**（`MS2026.Stage.Tests.EditMode.csproj`）
- [ ] **Step 3: 上のファイルを書く**
- [ ] **Step 4: コンパイル（＋あれば Test Runner）で通るのを確かめる**。Unity が使えるなら、仮の PNG（自分で作った単純な星形。他人の絵は使わない）を 2 枚落とし、Inspector で `atlas` ができて `slice` が 0,1 になるのを確かめる。
- [ ] **Step 5: コミット** — `git commit -m "シール: 絵柄の一覧の画面と、PNG をまとめる係"`

---

### Task 8: 表面を調べる・自動でばらまく・表面に沿わせる

**Files:**
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerSurface.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerConform.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerScatter.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerSurfaceSignature.cs`
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerScatterTests.cs`

**Interfaces:**
- Consumes: Task 4 `StickerPlacement` / `StickerDesign`、既存 `FootprintMeshSampler.Collect(Transform modelRoot, Transform reference, List<Vector3> vertices, List<int> triangles)`
- Produces:
  - `public sealed class StickerSurface`
    - `public StickerSurface(List<Vector3> vertices, List<int> triangles, Vector3 projectDirection)` — 座標は Stickers のローカル。`projectDirection` は「上から下へ」の向き（正規化して持つ）。
    - `public Vector3 ProjectDirection { get; }`
    - `public static StickerSurface FromProp(StageProp prop, Transform layer)` — `FootprintMeshSampler.Collect(prop.modelRoot, layer, ...)`、向きは `layer.InverseTransformDirection(Vector3.forward)`。**`prop.modelRoot == null` なら null を返す**（visualRoot に戻すと子の「Stickers」自身のメッシュまで拾ってしまうため。Unity のオブジェクトに `??` は使わない）。
    - `public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 point, out Vector3 normal)` — 一番近い三角形（Möller–Trumbore）。`dot(三角形の法線, -direction) > 0` の面だけ（裏面は無視）。法線は三角形の面の法線。
    - `public bool ProjectDown(Vector2 planePoint, out Vector3 point, out Vector3 normal)` — `ProjectDirection` に垂直な平面の座標 (s,t) から、表面の一番上に落とす。平面の軸は `PlaneAxes` で決める。
    - `public void PlaneAxes(out Vector3 axisS, out Vector3 axisT)` — `ProjectDirection` に垂直な 2 軸（`Vector3.up` を基準に作る。`ProjectDirection` が up と平行なら `Vector3.right`）。
    - `public Rect PlaneBounds { get; }` — 上を向いた三角形（`dot(n, -dir) >= 0.5`）の頂点を (s,t) にした範囲。
    - `public float FacingArea { get; }` — 同じ三角形を平面へ投影した面積の合計。
    - 三角形が多いときのために (s,t) の格子でバケツ分けしてよい（任意）。
  - `public static class StickerConform { public static void BuildGrid(StickerSurface surface, Vector3 center, Vector3 normal, float rotationDegrees, float size, float surfaceOffset, Vector3[] positions, Vector3[] normals) }`
    - `normal` に垂直な面に、`PlaneAxes` の s 軸を `normal` 回りに `rotationDegrees` 回した u 軸と、v 軸 = `cross(normal, u)` を作る。格子点 `p = center + u*(i/4-0.5)*size + v*(j/4-0.5)*size` を、`p + normal*size*0.5` から `-normal` 向きに `size` の長さで `Raycast`。当たれば その点 + 法線×surfaceOffset、法線はその面の法線。外れたら `p + normal*surfaceOffset`、法線は `normal`。
    - `public static bool CornersOnSurface(StickerSurface surface, Vector3 center, Vector3 normal, float rotationDegrees, float size)` — 4 隅（i,j ∈ {0,4}）が全部当たるか。
  - `public struct StickerScatterSettings { public float Density; public float SizeScale; public float RotationJitterDegrees; public int Seed; public float Spacing; public float MaxSlopeDegrees; public float SurfaceOffset; public float LayerStep; public static StickerScatterSettings From(StageStickerLayer layer) }` — `From` は Spacing=0.62、MaxSlopeDegrees=60、SurfaceOffset=0.004、LayerStep=0.0003 を入れる。
  - **重ね順の決まり**: シールは少し重なってよい（にぎやかに見せるため。見本ページと同じ）。重なった所がちらつかないよう、`placements` の **list の順番がそのまま重ね順**（後ろほど上）で、i 番目のシールは面から `SurfaceOffset + LayerStep * i` だけ浮かせて格子を作る（最大 120 枚で 0.04m 弱）。手で直したシールは list の最後へ移して作り直す（Task 10）。
  - `public static class StickerScatter`
    - `public static List<StickerPlacement> Scatter(StickerSurface surface, in StickerScatterSettings settings, IReadOnlyList<StickerDesign> designs, IReadOnlyList<StickerPlacement> keep)`
      - 結果は「keep をそのまま先頭に」＋新しいシール。designs が空なら keep だけ。
      - 目標の枚数 `n = min(StageStickerLayer.MaxStickers, round(Density * FacingArea / (平均の大きさ)^2))`、平均の大きさ = designs の sizeMeters の平均 × SizeScale。新しく作るのは `n - keep.Count` 枚（0 以下なら作らない）。
      - `System.Random(Seed)`。候補 = `PlaneBounds` の中の一様な点 → `ProjectDown`。捨てる条件: 当たらない／`dot(normal, -dir) < cos(MaxSlopeDegrees)`／`CornersOnSurface` が false／すでにあるシール（keep 含む）との中心の距離 < `(sizeA + sizeB) * 0.5 * Spacing`。試すのは最大 `n*40` 回。
      - 1 枚の値: デザインは designs から一様に、大きさ = `sizeMeters * SizeScale * lerp(0.85, 1.15, r)`、回転 = `(r*2-1) * RotationJitterDegrees`、seed = r（0..1）、`handEdited = false`。格子は `Place(..., surfaceOffset: SurfaceOffset + LayerStep * 結果の list での番号)` で作る。keep のシールの格子は作り直さない。
    - `public static StickerPlacement Place(StickerSurface surface, Vector3 point, Vector3 normal, StickerDesign design, float size, float rotationDegrees, float seed, float surfaceOffset)` — `gridPositions/gridNormals` を 25 個で作り `StickerConform.BuildGrid` で埋める。`center = point + normal*surfaceOffset`。
  - `public static class StickerSurfaceSignature { public static int Compute(Transform modelRoot, Transform layer) }` — `layer.worldToLocalMatrix * modelRoot.localToWorldMatrix` の 16 個の値を 1e-3 で丸めたものと、子の `MeshFilter.sharedMesh` の `GetInstanceID()` と `vertexCount` を混ぜたハッシュ。

- [ ] **Step 1: 失敗するテストを書く**（テスト内に「中心が原点・1 辺 1 の立方体」の頂点と三角形を作る関数 `Cube()` を置く。上の面は z = −0.5、外向きの法線。向き `(0,0,1)`）

```csharp
[Test] public void Raycast_HitsCubeTopFromAbove()
{   var s = new StickerSurface(CubeVerts(), CubeTris(), Vector3.forward);
    Assert.IsTrue(s.Raycast(new Vector3(0.1f, 0.2f, -3f), Vector3.forward, 10f, out var p, out var n));
    Assert.AreEqual(-0.5f, p.z, 1e-4f); Assert.AreEqual(-1f, n.z, 1e-4f); }
[Test] public void BuildGrid_OnFlatTop_AllPointsOnSurfacePlusOffset()
{   var s = Surface(); var pos = new Vector3[25]; var nrm = new Vector3[25];
    StickerConform.BuildGrid(s, new Vector3(0, 0, -0.5f), new Vector3(0, 0, -1), 30f, 0.3f, 0.004f, pos, nrm);
    foreach (var p in pos) Assert.AreEqual(-0.504f, p.z, 1e-4f);
    foreach (var n in nrm) Assert.AreEqual(-1f, n.z, 1e-4f); }
[Test] public void Scatter_CubeTop_CornersStayOnTopFace()
{   var designs = new List<StickerDesign> { new StickerDesign { id = "a", sizeMeters = 0.2f } };
    var set = new StickerScatterSettings { Density = 1f, SizeScale = 1f, RotationJitterDegrees = 35f, Seed = 3, Spacing = 0.62f, MaxSlopeDegrees = 60f, SurfaceOffset = 0.004f, LayerStep = 0.0003f };
    var list = StickerScatter.Scatter(Surface(), set, designs, new List<StickerPlacement>());
    Assert.Greater(list.Count, 0); Assert.LessOrEqual(list.Count, 25);
    for (var k = 0; k < list.Count; k++)
    {   var p = list[k];
        Assert.AreEqual(-1f, p.normal.z, 1e-3f);
        Assert.AreEqual(-0.5f - (0.004f + 0.0003f * k), p.center.z, 1e-4f);   // 後ろほど上（重ね順）
        foreach (var i in new[] { 0, 4, 20, 24 })
        {   Assert.LessOrEqual(Mathf.Abs(p.gridPositions[i].x), 0.5001f); Assert.LessOrEqual(Mathf.Abs(p.gridPositions[i].y), 0.5001f); } } }
[Test] public void Scatter_SameSeed_SameResult()
{   /* 同じ設定で2回 → 枚数・各 center・designId・rotationDegrees が等しい */ }
[Test] public void Scatter_KeepsHandEditedFirstAndUnchanged()
{   /* keep に handEdited=true の1枚 → 結果[0] が同じ参照、その周りに spacing 内のシールが無い */ }
[Test] public void Scatter_NoDesigns_ReturnsOnlyKeep()
{   /* designs 空 → 結果 == keep の中身 */ }
[Test] public void Signature_ChangesWhenModelMoves()
{   var root = new GameObject("layer"); var model = new GameObject("model"); model.transform.SetParent(root.transform);
    try { var a = StickerSurfaceSignature.Compute(model.transform, root.transform);
          model.transform.localPosition = new Vector3(0.1f, 0, 0);
          Assert.AreNotEqual(a, StickerSurfaceSignature.Compute(model.transform, root.transform)); }
    finally { Object.DestroyImmediate(root); } }
```
（コメントの 3 つは、そのとおりのアサートを書く）

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: 上のファイルを書く**
- [ ] **Step 4: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 5: コミット** — `git commit -m "シール: 表面に沿わせて自動でばらまく計算"`

---

### Task 9: ステージ背景スタジオの「シール」欄

**Files:**
- Create: `Assets/_Game/Stage/Editor/Panels/PropStickerSection.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerLayerOps.cs`
- Modify: `Assets/_Game/Stage/Editor/Panels/PropDetailPanel.cs`（`_sections` の `new PropLookSection(),` の次に `new PropStickerSection(),`）

**Interfaces:**
- Consumes: Task 6 `StageStickerLayer`、Task 7 `StickerAssets.EnsureDefaultLibrary()`、Task 8 `StickerSurface.FromProp` / `StickerScatter` / `StickerScatterSettings.From` / `StickerSurfaceSignature.Compute`、既存 `PropSection`（`Build(StageProp)` / `Refresh()` / `Field(...)`）と `StudioUi`
- Produces:
  - `public static class StickerLayerOps`
    - `public static StageStickerLayer Find(StageProp prop)` — `prop.visualRoot` の子 `"Stickers"` の `StageStickerLayer`。無ければ null。
    - `public static StageStickerLayer Create(StageProp prop)` — `Undo.RegisterCreatedObjectUndo` で子「Stickers」を `visualRoot` の下（ローカル位置0・回転0・倍率1）に作り、`StageStickerLayer` を付け、`library = EnsureDefaultLibrary()`、`designIds` = ライブラリの全絵柄、`seed = Random.Range(1, 100000)`。`MeshRenderer.sharedMaterial = library.material`、`shadowCastingMode = Off`、`receiveShadows = true`。最後に親の `StagePropRenderer.CollectRenderers()`。メッシュのアセットは続く `Rescatter` の `StickerMeshAssets.Rebuild` が作る。
    - `public static void Rescatter(StageProp prop, StageStickerLayer layer, bool keepHandEdited)` — `Undo.RecordObject(layer)` → `StickerSurface.FromProp` → `Scatter`（designs = `designIds` のうちライブラリに今ある絵柄だけ。keep = handEdited のものだけ、または空）→ `placements` に入れる → `surfaceSignature = StickerSurfaceSignature.Compute(prop.modelRoot, layer.transform)` → `StickerMeshAssets.Rebuild(layer)` → `EditorUtility.SetDirty` と `PrefabUtility.RecordPrefabInstancePropertyModifications(layer)`。
    - `public static void Remove(StageStickerLayer layer)` — `Undo.DestroyObjectImmediate(layer.gameObject)` と親の `CollectRenderers()`。
    - `public static float HoloShare()` — シーンの `StageStickerLayer.All` の全シールのうち、デザインの style が Holo の割合（0 枚なら 0）。
  - `public sealed class PropStickerSection : PropSection` — 見出し「シール」、説明「壁に貼るシール。スキルが当たると光って焦げます。」
    - 役割が Wall／DestructibleWall でなければ注意の帯「シールは壁・壊せる壁にだけ貼れます。」だけ。`prop.modelRoot == null` なら「モデルが入っていない物には貼れません。」だけ。
    - シールが無ければ: 主ボタン「シールを貼る」（`Create` → `Rescatter(keepHandEdited:false)` → 欄を作り直す）だけ。ライブラリに絵柄が無ければ、その上に「絵柄がまだありません。『絵柄の一覧を開く』から PNG を入れてください。」と「絵柄の一覧を開く」ボタン（`Selection.activeObject = library`）。
    - シールがあれば、**開いた瞬間に見えるのは次だけ**:
      1. 「量」スライダー（`density`。離したら `Rescatter(keepHandEdited:true)`）
      2. 「使う絵柄」: ライブラリの絵柄をチップで並べ、押すとオン／オフ（`designIds`。変えたら `Rescatter(true)`）。チップの文字は「名前（紙）」のように見え方も出す。
      3. 「ばらまき直す」ボタン（`seed` を変えて `Rescatter(true)`。Tooltip「手で直したシールは残します」）
      4. 枚数「シール 23 枚（手で直した 3 枚）」
      - 必要なときだけ注意の帯: 署名が変わっていたら「モデルが変わりました。『ばらまき直す』を押してください。」／`HoloShare() > holoWarnShare` なら「ホログラムが全体の 42% です。多いと敵が見づらくなります（目安 30% まで）。」
    - 閉じた Foldout「細かい設定」: 大きさの倍率・回転のばらつき・「手で直した分も消してばらまく」・「絵柄の一覧を開く」・「シールを外す」（確認ダイアログ）。
    - 閉じた Foldout「1枚ずつ直す」: トグル「シーンで直す」（Task 10 の `StickerSceneTool.Begin(layer)` / `End()`）、操作の説明（下の Task 10 の操作表を短く）、「足す絵柄」の選択。
    - Play 中だけ押せるボタン: 「スキルを当ててみる」（1.5 秒間、毎フレーム `layer.ReceiveHit(壁の手前のまん中, Sustained)`。`EditorApplication.update` で回して止める）／「焦げを消す」（`ClearMarks`）。`Refresh()` で Play 中かどうかに合わせて押せる／押せないを切り替え、「今: 光っている N 枚 / 焦げた N 枚」を出す。

- [ ] **Step 1: `StickerLayerOps` を書く**
- [ ] **Step 2: `PropStickerSection` を書き、`PropDetailPanel` に足す**
- [ ] **Step 3: コンパイルを確かめる**（`MS2026.Stage.Editor.csproj`）
- [ ] **Step 4: Unity が使えるなら** ステージ背景スタジオで壁を 1 つ選び「シールを貼る」→ シールが上の面に散らばる・量を変えると増減する・壊せる壁でない物（床）では注意だけ出る、をスクリーンショットで確かめる。使えなければ Task 11 へ回す。
- [ ] **Step 5: コミット** — `git commit -m "シール: ステージ背景スタジオにシールの欄を足す"`

---

### Task 10: シーンビューで 1 枚ずつ直す

**Files:**
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerPick.cs`
- Create: `Assets/_Game/Stage/Editor/Stickers/StickerSceneTool.cs`
- Test: `Assets/_Game/Stage/Tests/EditMode/StickerPickTests.cs`

**Interfaces:**
- Consumes: Task 6 `StageStickerLayer`、Task 8 `StickerSurface` / `StickerScatter.Place`、Task 9 `StickerLayerOps`
- Produces:
  - `public static class StickerPick { public static int Nearest(IReadOnlyList<Vector2> screenPoints, Vector2 mouse, float maxPixels) }` — 一番近い番号。maxPixels より遠ければ -1。
  - `public static class StickerSceneTool`
    - `public static StageStickerLayer Active { get; }` / `public static int Selected { get; }`
    - `public static string AddDesignId { get; set; }`（「足す絵柄」。Task 9 の選択が入れる）
    - `public static void Begin(StageStickerLayer layer)` — `SceneView.duringSceneGui` に登録、`StickerSurface.FromProp` を作って持つ。`public static void End()` — 外す。選んでいる物が変わった・シーンが閉じた・Play に入ったら自動で `End()`。
  - 操作（シーンビュー。Active の間だけ）:

| 操作 | 動き |
|---|---|
| クリック | 近いシール（20px 以内）を選ぶ。何も無ければ選択を外す |
| Shift＋クリック | 面のその場所に「足す絵柄」を 1 枚足す（`Place`、大きさ = 絵柄の大きさ × 倍率、回転 0、`handEdited = true`） |
| 選んだシールをドラッグ | 面の上を動かす（マウスの下に `Raycast` し直して `Place` で作り直す。大きさ・回転・絵柄はそのまま） |
| Q ／ E | 15° ずつ回す |
| [ ／ ] | 10% ずつ小さく／大きく |
| Delete ／ Backspace | 消す |
| Esc | 直すのをやめる（`End()`） |

    - 変えるたびに: `Undo.RecordObject(layer, "シールを直す")` → 変更 → `handEdited = true` → **そのシールを list の最後へ移す**（重なったら上に来るように。Task 8 の重ね順の決まり）→ 格子を `Place` で作り直す（浮かせる量は最後の番号のもの）→ `StickerMeshAssets.Rebuild(layer)` → `EditorUtility.SetDirty` → `PrefabUtility.RecordPrefabInstancePropertyModifications(layer)`。
    - 表示: 全シールの中心に小さな円（`Handles.DrawWireDisc`）、選んだシールは太く。選んだシールの近くに「Q/E 回す・[ ] 大きさ・Delete 消す」のラベル。
    - マウスの光線は `HandleUtility.GUIPointToWorldRay` を `layer.transform` のローカルに直して `StickerSurface.Raycast` に渡す。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test] public void Nearest_PicksClosestWithinRange()
{   var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(30, 0) };
    Assert.AreEqual(1, StickerPick.Nearest(pts, new Vector2(12, 1), 20f)); }
[Test] public void Nearest_ReturnsMinusOneWhenTooFar()
{   Assert.AreEqual(-1, StickerPick.Nearest(new List<Vector2> { new Vector2(0, 0) }, new Vector2(50, 0), 20f)); }
[Test] public void Nearest_EmptyList_ReturnsMinusOne()
{   Assert.AreEqual(-1, StickerPick.Nearest(new List<Vector2>(), Vector2.zero, 20f)); }
```

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: `StickerPick` と `StickerSceneTool` を書く**
- [ ] **Step 4: コンパイル（＋あれば Test Runner）で通るのを確かめる**
- [ ] **Step 5: コミット** — `git commit -m "シール: シーンビューで1枚ずつ動かす・回す・大きさ・消す・足す"`

---

### Task 11: まとめての確認と報告

**Files:** なし（直すところがあれば、そのタスクのファイル）

- [ ] **Step 1: 全部のコンパイル** — Stage.Runtime／Stage.Editor／Stage.Tests.EditMode／Fortress.Runtime／Fortress.Tests.EditMode／RedSkill.Runtime の csproj（またはUnity）でエラー 0。
- [ ] **Step 2: テスト** — Unity の Test Runner（EditMode）で `MS2026.Stage.Tests.EditMode` と `MS2026.Fortress.Tests.EditMode` が全部通る。Unity が使えなければ、ユーザーに回してもらうよう報告に書く。
- [ ] **Step 3: Unity での見た目の確認**（Unity が使えるなら自分で、使えなければユーザーへのお願いとして報告に並べる）
  1. 絵柄の一覧に PNG を 3 枚入れ、紙・ホログラム・ぷっくりを 1 つずつにする。
  2. ステージ背景スタジオで壁に「シールを貼る」。見本ページ（`2026-11-26-obstacle-stickers-look-demo.html`）と見比べ、3 つの見え方が近いか。
  3. Play で赤のスキル → 当たった所が光り、当て続けると焦げ、やめると光りは冷めて焦げは残る。ふだんの砲台のレーザーではシールが何も変わらない。
  4. 黄色のスキル → はね返った所が一瞬光って少し焦げる。
  5. 壊せる壁 → 壊れるとシールも同じように溶けて消える。再生するとシールは新品。
  6. 砲台が壁の裏に入ったとき、壁と同じ所でシールにも穴があく（透け）。
  7. 壁が敵に押されて揺れるとき、シールも一緒に揺れる。
  8. 根元が広い物（ボトルなど）でも、赤のスキルを当てた側のシールが光るか。届かないときは `StageLookProfile` の `stickers.sustainedRadius` を何 m にすれば届くかを調べて報告する（勝手に初期値は変えない）。
  9. シールを貼った壁で「Prefab に保存」→ シーンを開き直してもシールが残っている・保存していない変更の印が残り続けない・コンソールに参照切れの警告が出ない。障害物を複製してシールを直すと、元の障害物のシールが変わらない（メッシュのアセットが分かれる）。
  10. シールが重なった所がちらつかない。
  11. `Beautiful_2D_Analysis.md` の `[CHECK]` の P1（4 人・敵がいっぱいの場面で敵が読めるか）と P8（光りすぎていないか）を見て、気になる点を報告する（値は勝手に大きく変えない）。
- [ ] **Step 4: 報告**（日本語）— 作ったもの・コミットの一覧・確かめた事・確かめられなかった事（シェーダー・Test Runner など）・Task 2 Step 3 の結果（Client でも報告されるか）・初期値から変えた値があればその値。ツールの資料は更新していないこと。
