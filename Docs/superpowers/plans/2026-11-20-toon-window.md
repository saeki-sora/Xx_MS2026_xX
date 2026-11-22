# 3Dトゥーン調整ウィンドウ（C 細長パネル型）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Tools/3Dオブジェクト トゥーン/調整ウィンドウ` を、デザイナーさん向けの細長い1列の UI Toolkit 画面に置き換える。

**Architecture:** 薄い EditorWindow が、状態クラス（ToonContext）と3つの欄（見た目・物ごと・準備）を並べる。項目は表（ToonCatalog）で決め、行（ToonPropertyRow）が材質の値を読み書きする。値を配る・材質を作る処理は今の ObjectToonMaterials をそのまま使う。

**Tech Stack:** Unity 6 エディタ、UI Toolkit、StudioKit（uss・StudioUi）、C#。

**Spec:** `Docs/superpowers/specs/2026-11-20-toon-window-design.md`

## Global Constraints
- シェーダー・ObjectToonApplier・ObjectToonProfile の形は変えない。StudioKit も変えない。
- 画面の文言は日本語。どの部品にも tooltip（下の帯に出る説明）を付ける。
- よく触る8つ: 影の色・影の広さ・境目のくっきり・影の面の明るさ・線の太さ・線の色・彩度・全体の明るさ。
- 折りたたみ: 影／輪郭線／色と光／ハイライト・ふちの光。物ごと調整中だけ「この物の色」「鏡・映り込み」。
- 変えた所の基準: 共通の設定を調整中はシェーダーの初期値。物ごとに調整中は共通の設定の値。物ごとの値（PerMaterialProperties）には印を付けない。
- 確かめ方: 一時的な csproj（新しいファイルと StudioKit の参照を足した物）を `dotnet msbuild` でコンパイルする。

## Review Focus
- 元に戻す（Ctrl+Z）: 行の値が材質に合わせて描き直され、共通の設定なら仲間にも配られる。
- 再生中: 値を変えるとすぐゲームに出る。プレビューのスイッチは押せない。
- 選んだ物に描画する部品が無い・対象外のシーン・トゥーンの対象外の材質: カードに理由を出し、ボタンは押せない。
- 調整中の材質が消された・設定ファイルが無い: 共通の設定に戻す／はじめるボタンを出す（落ちない）。
- 検索に合う物が無い: 「合う項目はありません」と出す。

---

### Task 1: 項目の表と値の読み書き
**Files:** Create `Assets/3DObjectshader/Scripts/Editor/Window/ToonCatalog.cs`, `ToonValues.cs`
**Produces:** `enum ToonGroup { Shadow, Line, ColorLight, Shine, ThisObjectColor, Mirror }`、`enum ToonKind { Auto, Toggle, TwoSided }`、`sealed class ToonItem { Id, Label, Group, Top, Parent, Tip, Kind, Keyword }`、`static class ToonCatalog { IReadOnlyList<ToonItem> Items; string GroupLabel(ToonGroup); bool IsPerMaterialGroup(ToonGroup); bool Matches(ToonItem, string query) }`、`static class ToonValues { bool IsChanged(Material target, Material baseline, ToonItem); void Reset(Material target, Material baseline, ToonItem); float Default float/Color from shader }`。
- [ ] 表を書く（ラベルは見本ページと同じ）。`Matches` は空なら true、ラベルか見出しに含めば true。
- [ ] `ToonValues` はシェーダーの `GetPropertyDefault*Value` と比べる。baseline があればその値と比べる。

### Task 2: 状態と行
**Files:** Create `ToonContext.cs`, `ToonPropertyRow.cs`, `ObjectToon.uss`
**Produces:** `ToonContext { Profile; Material Target; string TargetLabel; bool IsCommon; Material Baseline; event Action ValuesChanged, TargetChanged; void UseCommon(); void UseMaterial(Material, string label); void Write(Action<Material> change, string undoName); IReadOnlyList<Material> Missing; void RefreshMissing() }`、`ToonPropertyRow : VisualElement { ToonItem Item; void Bind(ToonContext); void Pull(); bool Changed }`。
- [ ] Write は Undo.RecordObject → 変更 → SyncOutlinePass → PushIfCommon → RepaintViews → ValuesChanged。

### Task 3: 3つの欄と窓
**Files:** Create `ToonLookSection.cs`, `ToonObjectsSection.cs`, `ToonSetupSection.cs`; Rewrite `ObjectToonWindow.cs`; Modify `MS2026.ObjectToon.Editor.asmdef`（MS2026.StudioKit.Editor を参照）
- [ ] 見た目: 検索・変えた所＋すべて戻す・知らせ・よく触る8つ・折りたたみ（見出しに変えた数）。
- [ ] 物ごと: 選んでいる物のカード（この材質の物を調整／この物だけ調整／鏡にする）と材質の一覧。
- [ ] 準備: ゲーム中にトゥーン・対象のシーン・材質探し・2Dの絵（オンオフ・シーン・2D の共通の値は MaterialEditor を IMGUIContainer で）。
- [ ] 窓: 上の帯（題名・プレビュー）・調整中の帯・ScrollView・下の説明の帯。Undo と選択の変化を受けて描き直す。
- [ ] コンパイル確認: 一時 csproj で `dotnet msbuild` → エラー 0。
