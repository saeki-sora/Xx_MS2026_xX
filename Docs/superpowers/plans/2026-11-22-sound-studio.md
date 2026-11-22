# サウンドスタジオ 作る手順の計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 遊ぶ流れの順にシーンを並べ、各タイミングの SE・声・BGM を入れる・試聴する・音量を合わせるエディタの窓「サウンドスタジオ」を作る（1段目）。そのあと、まだ欄がない候補 N1〜N20 を全部ゲームに足す（2段目）。

**Architecture:** ゲーム側は `FortressEffect` に3つの項目（鳴らすPC・この場所の倍率・声かどうか）を足す。判断と計算は、Unity に頼らない小さな静的クラス（`FortressSoundRules`）に置く。音量は `FortressAudioMix` が D-Drive の Handle／BGM のデータの複製に掛ける。エディタ側は StudioKit の `StudioShell` を使う。中身は「欄を集める係（Scanner）」「名前の表（Catalog）」「ページ・行・細かい設定」「試聴」「wav の取り込み」に分けたファイルで、窓は並べるだけにする。

**Tech Stack:** Unity 6（UI Toolkit のエディタ窓）、C#、D-Drive v1.2.1（`com.ddrive.core`、パッケージは触らない）、NUnit（EditMode テスト）、Netcode（Host の判定）。

**Spec:** `Docs/superpowers/specs/2026-11-22-sound-studio-design.md`（作る前に必ず読む）

## Global Constraints

- ブランチ: main から日本語の名前で切る → `git switch -c サウンドスタジオ main`。今の作業ブランチの変更は入れない。作業ツリーにある他の人の未コミットの変更（あれば）には触らない。
- 窓のメニュー: `Tools/サウンドスタジオ`。窓の題名: 「サウンドスタジオ」。
- 画面の言葉はやさしい日本語で、クラス名・フィールド名は出さない。どの項目にも Tooltip を付ける（下の帯に出る）。
- 最初に見える物は少なく、細かい設定は行を選んだときだけ出す（`Docs/Reference/Tool_UI_Analysis.md` の T2）。
- この場所の倍率は 0〜1（初期値 1）。1 より上は作らない。
- 新しい項目の初期値は今の動きと同じにする（鳴らすPC＝おまかせ、倍率＝1、声＝いいえ）。今ある設定の音は何も変わらない。
- 演出欄は `FortressEffect` にそろえ、`FortressEffectPlayer` で鳴らす。VFX の Flags › Net は Local のまま。VFX の欄はツールで変えない。
- ゲームのコードで `AudioSource.Play`・`Instantiate`（プレハブ）・`Resources.Load` を直接使わない。D-Drive のパッケージと DDrive の asmdef は改造しない。
- `.unity` `.asset` `.meta` をテキストで書き換えない（Unity の API で変える）。設定ファイルをエディタで変えるときは `Undo.RecordObject` と `EditorUtility.SetDirty` を使う。
- ツールの資料（`Docs/Tools/`）と `MultiplayerProgress.md` は更新しない。
- 1つのタスクを1つのコミットにする。2段目は1つの候補（N の番号）を1つのコミットにする。コミットの最後に `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` を付ける。
- 確かめ方: UnityMCP はつながっていない。Unity が作った csproj を scratchpad に写し、新しいファイルを `<Compile Include>` に足して `dotnet msbuild` でコンパイルする（参照は `Library/ScriptAssemblies`）。新しい asmdef には csproj がまだ無いので、近い csproj（`MS2026.SkillStudio.Editor.csproj`）を写して作る。テストはコンパイルまでを確かめ、実行は Unity の Test Runner でユーザーに頼む（できなかった事はそのまま報告する）。

## Review Focus

- **D-Drive の準備前・Play していないときに鳴らす**: 今と同じく何もしない（警告を出さない）。`FortressAudioMix` が `DDriveRuntimeBootstrap.Instance == null` や `Registry` が無いときに例外を出さないこと → Task 2 にテストを足す（`ApplySeVolume` に null の Bootstrap を渡しても例外が出ない）。
- **連打の間引き中に鳴らした**: `PlaySe` が無効な Handle を返す。音量を掛けずに何もしない → Task 2 のテスト。
- **シーンにある欄を、そのシーンを開いていないときに変えた**: 裏で開いて保存して閉じる。ユーザーが開いているシーンを閉じたり、保存していない変更を勝手に保存したりしない → Task 7 で、開いているシーンは「保存してください」と出すだけにする手順と、その確かめをする。
- **同じ英字の名前の wav をもう一度落とした**: 上書きするか聞く。「いいえ」なら何もしない → Task 9 のテスト（`SeImportNaming.MakeFileName` が同じ名前を返すとき、`SeImporter` が聞く道に入る）。
- **ネットにつながっていない・1人で遊ぶ・持ち主が分からない（-1）**: その人の音も鳴らす（今と同じ）→ Task 1 のテストで全部の組み合わせを押さえる。

---

## 1段目: ツールと、ゲームが新しい項目を使うようにする

### Task 1: ブランチ、欄の新しい項目、鳴らすかどうかの判断

**Files:**
- Create: `Assets/_Game/Fortress/Runtime/Effects/SoundAudience.cs`
- Create: `Assets/_Game/Fortress/Runtime/Effects/FortressSoundRules.cs`
- Modify: `Assets/_Game/Fortress/Runtime/Effects/FortressEffect.cs`（項目を3つ足す）
- Test: `Assets/_Game/Fortress/Tests/EditMode/FortressSoundRulesTests.cs`

**Interfaces:**
- Produces:
  - `public enum SoundAudience { Auto = 0, Everyone = 1, OwnerOnly = 2 }`（Auto は「持ち主の番号が渡されたらその人のPCだけ、渡されなければ全員のPC」。ユーザー決定「その人の事は最初はその人のPCだけ」を、今ある設定を書き換えずに実現するための値）
  - `FortressEffect` に `public SoundAudience audience = SoundAudience.Auto;`、`[Range(0f, 1f)] public float volumeScale = 1f;`、`[HideInInspector] public bool isVoice;`（Tooltip は日本語）
  - `public static class FortressSoundRules`
    - `public static bool ShouldPlay(SoundAudience audience, int ownerIndex, int localIndex, bool isHost, bool ownerIsHuman)`
    - `public static float FinalVolume(float soundVolume, float slotScale, float master)` … 3つを掛けて 0〜1 に収める

- [ ] **Step 1: ブランチを切る**

Run: `git switch -c サウンドスタジオ main` → Expected: `Switched to a new branch 'サウンドスタジオ'`。設計書（`Docs/superpowers/specs/2026-11-22-sound-studio-design.md`）とこの計画が作業ツリーにあることを確かめる。

- [ ] **Step 2: 失敗するテストを書く**

```csharp
// ShouldPlay: 1行 = (audience, owner, local, isHost, ownerIsHuman) → 期待
[TestCase(SoundAudience.Everyone,  2,  0, false, true,  ExpectedResult = true)]
[TestCase(SoundAudience.OwnerOnly, 2,  0, false, true,  ExpectedResult = false)]
[TestCase(SoundAudience.OwnerOnly, 0,  0, false, true,  ExpectedResult = true)]
[TestCase(SoundAudience.OwnerOnly, 2, -1, false, true,  ExpectedResult = true)]  // ネット無し・1人
[TestCase(SoundAudience.OwnerOnly, -1, 0, false, true,  ExpectedResult = true)]  // 持ち主が分からない
[TestCase(SoundAudience.OwnerOnly, 2,  0, true,  false, ExpectedResult = true)]  // CPU の席 → Host で鳴らす
[TestCase(SoundAudience.OwnerOnly, 2,  1, false, false, ExpectedResult = false)] // CPU の席 → Client では鳴らさない
[TestCase(SoundAudience.Auto,      2,  0, false, true,  ExpectedResult = false)] // 番号あり → その人だけ
[TestCase(SoundAudience.Auto,      -1, 0, false, true,  ExpectedResult = true)]  // 番号なし → 全員
public bool ShouldPlay_Cases(SoundAudience a, int owner, int local, bool host, bool human)
    => FortressSoundRules.ShouldPlay(a, owner, local, host, human);

[Test] public void FinalVolume_Multiplies() => Assert.That(FortressSoundRules.FinalVolume(0.8f, 0.5f, 0.5f), Is.EqualTo(0.2f).Within(1e-5f));
[Test] public void FinalVolume_ClampsToOne() => Assert.That(FortressSoundRules.FinalVolume(1f, 1f, 2f), Is.EqualTo(1f));
[Test] public void FinalVolume_ClampsNegative() => Assert.That(FortressSoundRules.FinalVolume(1f, -1f, 1f), Is.EqualTo(0f));
[Test] public void NewEffect_DefaultsKeepCurrentBehaviour()
{
    var e = new FortressEffect();
    Assert.That(e.audience, Is.EqualTo(SoundAudience.Auto));
    Assert.That(e.volumeScale, Is.EqualTo(1f));
    Assert.That(e.isVoice, Is.False);
}
```

- [ ] **Step 3: コンパイルして失敗を確かめる**（`MS2026.Fortress.Tests.EditMode.csproj` を写してテストを足し `dotnet msbuild`）→ Expected: `SoundAudience` / `FortressSoundRules` が無いというエラー。

- [ ] **Step 4: `SoundAudience`・`FortressSoundRules`・`FortressEffect` の項目を作る**

- [ ] **Step 5: もう一度コンパイルする**（`MS2026.Fortress.Runtime` とテストの両方）→ Expected: エラー 0。

- [ ] **Step 6: コミット**: `git add` に上の4ファイルとその `.meta`（Unity が作るまで無ければ、Unity を開いたあとの Task 10 で足す）、設計書、この計画 → `git commit -m "サウンドスタジオ: 欄に鳴らすPC・この場所の倍率・声の印を足す"`

### Task 2: 全体音量の設定と、音量の掛け方

**Files:**
- Create: `Assets/_Game/Fortress/Runtime/Effects/FortressSoundBalance.cs`（ScriptableObject）
- Create: `Assets/_Game/Fortress/Runtime/Effects/FortressAudioMix.cs`（static）
- Create: `Assets/_Game/Fortress/Editor/Effects/FortressSoundBalanceEditorLoader.cs`（`[InitializeOnLoad]`）
- Modify: `Assets/_Game/Fortress/Runtime/Effects/FortressEffectPlayer.cs`
- Test: `Assets/_Game/Fortress/Tests/EditMode/FortressAudioMixTests.cs`

**Interfaces:**
- Consumes: Task 1 の `FortressSoundRules`、`FortressEffect.audience/volumeScale/isVoice`
- Produces:
  - `public sealed class FortressSoundBalance : ScriptableObject { [Range(0,1)] public float seVolume = 1f, voiceVolume = 1f, bgmVolume = 1f; public static FortressSoundBalance Current { get; set; } }`。`OnEnable` で `Current ??= this`。ビルドでは PlayerSettings の Preloaded Assets に入れて読ませ（Task 6 のツールが `PlayerSettings.SetPreloadedAssets` で足す）、エディタでは `FortressSoundBalanceEditorLoader` が `AssetDatabase.FindAssets("t:FortressSoundBalance")` の1つ目を `Current` に入れる。置き場所: `Assets/_Game/Fortress/Data/Audio/FortressSoundBalance.asset`（Task 6 のツールが無ければ作る）。`Current` が null のときは全部 1 として扱う。
  - `public static class FortressAudioMix`
    - `public static float Master(bool isVoice)` / `public static float BgmMaster`
    - `public static void ApplySeVolume(Handle<SeMarker> handle, AssetId<SeMarker> id, float slotScale, bool isVoice)` … `DDriveRuntimeBootstrap.Instance?.Registry.TryResolveSync<SeData>(id.Value, out var data)` で音そのものの音量を読み、`Instance.Audio.SetVolume(handle, FortressSoundRules.FinalVolume(data.Volume, slotScale, Master(isVoice)))`。Handle が Invalid・Bootstrap が無い・読めないときは何もしない。倍率 1 かつ全体音量 1 のときも何もしない（今と同じ音量なので呼ばない）。
    - `public static void PlayBgm(AssetId<BgmMarker> id, float fadeIn = -1f)` … `BgmMaster` が 1 なら今と同じ `Audio.PlayBgm(id, fadeIn)`。1 未満なら `TryResolveSync<BgmData>` で読み、`Volume` を掛けた複製（`ScriptableObject.Instantiate`、id ごとに使い回す）を `Instance.Bgm.PlayBgmData(clone, fadeIn)` で鳴らす。
    - `public static bool OwnerIsHuman(int ownerIndex)` … `FortressNetworkBootstrap.Instance?.ClientIdToPlayerIndex` の値に含まれるか（Host のときだけ意味がある）
    - `public static bool IsHost` … `NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer`
  - `FortressEffectPlayer.PlaySound(FortressEffect effect, Vector3 position, int playerIndex = -1)`: `ShouldPlay(effect.audience, playerIndex, LocalPlayerIndex(-1 if no bootstrap), IsHost, OwnerIsHuman(playerIndex))` が false なら鳴らさない。鳴らしたら `ApplySeVolume`。`PlayOnce` は受け取った `playerIndex` を `PlaySound` に渡す。
  - エディタだけ: `#if UNITY_EDITOR public static event Action<FortressEffect, int> Played; #endif` … 実際に鳴らしたとき（ShouldPlay が true で PlaySe を呼んだ後）に呼ぶ。Task 8 の「鳴った行を光らせる」が使う。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test] public void ApplySeVolume_NoBootstrap_DoesNotThrow()
    => Assert.DoesNotThrow(() => FortressAudioMix.ApplySeVolume(Handle<SeMarker>.Invalid, default, 0.5f, false));
[Test] public void Master_UsesVoiceVolumeForVoice()
{
    var b = ScriptableObject.CreateInstance<FortressSoundBalance>(); b.seVolume = 0.3f; b.voiceVolume = 0.7f;
    FortressSoundBalance.Current = b;
    Assert.That(FortressAudioMix.Master(true), Is.EqualTo(0.7f));
    Assert.That(FortressAudioMix.Master(false), Is.EqualTo(0.3f));
    FortressSoundBalance.Current = null;
    Assert.That(FortressAudioMix.Master(false), Is.EqualTo(1f));
}
[Test] public void PlaySound_NotReady_DoesNotThrow()
    => Assert.DoesNotThrow(() => FortressEffectPlayer.PlaySound(new FortressEffect(), Vector3.zero, 2));
```

- [ ] **Step 2: コンパイルして失敗を確かめる** → Expected: 型が無いエラー。
- [ ] **Step 3: 上の Interfaces のとおり作る**（`MS2026.Fortress.Runtime.asmdef` は Netcode と DDrive をもう参照しているので asmdef は変えない）
- [ ] **Step 4: コンパイル**（Runtime・Editor・Tests）→ Expected: エラー 0。今ある `PlaySound(effect, pos)` の呼び出しはそのままコンパイルが通ること。
- [ ] **Step 5: コミット** `"サウンドスタジオ: 全体音量とこの場所の倍率をゲームの音に掛ける"`

### Task 3: 誰の音かを渡していない呼び出しと、BGM を鳴らす所を直す

**Files（Modify）:**
- `Assets/_Game/Fortress/Runtime/Laser/LaserTurretEffects.cs:108,150,184` → `PlaySound(effect, pos, _turret.playerIndex)`
- `Assets/CharacterSelect/Scripts/Runtime/CharacterSelectEffects.cs:26` → `PlaySound(effect, position, playerIndex)`
- `Assets/_Game/Fortress/Runtime/Seats/FortressSeatMover.cs:515` → `PlaySound(effect, position, jumper.Player)`
- BGM: `FortressBgmPlayer.cs:119,167`、`Assets/_Game/Stage/Runtime/Stage/StageRoot.cs:200`、`Assets/_Game/UI/Game/Session/GameSession.Match.cs:480` の `Audio.PlayBgm(...)` → `FortressAudioMix.PlayBgm(...)`（どのアセンブリも `MS2026.Fortress.Runtime` を参照済み）

**Interfaces:**
- Consumes: Task 2 の `PlaySound(effect, pos, playerIndex)`、`FortressAudioMix.PlayBgm`
- 決まり: 「その人の事」の欄＝呼び出しが持ち主の番号を渡している欄。`PlayOnce(..., owner)` は Task 2 で自動で渡るので直さない。番号を渡さない呼び出し（`StageCoreDrop`・`FortressWaveRunner`・`GameSession.*`・ルーレット・`PasteCurtain` の -1 など）はみんなの事なので、そのままにする。

- [ ] **Step 1: 直す**
- [ ] **Step 2: 変えた所が他に残っていないか確かめる**: Run `grep -rn "Audio.PlayBgm(" Assets --include=*.cs` → Expected: `FortressAudioMix.cs` の中だけ。
- [ ] **Step 3: コンパイル**（Fortress.Runtime・CharacterSelect.Runtime・Stage.Runtime・UI.Game）→ Expected: エラー 0。
- [ ] **Step 4: コミット** `"サウンドスタジオ: その人の音に持ち主の番号を渡し、BGM に全体音量を効かせる"`

### Task 4: エディタのアセンブリと、名前の表（Catalog）

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/MS2026.SoundStudio.Editor.asmdef`（参照: `MS2026.StudioKit.Editor`, `MS2026.Fortress.Runtime`, `MS2026.Fortress.Editor`, `DDrive.Runtime`, `DDrive.Foundation`, `DDrive.Editor`、Editor のみ）。ほかに、表に載せる持ち主の型があるアセンブリ（RedSkill・BlueSkill・Title・CharacterSelect・HowToPlay・Stages・Stage・UI.Game・ObjectToon.KillFx の Runtime）も参照する。
- Create: `Assets/_Tools/SoundStudio/Editor/Catalog/SoundPageId.cs`、`SoundCatalogEntry.cs`、`SoundCatalog.cs`、`SoundCandidate.cs`
- Create: `Assets/_Tools/SoundStudio/Tests/Editor/MS2026.SoundStudio.Tests.Editor.asmdef`（`UNITY_INCLUDE_TESTS`、nunit）
- Test: `Assets/_Tools/SoundStudio/Tests/Editor/SoundCatalogTests.cs`

**Interfaces:**
- Produces:
  - `public enum SoundPageId { Start, Title, Lobby, CharacterSelect, HowToPlay, Roulette, StageIntro, Match, Boss, Result, Other }`（並びは設計書の流れの順。`Start`＝「はじめに」）
  - `public static string SoundPageIds.Label(SoundPageId)`（日本語）、`public static string SoundPageIds.SeFolder(SoundPageId)`（wav を入れるフォルダ名: `Title`, `Lobby`, `CharacterSelect`, `HowToPlay`, `Roulette`, `StageIntro`, `Match`, `Boss`, `Result`, `Other`）
  - `public sealed class SoundCatalogEntry { string HolderType; string PropertyPath; SoundPageId Page; string Group; string Label; bool Personal; bool Voice; }`（`HolderType` は型の完全名。`PropertyPath` は SerializedProperty の道筋。配列の中は `[]` でまとめる。例 `sounds.onReady`）
  - `public static class SoundCatalog { IReadOnlyList<SoundCatalogEntry> Entries; SoundCatalogEntry Find(string holderType, string propertyPath); IReadOnlyList<SoundCandidate> Candidates; }`。`Find` は `.Array.data[3]` を `[]` に直してから探す。
  - `public sealed class SoundCandidate { string Number; SoundPageId Page; string Group; string Label; bool Personal; bool Voice; bool IsBgm; }`（N1〜N20。中身は設計書の表と同じ）
- 表の中身: プロジェクトにある `FortressEffect` の欄（約108）と BGM の欄（`FortressBgmPlayer.bgm/extraBgms`、`GameSession.lobbyBgm`、`StageSet.bgm`）を全部載せる。名前と小見出しは作る人が決めてよい（設計書の「作る人に任せる所」）。`Personal` は Task 3 の決まり（呼び出しが持ち主の番号を渡すか）に合わせる。`Voice` は欄の名前に `voice` / `Voice` が入る物（例 `GreenMint` の `landVoice`）。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[Test] public void Find_NormalizesArrayIndex()
{
    var e = SoundCatalog.Entries[0];
    var path = e.PropertyPath.Replace("[]", ".Array.data[3]");
    Assert.That(SoundCatalog.Find(e.HolderType, path), Is.SameAs(e));
}
[Test] public void Entries_AreUnique() =>
    Assert.That(SoundCatalog.Entries.Select(x => x.HolderType + "|" + x.PropertyPath), Is.Unique);
[Test] public void Entries_HolderTypesExist() { foreach (var e in SoundCatalog.Entries) Assert.That(FindType(e.HolderType), Is.Not.Null, e.HolderType); }
[Test] public void Entries_PathsExistOnHolder() { /* 各型を反射でたどり、道筋の各段のフィールドが在ることを確かめる（名前が変わったら気付ける） */ }
[Test] public void Candidates_AreN1ToN20() =>
    Assert.That(SoundCatalog.Candidates.Select(c => c.Number), Is.EqualTo(Enumerable.Range(1, 20).Select(i => "N" + i)));
[Test] public void Pages_InFlowOrder() =>
    Assert.That((int)SoundPageId.Title < (int)SoundPageId.Lobby && (int)SoundPageId.Boss < (int)SoundPageId.Result);
```

- [ ] **Step 2: コンパイルして失敗を確かめる**（新しい asmdef 2つは csproj を写して作る）
- [ ] **Step 3: 作る。** 欄の洗い出しは `grep -rn "public FortressEffect \|FortressEffect \w* = new" Assets --include=*.cs` と、入れ子の設定クラス（`LaserEffectSettings` など、設計書の「作り」にある型）をたどって行う。
- [ ] **Step 4: コンパイル** → Expected: エラー 0。
- [ ] **Step 5: コミット** `"サウンドスタジオ: エディタのアセンブリと、欄の名前の表"`

### Task 5: 欄を集める係（Scanner）

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/Scan/SoundSlot.cs`、`SoundSlotKind.cs`、`SlotPathFinder.cs`、`SoundSlotScanner.cs`
- Test: `Assets/_Tools/SoundStudio/Tests/Editor/SlotPathFinderTests.cs`（テスト用の ScriptableObject `TestSoundHolder` もここに置く）

**Interfaces:**
- Consumes: Task 4 の `SoundCatalog.Find`、`SoundCandidate`
- Produces:
  - `public enum SoundSlotKind { Effect, Bgm, Candidate }`
  - `public sealed class SoundSlot { SoundSlotKind Kind; SoundCatalogEntry Entry /* 表に無ければ null → その他 */; SoundCandidate Candidate; UnityEngine.Object Owner /* SO は本体、シーンの物は null */; string AssetPath /* SO・Prefab・シーンの .unity の道 */; string GlobalObjectId /* シーン・Prefab の部品の場所（GlobalObjectId.ToString()） */; string PropertyPath; string HolderType; string Tooltip; SoundPageId Page; string Group; string Label; }`
  - `public static class SlotPathFinder { public static List<string> FindPaths(SerializedObject so, out List<string> bgmPaths); }` … `FortressEffect` の欄（`type == "FortressEffect"`）と `AssetId<BgmMarker>` の欄の道筋を、配列の中まで返す。
  - `public static class SoundSlotScanner { public static List<SoundSlot> ScanAll(); }` … (1) `t:ScriptableObject` の全アセット、(2) `t:Prefab` の全 Prefab の部品、(3) Build Settings のシーン（開いていなければ `OpenScene(path, OpenSceneMode.Additive)` で開き、読んだら閉じる。開いていたシーンは閉じない）を調べる。最後に候補 N1〜N20 を `Candidate` として足す。呼ぶのは窓を開いたときと［調べ直す］のときだけ。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// TestSoundHolder: FortressEffect a; FortressEffect[] list (2個); Nested nested { FortressEffect inner; }; AssetId<BgmMarker> music;
[Test] public void FindPaths_FindsDirectArrayAndNested()
{
    var so = new SerializedObject(ScriptableObject.CreateInstance<TestSoundHolder>());
    var paths = SlotPathFinder.FindPaths(so, out var bgm);
    Assert.That(paths, Is.EquivalentTo(new[] { "a", "list.Array.data[0]", "list.Array.data[1]", "nested.inner" }));
    Assert.That(bgm, Is.EqualTo(new[] { "music" }));
}
[Test] public void FindPaths_DoesNotDescendIntoFortressEffect() { /* FortressEffect の中の vfx/se を別の欄として返さない */ }
```

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: 作る**
- [ ] **Step 4: コンパイル** → Expected: エラー 0
- [ ] **Step 5: コミット** `"サウンドスタジオ: プロジェクトの音の欄を集める係"`

### Task 6: 窓・上の帯・ナビ・シーンのページと行

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/SoundStudioWindow.cs`（`[MenuItem("Tools/サウンドスタジオ")]`、`SkillStudioWindow` と同じ形: `CreateGUI` で `StudioShell` を作って並べるだけ、`OnInspectorUpdate` で `Tick`）
- Create: `Assets/_Tools/SoundStudio/Editor/SoundStudioContext.cs`（集めた欄・選んでいる行・全体音量の設定）
- Create: `Assets/_Tools/SoundStudio/Editor/Pages/SoundStartPage.cs`（はじめに）、`SoundScenePage.cs`（流れのシーンとその他。`IStudioPage`、`Badge`＝音がない欄の数、`BadgeIsWarning = true`）
- Create: `Assets/_Tools/SoundStudio/Editor/Panels/SoundHeader.cs`（`HeaderSlot` に: 「音あり N / M」、［音がない所だけ］、SE・声・BGM の全体音量のスライダー、［調べ直す］）、`SoundRow.cs`、`SoundBgmRow.cs`、`SoundCandidateRow.cs`
- Create: `Assets/_Tools/SoundStudio/Editor/SoundStudio.uss`（行・紫の点線・オレンジの空き・光る行の見た目だけ。土台は `StudioKit.uss`）

**Interfaces:**
- Consumes: Task 5 の `SoundSlotScanner.ScanAll()`、Task 2 の `FortressSoundBalance`
- Produces: `SoundStudioContext.Selected`（`SoundSlot`）、`event Action SelectionChanged`、`void Rescan()`、`FortressSoundBalance Balance`（無ければ `Assets/_Game/Fortress/Data/Audio/FortressSoundBalance.asset` を作り、`PlayerSettings.GetPreloadedAssets/SetPreloadedAssets` に足す）
- 画面は設計書の「画面」のとおり。行は「名前（その人／声の小さな印）・音（無ければオレンジの『音を入れる…』）・音量のバー（この場所の倍率）・▶」。持ち主が複数の同じ欄は、行の名前の後ろに持ち主の名前を小さく出す（まとめ方は作る人が決めてよい）。初期値から変えた所に印を付け、右クリックで戻せる。全体音量のスライダーは `Undo.RecordObject` で `Balance` を変える。

- [ ] **Step 1: 作る**（UI の見た目はテストしない。数え方 `int CountMissing(IEnumerable<SoundSlot>)` は `SoundStudioContext` の static にして Step 2 でテストする）
- [ ] **Step 2: テスト**: `CountMissing_IgnoresCandidates`（候補の行は「音がない」に数えない）、`CountMissing_CountsEmptyEffectAndBgm`
- [ ] **Step 3: コンパイル** → Expected: エラー 0
- [ ] **Step 4: コミット** `"サウンドスタジオ: 窓とシーンのページ"`

### Task 7: 行の細かい設定と、保存

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/Panels/SoundDetailPanel.cs`
- Create: `Assets/_Tools/SoundStudio/Editor/Edit/SoundSlotWriter.cs`
- Create: `Assets/_Tools/SoundStudio/Editor/Edit/SePicker.cs`（検索付きで SE / BGM を選ぶ小さな窓。`t:SeData` / `t:BgmData` を `DisplayName` とファイル名で探す）

**Interfaces:**
- Consumes: Task 6 の `SoundStudioContext.Selected`
- Produces: `public static class SoundSlotWriter`
  - `public static bool Edit(SoundSlot slot, Action<SerializedProperty> change)` … SO・Prefab: `SerializedObject` で変えて `ApplyModifiedProperties`（Undo が付く）、`AssetDatabase.SaveAssetIfDirty`。シーン: そのシーンが開いていればその場で変えて `EditorSceneManager.MarkSceneDirty`、戻り値で「シーンを保存してください」を出させる。開いていなければ `OpenScene(Additive)` → 変える → `SaveScene` → `CloseScene(removeScene: true)`。Play 中のシーンの欄は変えずに false。
  - 欄を変えるたびに `isVoice` を表の `Voice` と同じにする。窓を開いたとき、設定ファイル（SO）の欄で `isVoice` が表と違う物は直す（シーンは開かない）。
- 細かい設定は設計書の「行を選ぶと下に出る細かい設定」のとおり: 音そのもの（`SeData` の `Volume`・`PitchRange`・`CooldownSec`・`MaxConcurrent`。SeData を `Undo.RecordObject` で直接変える）、この場所だけ（倍率 0〜1・鳴らすPC は `Personal` の欄だけ「おまかせ（その人のPCだけ）／全員のPC／その人のPCだけ」、みんなの欄は「全員のPC」と表示だけ）、同じ音を使う所の一覧、ボタン［別の音を選ぶ］［音を外す］［D-Drive で開く］（`AssetDatabase.OpenAsset(seData)`）［設定のある所を見せる］（`Selection.activeObject` / `EditorGUIUtility.PingObject`）。

- [ ] **Step 1: 作る**
- [ ] **Step 2: テスト**: `SoundSlotWriter` のうち SO の道だけテストする（テスト用 SO をメモリに作り、`Edit` で `volumeScale` を 0.5 にすると値が変わり、`isVoice` が表のとおりになる）。
- [ ] **Step 3: 開いているシーンを勝手に保存・閉じないことを確かめる**: コードを読んで、`SaveScene`/`CloseScene` が「最初は開いていなかったシーン」の道でしか呼ばれないこと。
- [ ] **Step 4: コンパイル** → Expected: エラー 0
- [ ] **Step 5: コミット** `"サウンドスタジオ: 行の細かい設定と保存"`

### Task 8: 試聴（▶）と、Play 中に鳴った行を光らせる

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/Preview/SoundPreview.cs`
- Create: `Assets/_Tools/SoundStudio/Editor/Preview/PlayedSlotIndex.cs`

**Interfaces:**
- Consumes: Task 2 の `FortressEffectPlayer.Played`（エディタだけ）、Task 5 の `SoundSlot`
- Produces:
  - `SoundPreview.Play(SoundSlot slot)` … Play 外: `DDrive.Editor` の `PreviewService` を1つ持って `Initialize()`、`PlaySe(seData)` の Handle に `PreviewService.AudioManager.SetVolume(handle, FinalVolume(...))`、BGM は `PlayBgm(clone)`。窓を閉じたら `Dispose`。Play 中: `FortressEffectPlayer.PlaySound(effect, Vector3.zero)`（BGM は `FortressAudioMix.PlayBgm`）。`PreviewService` が使えないときは「Play 中に試聴してください」と下の帯に出す。
  - `PlayedSlotIndex` … Play に入ったときとシーンを読んだとき、表に載っている持ち主の型の部品（`FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, ...)`）と集めた SO から、反射で `FortressEffect` の実物 → `SoundSlot` の対応表を作る。`Played` が来たら対応する行とナビのシーンを 1 秒ほど光らせる。

- [ ] **Step 1: 作る**
- [ ] **Step 2: テスト**: `PlayedSlotIndex` の反射の部分（`TestSoundHolder` の `nested.inner` の実物から道筋 `nested.inner` が引ける）。
- [ ] **Step 3: コンパイル** → Expected: エラー 0
- [ ] **Step 4: コミット** `"サウンドスタジオ: 試聴と、鳴った行を光らせる"`

### Task 9: wav を落として登録する

**Files:**
- Create: `Assets/_Tools/SoundStudio/Editor/Import/SeImportNaming.cs`、`SeImporter.cs`、`SeNameDialog.cs`
- Test: `Assets/_Tools/SoundStudio/Tests/Editor/SeImportNamingTests.cs`

**Interfaces:**
- Consumes: Task 4 の `SoundPageIds.SeFolder`、Task 7 の `SoundSlotWriter.Edit`
- Produces:
  - `public static string SeImportNaming.SuggestName(string label, string sourceFileName)` … 英字・数字だけの PascalCase。label が日本語だけなら元のファイル名から作る。どちらも空なら `Sound`。
  - `public static string SeImportNaming.MakeFileName(SoundPageId page, string name)` … `Assets/_Game/DDrive/SourceAssets/Se/<SeFolder>/<name>.wav`。名前の頭にシーンのフォルダ名が重なっていたら外す（`TitleStart` → `Start`、ID は `SEID.TitleStart` になる）。
  - `SeImporter.Import(string sourcePath, SoundSlot slot)` … 名前を聞く → 同じファイルがあれば上書きするか聞く（いいえなら終わり）→ コピーして `AssetDatabase.ImportAsset` → `EditorApplication.update` で最大 10 秒、`t:SeData` のうち `Clips` にその AudioClip を持つ物ができるのを待つ → `new AssetId<SeMarker>(seData.Id, AssetType.Se)` を欄に入れる。10 秒で見つからなければ「D-Drive の登録が終わりませんでした。AssetBrowser で確かめてください」と出す。行と「はじめに」の素材置き場の両方に `DragAndDrop` を付ける（はじめにに落としたときは欄に入れず登録だけ）。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
[TestCase("Start", "a.wav", ExpectedResult = "Start")]
[TestCase("スタートを押した", "push_start.wav", ExpectedResult = "PushStart")]
[TestCase("", "", ExpectedResult = "Sound")]
public string SuggestName(string label, string file) => SeImportNaming.SuggestName(label, file);
[Test] public void MakeFileName_StripsFolderPrefix() =>
    Assert.That(SeImportNaming.MakeFileName(SoundPageId.Title, "TitleStart"), Is.EqualTo("Assets/_Game/DDrive/SourceAssets/Se/Title/Start.wav"));
[Test] public void MakeFileName_SameNameTwice_SamePath() =>
    Assert.That(SeImportNaming.MakeFileName(SoundPageId.Match, "Hit"), Is.EqualTo(SeImportNaming.MakeFileName(SoundPageId.Match, "Hit")));
```

- [ ] **Step 2: コンパイルして失敗を確かめる**
- [ ] **Step 3: 作る**（取り込みのルールが `SourceAssets/Se/` の下の新しいフォルダでも動くかを、D-Drive の取り込みルールのソースで確かめる。動かないならフォルダを `Se/Fortress/` に入れて名前の頭にシーン名を付ける形に変え、報告する）
- [ ] **Step 4: コンパイル** → Expected: エラー 0
- [ ] **Step 5: コミット** `"サウンドスタジオ: wav を落とすと D-Drive に登録して欄に入れる"`

### Task 10: 1段目をユーザーに Unity で確かめてもらう

- [ ] **Step 1:** Unity を開いてもらい、コンパイルエラーが無いこと、`.meta` ができたことを確かめる。できた `.meta` と `FortressSoundBalance.asset`、ProjectSettings の Preloaded Assets の変更をコミット `"サウンドスタジオ: Unity が作ったファイル"`。
- [ ] **Step 2:** Test Runner で `MS2026.Fortress.Tests.EditMode` と `MS2026.SoundStudio.Tests.Editor` を流してもらう → Expected: 全部 PASS。
- [ ] **Step 3:** 確かめてもらう事を短い箇条書きで渡す: 窓が開く／ナビが流れの順／▶が Play 外で鳴る／倍率を下げると小さくなる／wav を落とすと欄に入る／Play 中に鳴った行が光る／レーザーの音が自分のPCだけで鳴る（2台で）。

---

## 2段目: 候補 N1〜N20 をゲームに足す（全部。ユーザー決定）

**1つの候補の足し方（どの候補も同じ）:**
1. そのタイミングを持つ設定（下の表の「持ち主」）に `[Tooltip("…")] public FortressEffect onXxx = new FortressEffect();` を1つ足す（BGM は `AssetId<BgmMarker>`）。
2. そのタイミングが起きる所で1回だけ鳴らす。その人の事なら `FortressEffectPlayer.PlaySound(onXxx, pos, playerIndex)`（または `PlayOnce`）、みんなの事なら番号を渡さない。毎フレーム呼ばない（起きた瞬間だけ。状態が変わったフレームを見る）。
3. `SoundCatalog.Entries` に1行足し、`SoundCatalog.Candidates` からその N を消す。`Candidates_AreN1ToN20` のテストは、残っている番号だけを見る形に直す（最初の候補を足すときに1回だけ）。
4. コンパイル → コミット `"サウンドスタジオ N<番号>: <タイミング名>"`。

| N | 持ち主（欄を足す所） | 起きる所 | その人 |
|---|---|---|---|
| N1 | `TitleScreenController` | タイトルで握りはじめた瞬間（握力の値が「押した」に変わったフレーム） | ○ |
| N2 | `CharacterSelectController`（キャラごとの声の欄。キャラのデータに持たせるなら `CharacterSelect.Data` の型） | カーソルがキャラに乗った瞬間。`isVoice` は表で ○ | ○・声 |
| N3 | `GameSession`（`GameSession.Match.cs`） | 試合の残り時間が 10 秒以下になってから、整数の秒が変わるたび | — |
| N4 | `WaveNewEnemyCards`（`Assets/_Game/UI/Game/Waves/`） | 新しい敵の紹介カードが出た瞬間 | — |
| N5 | `LaserTurretEffects` の設定 | `TurretState` が `Charging` になった瞬間 | ○ |
| N6 | 同上 | `Overheated` になった瞬間 | ○ |
| N7 | 同上 | `Overheated` から抜けた瞬間 | ○ |
| N8 | 同上 | 熱がオーバーヒートの手前のしきい値（欄の横に 0〜1 の `heatWarnAt`、初期値 0.8）を上へ越えた瞬間。下がって戻るまで2回鳴らさない | ○ |
| N9 | `CoreCrystalController` | コアの HP がしきい値（`pinchAt`、初期値 0.25）を下へ越えた瞬間。回復して上へ戻るまで2回鳴らさない | — |
| N10 | `ComboTracker` を見る所（`FortressMatch`） | その人のコンボが 10・50・100・以後 100 ごとに届いた瞬間 | ○ |
| N11 | 敵を出す所（`FortressWaveRunner`） | ウェーブの中で敵の出現が始まった瞬間（1体ごとではなく、出現のまとまりごと。間引きは SE の `CooldownSec`） | — |
| N12 | `HudCoreHealFx`（`Assets/_Game/UI/Game/`） | 回復の演出が始まった瞬間 | — |
| N13 | `MatchResultPresenter` | 結果画面が出た瞬間 | — |
| N14 | 同上 | 数字が増えていく間（`CooldownSec` で間引く前提で、数字が変わるたび） | — |
| N15 | 同上 | 最大コンボを見せた瞬間 | — |
| N16 | 同上 | タイトルへ戻る操作をした瞬間 | — |
| N17 | Title シーンに `FortressBgmPlayer` を置く（ツールのエディタ用メニューから置き、シーンはテキストで書き換えない） | シーン開始 | — |
| N18 | Character_Select シーン（遊び方も同じシーンなら1つ）に `FortressBgmPlayer` | シーン開始 | — |
| N19 | `BossDefinition` に `AssetId<BgmMarker> bossBgm` | ボス戦が始まった瞬間に `FortressAudioMix.PlayBgm`（クロスフェード） | — |
| N20 | `MatchResultPresenter` に `clearBgm` / `gameOverBgm` | 結果画面が出た瞬間 | — |

- 起きる所が表と違って見つからないとき、または同じ事をもう鳴らしている欄がすでにあるときは、その N は作らずに報告する（同じ音が二重に鳴るのを避ける）。
- N17・N18 はシーンに物を置くので、Unity が開いているときにユーザーに頼むか、Unity を開いたあとにエディタのメニュー（`Tools/サウンドスタジオ/BGM の係を置く`。このタスクで作る）で置く。
- 2段目の最後に、Task 10 と同じくユーザーに Unity でコンパイル・テスト・試しのプレイを頼む。
