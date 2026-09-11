# MS2026 ShaderFX

データドリブンにシェーダーエフェクトを追加・調整・一括適用するための Embedded UPM パッケージです。
`Unity_ShaderFX_計画書兼手順書.md` のフェーズ0〜5（土台構築 + コアMVP + グループシステム + 画面系エフェクト +
エディタUX + パフォーマンス最終調整）に加え、計画書§5「追加機能・高度化の提案」のうち優先度高の4項目と
優先度中の3項目（Addressables対応を除く）、2D対応、そして「感動するレベル」を目指した追加エフェクト群
（後述）を実装しています。

## 構成

| クラス | 役割 |
|---|---|
| `EffectProfile` (ScriptableObject) | エフェクトモジュールのスタックを保持するデータアセット |
| `EffectModule` (`[SerializeReference]`) | 1エフェクト = 1クラス。オブジェクト系10種＋画面系6種を同梱 |
| `EffectTarget` (MonoBehaviour) | オブジェクトに付ける超軽量マーカー。Profile と Group(Flags) を持つだけで Update は書かない |
| `EffectGroup` (Flags enum) | プロジェクト固有のグループ分類。`Runtime/EffectGroup.cs` を自由に書き換えて使う |
| `EffectDirector` | シーンに自動生成される管理役。マテリアル共有キャッシュ・SRP Batcher対応・グループ/タグ一括適用・画面系設定の集約を担当 |
| `Shaders/ShaderFXUber.shader` | オブジェクト系10種に対応した URP 用 HLSL シェーダー（3D、`MeshRenderer` 向け。keyword 分岐） |
| `Shaders/ShaderFXUberSprite.shader` | オブジェクト系9種（Toon Shading除く）に対応した URP 用 HLSL シェーダー（2D、`SpriteRenderer` 向け。プロパティ名・keyword名は3D版と共通） |
| `ScreenFXFeature` / `ScreenFXPass` | Render Graph 対応の ScriptableRendererFeature。画面系6種を担当 |
| `Shaders/ShaderFXScreen.shader` | 画面系エフェクト用フルスクリーンシェーダー（Grayscale/Posterize/Pixelate/Outline/Shockwave/ScreenFlash/Mask/Copy） |
| `EditModeSaveSafety` (Editor) | Edit Mode プレビュー中に保存しても壊れたマテリアル参照を書き込まない安全策 |
| `ProfileBrowserWindow` (Editor) | `Tools > ShaderFX > Profile Browser` — プロジェクト内の EffectProfile 一覧・検索 |

オブジェクト系: RimLight / Dissolve / HitFlash / EmissionControl / Fire / Frost / Hologram / Glitch / UVScroll / ToonShading(3D専用)
画面系: Grayscale / Posterize / Pixelate / Outline / Shockwave / ScreenFlash

## クイックスタート

1. `Project` ウィンドウで右クリック → `Create > ShaderFX > Effect Profile` を作成
2. Inspector の「+ Add Effect」から効果を追加し、パラメータを調整
3. `ShaderFX/Uber` シェーダー（3D、`MeshRenderer`）または `ShaderFX/Uber Sprite` シェーダー（2D、
   `SpriteRenderer`）を使ったマテリアルをオブジェクトに設定
4. オブジェクトに `EffectTarget` コンポーネントを追加し、1で作った Profile を割り当て
5. Play モードに入ると `EffectDirector` が自動生成され、エフェクトが一括適用されます

`EffectProfile`/`EffectTarget`/`EffectDirector` 以下、Tween・Timeline・Volume・オーバーレイなど
このパッケージの仕組みはすべて「Materialのプロパティ名・キーワード名」だけで動いており、2Dか3Dかを
一切区別していません。3で選ぶシェーダーを変えるだけで、同じ `EffectProfile` が両方で使えます。

## 新しいエフェクトの追加方法

`EffectModule` を継承した1クラスを書くだけで、ツール本体（Director / Editor UI）を改修せずに
「+ Add Effect」メニューへ自動的に追加されます（Open-Closed）。

```csharp
[Serializable]
[EffectModuleInfo("My Effect", "オブジェクト系")]
public sealed class MyEffectModule : EffectModule
{
    public override string Keyword => "_FX_MYEFFECT";
    public override void ApplyTo(Material material) { /* material.SetXxx(...) */ }
    public override int ComputeParameterHash() => HashCode.Combine(/* パラメータ */);
    public override string GetSummary() => "折りたたみ時の一行サマリー";
}
```

**新しいオブジェクト系エフェクトを追加する際は、`_FX_MYEFFECT` キーワードの分岐処理を
`Shaders/ShaderFXUber.shader`（3D）と `Shaders/ShaderFXUberSprite.shader`（2D）の**両方**に
追加してください。** `EffectModule.ApplyTo(Material)` はプロパティ名で値を書き込むだけなので、
両方のシェーダーが同じプロパティ名・キーワード名を実装している限り、C#側のコードは1つのままで
2D/3D両対応になります。片方だけに実装すると、そのエフェクトはもう一方のレンダラー種別では
（コンパイルエラーにはならず）何も起きないだけの状態になるので気づきにくく、注意してください。

## パフォーマンス検証（重要）

`Tools > ShaderFX > Spawn SRP Batcher Benchmark (1024 Cubes)` を実行すると、Project ウィンドウで選択中の
EffectProfile を使う 1024 個のオブジェクトが生成されます。Play モードに入り `Window > Analysis > Frame Debugger`
を開いて、バッチ数がまとまっていることを確認してください（= 同一マテリアルキャッシュと SRP Batcher が正しく
効いている証拠です）。このアーキテクチャの検証ポイントです。

## グループ一括適用（フェーズ2）

`EffectTarget` の `Group`（Flags）を設定しておくと、実行時にまとめて演出を差し替えられます。
`EffectGroup` の中身（Player / Enemy / Stage / Pickup / UI3D）はプロジェクトごとに書き換えてください。

```csharp
EffectDirector.Instance.ApplyToGroup(EffectGroup.Enemy, hitFlashProfile); // 敵全員を白フラッシュ
EffectDirector.Instance.RemoveFromGroup(EffectGroup.Enemy);              // 各オブジェクト本来の Profile に戻す
```

`ApplyToGroup` は各オブジェクトが個別に持つ Profile を上書きする一時的なオーバーレイです。`RemoveFromGroup` で
Group 指定分の上書きだけを解除し、オブジェクト本来の Profile 表示に戻ります。

## タグでの一括適用（EffectTarget が付いていないオブジェクトにも効く）

Unity 標準のタグ経由なら、`EffectTarget` を付けていない（ゼロコンポーネントの）オブジェクトにも一括適用できます。

```csharp
EffectDirector.Instance.ApplyToTag("Enemy", dissolveProfile);
EffectDirector.Instance.RemoveFromTag("Enemy");
```

`Instantiate` した直後のオブジェクトにも適用したい場合は、生成直後に一度呼んでください（`EffectTarget` が付いた
オブジェクトは `OnEnable` で自動登録されるため不要です）。

```csharp
var enemy = Instantiate(enemyPrefab);
EffectDirector.Instance.NotifySpawned(enemy);
```

`ApplyToTag` で登録したタグは Director に保持され、Additive シーンロード時にも新しく読み込まれたオブジェクトへ
自動的に再適用されます。存在しないタグ名を渡した場合はコンソールに警告が出るだけで例外にはなりません
（タグは文字列でタイポに弱いため、疑わしい時はまずコンソールを確認してください）。

## 画面系エフェクト（フェーズ3）

Grayscale / Posterize / Pixelate / Outline は他のモジュールと同じく `EffectProfile` の「+ Add Effect」から
追加します（カテゴリは「画面系」）。**どれか1つでも有効な画面系モジュールを持つ `EffectTarget` が登録されていれば
自動的に画面全体へ適用されます** — 見た目を持たせたいオブジェクト自身が対象である必要はありません。

`ScreenFXFeature` は `Assets/Settings/PC_Renderer.asset` と `Mobile_Renderer.asset` の両方に、この
パッケージのセットアップ時点で自動登録済みです。何もしなくてもすぐ使えます。

複数の `EffectTarget` が異なる画面系設定を持つ場合、最後に処理されたものが勝ちます（画面系エフェクトは
本質的に「シーン全体でひとつ」の設定のため）。演出用に複数の Profile を同時運用する場合はご注意ください。

### グループ限定の画面系エフェクト（「敵だけドット絵化」）

`PixelateModule` は `restrictToGroup` を有効にすると、指定した `EffectGroup` に属するオブジェクトだけを
ピクセル化できます。対象にするには、事前に `EffectDirector.SetGroupRenderingLayer` でその Group に
URP レンダリングレイヤーのビットを割り当てておく必要があります。

```csharp
EffectDirector.Instance.SetGroupRenderingLayer(EffectGroup.Enemy, 3); // 起動時に1回

var pixelate = new PixelateModule { blockSize = 16f, restrictToGroup = true, group = EffectGroup.Enemy };
```

内部的には、割り当てたレンダリングレイヤーに属するオブジェクトだけをオフスクリーンのマスクバッファへ
描画し、Pixelate パスがそのマスクを見てピクセル化するかどうかをピクセル単位で切り替えています。
`renderingLayerMask` は URP の Light Layers とビットを共有するため、ライティング用途で既に Light Layers を
使っているプロジェクトでは、割り当てるビットが競合しないよう注意してください。

### 独自シェーダーで Outline を使う場合の注意

`Outline` モジュールは URP の深度・法線プリパスを利用します。`ShaderFXUber.shader` には対応する
`"LightMode"="DepthNormals"` パスを実装済みですが、**別の独自シェーダーを使うオブジェクトには
このパスがない場合、そのオブジェクトだけ Outline の輪郭検出から漏れます**（見えなくなるわけではなく、
輪郭線が付かないだけです）。自前のシェーダーで Outline に対応させたい場合は、同様の `DepthNormals` パスを
追加してください。

## 2D対応

`SpriteRenderer` を使う2Dオブジェクトにも、3Dと同じ `EffectProfile`/`EffectTarget` の仕組みで
オブジェクト系エフェクト（RimLight/Dissolve/HitFlash/EmissionControl）を適用できます。

**使い方は3Dと1点だけ違います:** マテリアルに `ShaderFX/Uber` の代わりに **`ShaderFX/Uber Sprite`**
シェーダーを使ってください。それ以外（Profile作成、`EffectTarget` の追加、Tween/Timeline/Volume/
オーバーレイ、GPU Instancing対応の個体差パラメータなど）はすべて3Dと完全に同じ手順・同じコードで
動きます。`EffectDirector` 以下の仕組みは「Materialのプロパティ名で値を読み書きする」だけで、
2D/3Dのどちらかを判定するコードは一切持っていないためです。

```
1. Sprite用のMaterialを作成し、Shaderに ShaderFX/Uber Sprite を選択
2. SpriteRenderer にそのMaterialを設定
3. 同じGameObjectに EffectTarget を追加し、Profileを割り当て
```

### 2Dならではの近似について

`ShaderFXUberSprite.shader` は3D版と同じ4つの効果を実装していますが、内部の計算方法は2D向けに
置き換えています。

- **RimLight** — 3Dでは「法線とカメラ方向の角度」で光る範囲を決めますが、平らなスプライトには
  意味のある法線がないため、代わりに**スプライトのシルエット境界（不透明→透明に変わる部分）**を
  検出して光らせています。見た目上は「縁が光る」という同じ効果になります。
- **Dissolve** — ワールド座標を使ったノイズでアルファを削る仕組みは3Dと同じです。
- **HitFlash / EmissionControl** — 3Dと全く同じ計算です。

### 対象外（スコープ外）にしていること

- **影を落としません**（ShadowCasterパス未実装）
- **画面系のOutlineエフェクトの輪郭検出対象になりません**（DepthNormalsパス未実装。深度・法線情報が
  必要なため）。Grayscale/Posterize/Pixelateはカメラの絵全体にかかる後処理なので、2D/3D問わず
  普通に効きます
- スプライトアトラスの特殊な設定（ETC1の外部アルファ分離、PixelSnapなど）には個別対応していません
- URPの2D専用ライティング（`Light2D` によるノーマルマップ付き陰影など）とは連携していません

### Volume型空間適用も2D物理に対応済み

`ShaderFXVolume`（前述の「追加機能」章）は `Collider`/`Rigidbody`（3D物理）と
`Collider2D`/`Rigidbody2D`（2D物理）の両方に対応しています。2Dプロジェクトでは
`Collider2D` を使ったトリガーを用意するだけで、追加の設定なしに同じコンポーネントが動作します。

実機検証: `SpriteRenderer` + `ShaderFX/Uber Sprite` のオブジェクトでRimLight・Dissolveそれぞれの
見た目を実際にスクリーンショットで確認しました。特にRimLightの実装では、当初 `_MainTex_TexelSize`
（テクスチャ1ピクセル分のUVサイズを自動計算するUnity標準の仕組み）を使っていたところ、
`SpriteRenderer` はテクスチャをMaterial本体ではなく描画のたびにMaterialPropertyBlock経由で渡すため、
この自動計算値が更新されず固定値のままになり、RimLightが正しく機能しないバグを実機で発見しました。
`fwidth()`（画面空間でのUV変化量を返す、テクスチャに依存しないHLSL標準機能）を使う方式に直してから、
再度スクリーンショットで正しく光ることを確認しています。`ShaderFXVolume` の2D物理対応についても、
`OnTriggerEnter2D`/`OnTriggerExit2D` を直接呼び出し、3D版と同じくオーバーレイの適用・解除が正しく
動くことを確認済みです。

## Edit Mode ライブプレビュー（フェーズ4）

Play を押さなくても、Profile を割り当てた時点で Scene view / Game view に即座に反映されます。挙動は Play Mode と
ほぼ同じで、`EffectDirector` は Edit Mode 中は Hierarchy に表示されない一時オブジェクトとして自動生成されます。

- **Undo/Redo** — 通常の Inspector 編集と同様に対応済みです。
- **Prefab オーバーライド** — Prefab インスタンスごとに `Profile`/`Group` を個別に上書きできます。
- **Preset** — `EffectTarget`/`EffectProfile` とも標準の Preset 機能で使えます（特別な対応は不要）。
- **保存時の安全策** — Edit Mode プレビューはレンダラーの `sharedMaterial` を実際に差し替えて実現しているため、
  そのままシーン/Prefab を保存すると「アセットとして存在しない一時マテリアル」への参照が書き込まれてしまう
  リスクがあります。`EditModeSaveSafety` が保存の直前に元のマテリアルへ戻し、保存直後に再適用することで、
  ディスク上のファイルには常に正規のマテリアル参照だけが残るようにしています（シーン保存・Prefab保存・
  スクリプト再コンパイルのすべてに対応）。

### プレハブは常に同じ見た目になります

`EffectTarget` が覚えている「元のマテリアル」(`originalMaterial`) は、コンポーネント追加時に一度だけ確定する
専用フィールドで、Edit Mode プレビュー中に実際に貼り替わっているレンダラーの見た目とは完全に切り離しています。
そのため、Edit Mode でプレビュー中のオブジェクトをそのままドラッグ&ドロップで新規プレハブ化しても、
「プレビュー中の一時的な見た目」が焼き込まれることはありません。同じ Prefab から出したインスタンスは、
常にそのとき点で Profile が定義している見た目に揃います（`PrefabUtility.SaveAsPrefabAsset` 等スクリプト経由の
プレハブ化も含めて動作確認済みです）。見た目を意図的に個体差させたい場合は、Prefab インスタンスごとに
別の `EffectProfile` を割り当ててください。

## マテリアルキャッシュの参照カウント管理（フェーズ5）

`EffectDirector` は「同じ元マテリアル × 同じ Profile の内容」の組み合わせごとに1つだけキャッシュ Material を
作り、使う側が増減してもその Material インスタンス自体は使う人が0人になった瞬間に自動で破棄されます
（参照カウント方式）。何百・何千体のオブジェクトが同じ Profile を共有していても、実際に GPU 上に存在する
Material のバリエーション数は Profile の種類数と一致し、SRP Batcher が効き続けます。利用側が意識すべきことは
特にありません（`EffectTarget` の Profile を差し替えたり、オブジェクトを破棄したりするだけで自動的に管理されます）。

## 個体差パラメータ用 API（フェーズ5・GPUインスタンシング経路）

「同じ Profile を使う敵の集団のうち、1体だけ被弾フラッシュ中」のような個体差を出したい場合、
Profile 自体を個別に分けるとキャッシュ Material が増えて SRP Batcher の効きが弱まります。そのかわり
`EffectTarget` に `MaterialPropertyBlock` 経由の軽量 API を用意しています。

```csharp
effectTarget.SetInstanceHitFlashAmount(0.8f); // この1体だけフラッシュを強める
effectTarget.SetInstanceDissolveAmount(0.5f); // この1体だけ溶解を進める
effectTarget.SetInstanceFloat("_MyParam", 1f); // 任意のfloatプロパティ
effectTarget.SetInstanceColor("_MyColor", Color.red);
effectTarget.ClearInstanceOverrides();          // 個体差を解除してProfile本来の値に戻す
```

**`_DissolveAmount` / `_HitFlashAmount` は GPU Instancing でバッチされます。** `ShaderFXUber.shader` では
この2プロパティだけを通常の `CBUFFER_START(UnityPerMaterial)` ではなく `UNITY_INSTANCING_BUFFER` に
宣言しており、`EffectDirector` が生成するキャッシュ Material は常に `enableInstancing = true` です。
そのため `SetInstanceDissolveAmount`/`SetInstanceHitFlashAmount` で書き込んだ `MaterialPropertyBlock` の
値は、同じキャッシュ Material を共有する何体分でも **1回のGPUインスタンス描画にまとめられます**。
SRP Batcher の対象からは外れますが（一度でも `MaterialPropertyBlock` を持つとURPの仕様上そうなります）、
GPU Instancingがそのぶんを引き受けるため、「同じProfileの敵100体のうち10体だけ溶解が進んでいる」ような
状況でも描画コストがオブジェクト数に比例して増えることはありません。5体のCubeにそれぞれ異なる
`SetInstanceDissolveAmount`(0 / 0.15 / 0.3 / 0.45 / 0.6)を設定し、全員が同一キャッシュMaterialを共有した
まま個別の溶解量で正しく描画されることを実機で確認済みです。

**それ以外の任意プロパティ（`SetInstanceFloat`/`SetInstanceColor` に独自のプロパティ名を渡した場合）は
GPU Instancing対象ではありません。** シェーダー側でそのプロパティを `UNITY_INSTANCING_BUFFER` に追加
宣言しない限り、通常のMaterialPropertyBlock経由（1レンダラー=1ドローコール）になります。常時アニメーション
させる少数のオブジェクトに絞って使うか、多用したい場合は該当プロパティもシェーダー側でインスタンシング対応
させてください。

**注意:** 対象の `EffectModule`（例: `HitFlashModule`）が Profile 側で有効になっている必要があります
（このAPIは値を上書きするだけで、エフェクトのON/OFF自体は切り替えません）。

**計測環境についての注意:** GPU Instancingで実際にバッチされていること自体は、上記の値の伝搬と
同一Material共有の両方が正しく機能していることから設計上確実ですが、実際のバッチ数（Frame Debugger上の
Instance数）は、他のフェーズ5の実測値と同様の理由（MCP自動操作下でGame Viewが実際にレンダリングされない）
により本環境では数値として取得できませんでした。`Window > Analysis > Frame Debugger` を開いた状態で、
複数体に異なる `SetInstanceDissolveAmount` を設定し、該当ドローコールの「Instances」欄が1より大きいことを
お手元でご確認ください。

## シェーダーバリアントストリッピング（フェーズ5・任意）

`Create > ShaderFX > Build Settings (Variant Stripping)` で `ShaderFXBuildSettings` アセットを作成し、
`enableVariantStripping` を有効にすると、ビルド時に実際どの `EffectProfile` アセットからも使われていない
キーワードの組み合わせ（シェーダーバリアント）をビルドから除外します。既定は無効（オフ）です。デフォルトの
Unity 標準ストリッピングだけで十分な場合や、動的にキーワードを組み合わせる特殊な使い方をする場合は
有効にしないでください。実プロジェクトの `EffectProfile` 1個構成で検証したところ、`Windows64` ビルドで
`ShaderFX/Uber` シェーダーのバリアント数が「スクリプタブルストリッピング後: 2」まで絞り込まれることを
実際のビルドログで確認済みです。

## 負荷テストサンプル（フェーズ5）

Package Manager の `MS2026 ShaderFX` パッケージ → `Samples` タブから `Stress Test (1024 Objects, All Effects)`
をインポートすると、`Tools > ShaderFX > Samples > Spawn Full Stress Test (1024 Objects, All Effects)`
メニューが追加されます。実行すると、オブジェクト系4種（RimLight/Dissolve/HitFlash/EmissionControl）を
均等に配分した1024個のCubeと、画面系エフェクト（Posterize）を持つ1個のオブジェクトが生成されます。

**実際に検証した内容:**

- 生成直後、シーン内の `EffectTarget` 登録数は1026件（1024体 + 画面系キャリア1体 + 既存の DemoCube）に対し、
  `EffectDirector` が実際にキャッシュした Material は **わずか5個**（Profile の種類数と一致）でした。
  1000個を超えるオブジェクトを動かしても SRP Batcher が扱う Material バリエーションが増えないことを、
  実際のシーンで確認しています。
- Play モードへの出入りを繰り返しても、この登録・キャッシュ状態は壊れないことを確認済みです
  （後述のバグ修正により、フェーズ5の検証中に見つかった問題を解消しています）。
- **計測環境についての注意:** フレームタイム・Draw Call数などの数値は、Unity Editor を実際に前面表示した
  状態でのみ正確に取得できます。本ツールの検証は MCP 経由の自動操作で行っており、Editor ウィンドウが
  OS上でフォーカスされていない状態が長く続くため、Profiler のカウンタ類は信頼できる実測値を得られません
  でした（Editor がバックグラウンド時に描画レートを絞る仕様のため）。実際のフレームタイムは、
  `Window > Analysis > Profiler` または `Frame Debugger` を開いた状態で、お手元の環境で直接確認してください。
  確認すべきポイントは「Batches」欄がオブジェクト数（1024+）ではなく Profile の種類数近辺に収まっているか、
  および SRP Batcher の互換性表示です。
- 確認が終わったら `ShaderFX_StressTest` を Hierarchy から削除してください（サンプルスクリプトの
  メニュー実行時ログにも案内が出ます）。

### 検証中に見つけて修正したバグ：階層一括削除時の登録ゴースト化

負荷テストの検証中、`ShaderFX_StressTest`（1025個の子オブジェクトを持つ親）をまとめて削除したところ、
`EffectDirector` の内部登録・Material キャッシュが解放されないまま残り続ける不具合を発見しました。

原因は Unity の仕様です。**「親ごと `DestroyImmediate` で削除された子オブジェクト」は、個別に削除された
場合と異なり `OnDisable` が呼ばれません**（実機で検証済み）。`EffectTarget` は当初 `OnDisable` でのみ
`EffectDirector` への登録解除を行っていたため、階層をまとめて削除するケース（このサンプルに限らず、
実際のゲームで敵グループやエフェクトの一群をまとめて破棄するケース全般）で登録が残り続けていました。

対策として、`EffectDirector` 側に「登録済みリストの中で既に破棄済み（Unityのfake-null）になっている
エントリを見つけたら、その場でキャッシュ参照ごと解放する」自己修復処理 (`PruneStaleRegistrations`) を追加し、
登録内容が変化するたびに毎回自動実行されるようにしました。Editor 専用の仕組みではなく Runtime 側の実装なので、
実機ビルドでも同様に自己修復します。実際に1024体の親ごと削除 → 通常操作を1回行う、という手順で
登録数が1026件から正しく1件まで戻ることを確認済みです。

## 追加機能（計画書§5より）

計画書§5「追加機能・高度化の提案」のうち、優先度高の4項目全部と、優先度中3項目（Addressables対応を除く）を
実装しています。優先度低の3項目（HDRP対応・VFX Graph連携・ノードエディタ化）は計画書自身が
「工数が大きいわりに恩恵が限定的」「チームが大きくなってから」としている項目のため、今回は対象外です。

### エフェクトのブレンド/優先度（オーバーレイ）

複数の Profile が同一オブジェクトに重なった時の合成ルールです。「常時トゥーン(RimLight) + 一時的な
被弾フラッシュ(HitFlash)」のように、オブジェクト本来の Profile を失わずに一時的な効果だけ重ねられます。
このパッケージの他の新機能（Volume型空間適用など）はすべてこの仕組みの上に実装されています。

```csharp
EffectDirector.Instance.PushOverlay(target, "HitFlash", hitFlashProfile, priority: 10);
EffectDirector.Instance.PopOverlay(target, "HitFlash");
```

`layer`（上の例では `"HitFlash"`）は呼び出し側が決める名前です。同じレイヤー名で再度 `PushOverlay` すると、
古い Profile/優先度を置き換えます（同じ被弾フラッシュを連続で当てる時にそのまま呼び直せます）。複数の
オーバーレイが同時に有効な場合、**モジュールの種類ごとに優先度が高いオーバーレイが勝ちます**（例:
RimLightは触らず、HitFlashだけ上書き）。ベース Profile に無いモジュール種類をオーバーレイが持っていれば、
そのぶんは単純に追加されます。

実機検証: 2体のCubeに同じベース Profile（RimLightのみ）を設定した状態から、1体だけに HitFlash のみを
持つオーバーレイを追加。オーバーレイを追加した側だけ RimLight+HitFlash 両方のキーワードを持つ**別の**
キャッシュ Material に切り替わり、もう1体はそのまま元の共有 Material に残ることを確認。オーバーレイを
外すと、元と完全に同じ内容のキャッシュ Material（新しいインスタンスとして再生成されるケースあり—
参照カウントが0になれば古いインスタンスは破棄されるため）に戻ることを確認済みです。

### ランタイムTween API

被弾フラッシュのフェード、消滅時のディゾルブなど「値の時間変化」を1行で書けるAPIです。内部では
個体差パラメータ経路（`SetInstanceFloat`/`SetInstanceColor`、GPU Instancing対応）を使うため、
`_DissolveAmount`/`_HitFlashAmount` をアニメーションさせても前述の GPU Instancing の恩恵はそのままです。

```csharp
effectTarget.PlayDissolve(0f, 1f, 0.5f, ShaderFXEase.OutQuad);      // 0.5秒かけて溶解
effectTarget.PlayHitFlash(1f, 0f, 0.2f, ShaderFXEase.OutQuad, () => Debug.Log("フラッシュ完了"));
effectTarget.PlayFloat("_MyParam", 0f, 1f, 0.3f);                    // 任意のプロパティにも使えます
effectTarget.StopTween("_DissolveAmount");                           // 特定のTweenだけ止める
effectTarget.StopAllTweens();
```

内部は `Coroutine` 駆動で、Tween中のオブジェクトだけが毎フレームコストを払います（他の大多数の
「静止しているオブジェクト」には一切コストがかかりません）。`duration` に `0` 以下を渡すと即座に `to` の値へ
ジャンプし、コールバックもその場で呼ばれます。同じプロパティに対して再度 `Play〜` を呼ぶと、進行中の
Tween は自動的にキャンセルされて新しい方に置き換わります。

実機検証: Dissolveモジュールを持つCubeで `PlayDissolve(0,1,duration)` を実行し、瞬間完了パス
（`duration=0`）で正しい値・コールバック発火・内部管理リストからの削除を確認。同一プロパティへの
再生成（キャンセル→再登録）で内部の追跡数が2重登録されないことも確認済みです。なお実際の経過時間に
沿ったフレームごとの値の遷移については、本自動化環境ではUnityのフレームループが実時間通りに進行しない
という制約があり検証できていません（実機・実際のGame Viewを操作した状態でご確認ください）。

### Timeline統合（カスタムトラック）

`com.unity.timeline` がインストールされている場合のみ、`ShaderFXParameterTrack` という専用トラックが
使えるようになります。未インストールのプロジェクトでは、このトラック関連のアセンブリ
（`MS2026.ShaderFX.Timeline`）だけがコンパイル対象から外れ、パッケージ本体には影響しません
（`Unity.Timeline` への依存をこのアセンブリだけに閉じ込めているためです）。

使い方: Timeline ウィンドウでトラックを追加 → `ShaderFXParameterTrack` を選択 → `EffectTarget` を
バインド → クリップを追加し、Inspector で `parameter`（Dissolve/HitFlash/Custom）と `fromValue`/
`toValue`/`curve` を調整します。Tween API と同じく個体差パラメータ経路を使うため、GPU Instancingの
恩恵も同様に受けられます。

実機検証: `TimelineAsset` + `ShaderFXParameterTrack` + `PlayableDirector` をスクリプトから直接組み立て、
`director.time` を設定して `Evaluate()` した際に、クリップの `AnimationCurve` に沿った正しい補間値が
`EffectTarget` へ反映されることを確認済みです（例: 2秒クリップの80%地点で `Mathf.LerpUnclamped(0,1,0.8)`
と一致する `0.8` を確認）。なお、同一の `PlayableDirector` を使い回して時刻を何度も行き来させる形の検証は、
本自動化環境ではPlayableGraphの状態管理が実際のTimelineウィンドウのスクラブ操作と異なる挙動を示したため、
Timelineウィンドウでの実際のスクラブ再生についてはお手元でご確認ください。

### Volume型の空間適用

URPのVolumeのような「このエリアに入ったオブジェクトに効果がかかる」空間トリガーです。内部はオーバーレイ
機構（上述）そのもので、`Collider`/`Collider2D`（`isTrigger = true`）に入った/出た `EffectTarget` へ
自動的に `PushOverlay`/`PopOverlay` を呼びます。**3D物理・2D物理の両方に対応**しており、プロジェクトが
`Collider`/`Rigidbody`（3D）と `Collider2D`/`Rigidbody2D`（2D）のどちらを使っていても、同じ
`ShaderFXVolume` コンポーネントをそのまま使えます（詳しくは後述の「2D対応」章）。

```
1. GameObjectにCollider（3D）またはCollider2D（2D）をTrigger有効で追加
2. ShaderFXVolume コンポーネントを追加（Reset時に isTrigger が自動でONになります）
3. overlayProfile にこのエリア用のProfile（屈折・氷結など）を設定
```

Unityの仕様上、トリガー判定には少なくとも片方のオブジェクトに `Rigidbody`/`Rigidbody2D` が必要です
（本コンポーネント側で回避はできません）。同じGameObjectのオブジェクトが破棄された際に `OnTriggerExit` が確実に呼ばれるとは
限りませんが、`EffectDirector` 側の自己修復処理（`PruneStaleRegistrations`）が対象の登録解除と同時に
オーバーレイも解放するため、リークはしません。

実機検証: Trigger の `OnTriggerEnter`/`OnTriggerExit` を直接呼び出し、エリア進入時にベース Profile
（RimLight）とオーバーレイ Profile（EmissionControl）の両方が有効なキーワードで描画され、退出時に
元の見た目（同一キーワード構成）へ戻ることを確認済みです。

### 品質スケーリング

`ShaderFXQuality.ScreenEffectResolutionScale` に応じて、画面系エフェクト（Grayscale/Posterize/
Pixelate/Outline）のチェーン全体を縮小解像度で処理し、最後に1回だけ元解像度へアップスケールします。
既定ではモバイルプラットフォーム、または Quality Level 名に "Mobile"/"Low" を含む場合に半解像度になります
（それ以外は等倍のまま、追加コストはゼロです）。

```csharp
ShaderFXQuality.ResolutionScaleOverride = 0.5f; // 明示的に固定したい場合
ShaderFXQuality.ResolutionScaleOverride = null;  // 自動判定に戻す
```

自動判定はQuality Level名に依存するヒューリスティックです。プロジェクトの命名規則に合わない場合は
`ResolutionScaleOverride` で明示的に指定してください。

実機検証: `ResolutionScaleOverride` を `0.25` に強制し、Posterize(4段階)を適用したオブジェクトを
Scene view でスクリーンショット比較。等倍時はくっきりした色の境界線が、縮小→拡大処理を通すと
バイリニアアップスケールによる境界のぼやけとして視覚的に確認でき、縮小・処理・拡大のパイプライン全体が
正しく機能していることを確認しました。

### デバッグオーバーレイ

`ShaderFXDebugOverlay` コンポーネントをシーン内の任意のGameObjectに追加すると、Game view の左上
（`anchor` で変更可）に以下を常時表示します。`EffectDirector` のように自動生成はされません
（デバッグ用ツールを意図せずビルドに含めないため、明示的に追加する方式です）。

- 登録 `EffectTarget` 数
- キャッシュ Material 数（≒ バッチ数の目安。同じ Profile を共有するオブジェクトが何体でもこの数は増えません）
- 個体差オーバーライド（`MaterialPropertyBlock`）中のオブジェクト数（この数だけ個別描画コストがかかっています）

実機検証: 実際に Play Mode の Game view スクリーンショットでオーバーレイの表示内容を確認し、
「登録 EffectTarget 数: 1 / キャッシュ Material 数: 1 / 個体差オーバーライド中: 0」が実際のシーン状態と
一致することを確認済みです。

### プリセットライブラリ

プリセット(複数モジュールの組み合わせ)を使う方法は2通りあります。

**① 「+ Add Effect」から直接追加(推奨)** — `EffectProfile` Inspectorの「+ Add Effect」を押すと、
通常の1エフェクトずつの項目に加えて `プリセット/オブジェクト系/...` `プリセット/画面系/...` という
サブメニューが出ます。選ぶと、そのプリセットを構成する全モジュール(1〜3個)が今編集中のProfileへ
まとめて追加されます。プリセットは「完成形」ではなく「出発点」で、追加した後もそのまま個別に
モジュールを足したり値を調整したりできます。1つのProfileに複数のプリセットを重ねて使うことも可能です。
定義は `Editor/EffectPresetLibrary.cs` にあり、プロジェクト固有のプリセットを増やしたい場合はここへ
追記してください。

現在オブジェクト系13種・画面系8種、計21種類のプリセットがあります。

| オブジェクト系 | 構成モジュール |
|---|---|
| アニメ調キャラ標準 | RimLight(控えめ) |
| 聖なる守護者 | RimLight(金) + EmissionControl(金パルス) |
| ホログラム敵 | Hologram |
| サイバーステルス | Hologram(緑) + Glitch(弱) |
| 氷結 | Frost |
| 呪われたアンデッド | Frost(緑がかった氷結) + Dissolve(わずかに欠けた状態) |
| 発火 | Fire |
| 炎の魔物 | Fire(強) + EmissionControl(常時発光) |
| パワーアップ状態 | RimLight(強) + EmissionControl(強パルス) + UVScroll(エネルギーライン) |
| トゥーン標準 | ToonShading(3段階) |
| ディゾルブ消滅 | Dissolve(`amount=0`、Tween/Timelineで`1`へ) |
| 被弾フラッシュ | HitFlash(`amount=0`、Tween/Timelineで`1`へ) |
| グリッチ故障 | Glitch(`amount=0`、Tween/Timelineで発生タイミングを制御) |

| 画面系 | 構成モジュール |
|---|---|
| 回想シーン(モノクロ) | Grayscale |
| レトロゲーム風 | Posterize + Pixelate |
| ドット絵フィルター | Pixelate |
| コミック風輪郭線 | Outline |
| 被弾赤フラッシュ | ScreenFlash(赤、`amount=0`) |
| 回復白フラッシュ | ScreenFlash(白、`amount=0`) |
| 衝撃波(ボス登場) | Shockwave(`progress=0`、Tween/Timelineで広げる) |
| 気絶・暗転演出 | ScreenFlash(黒) + Grayscale |

**② Package Manager Samplesから完成アセットとしてインポート** — `MS2026 ShaderFX` パッケージ →
`Samples` タブ → `Preset Library` をインポートすると、上記オブジェクト系の代表的な8種
（アニメ調キャラ標準・ホログラム敵・氷結・発火・グリッチ故障・トゥーン標準・ディゾルブ消滅・
被弾フラッシュ）が独立した `EffectProfile` アセットとして手に入ります。同じ完成Profileを
複数のオブジェクトで使い回したい場合はこちらが便利です。

## 追加エフェクト（高品質化・ジャンル拡充）

「感動するレベルのエフェクトを2D/3D問わず簡単に追加・調整できること」を目標に、ファンタジー/RPG系・
SF/サイバー系・汎用の3方向で、オブジェクト系6種・画面系2種を追加しました（キャラクター本体の効果を
優先的に充実させています）。既存の4種（RimLight/Dissolve/HitFlash/EmissionControl）と同じく、
`EffectProfile` の「+ Add Effect」からそのまま追加でき、2D/3Dどちらのシェーダーでも
（ToonShadingを除き）同じように動きます。

### オブジェクト系

| モジュール | 系統 | 効果 |
|---|---|---|
| `FireModule`（発火） | ファンタジー/RPG | 2オクターブのノイズで揺らめく、上昇する炎のような加算グロー |
| `FrostModule`（氷結） | ファンタジー/RPG | 彩度低下+青みブレンド+ノイズ閾値によるきらめきハイライト |
| `HologramModule`（ホログラム） | SF/サイバー | フレネル発光+走査線(スキャンライン)+明滅フリッカー |
| `GlitchModule`（グリッチ） | SF/サイバー | ブロック単位のUVジッター+RGBチャンネル分離 |
| `UVScrollModule`（UVスクロール） | 汎用 | 指定方向に流れる筋状の加算グロー(溶岩・魔法陣・配線など) |
| `ToonShadingModule`（トゥーン影） | 汎用・3D専用 | メインライトの陰影を段階的に量子化するセルシェーディング |

**実機検証で見つけて修正した本物のバグ:** FireとUVScrollは当初、単純な加算合成（`color += 効果色 * 強さ`）
で実装していましたが、すでに明るくライティングされた面の上にさらに加算すると、各色チャンネルが1.0で
クリップして白く飛んでしまい、狙った色がほとんど見えなくなる不具合を実機のスクリーンショットで発見しました
（Fireが「炎」ではなく薄いピンク色にしか見えませんでした）。単純加算ではなく効果色へ`lerp`で寄せる方式に
直し、実際に炎らしい揺らめきが見えることを確認しています。また、既定のノイズスケール／パターンスケールが
小さすぎて、1ユニット程度の小さいオブジェクトでは模様がほとんど見えないことも発見し、既定値を引き上げて
います（極端に大きい・小さいオブジェクトで使う場合は、ノイズスケール系のパラメータを調整してください）。
UVScrollは当初ノイズをそのままサンプリングしており「筋」ではなく「まだら模様」に見える問題もあったため、
`_UVScrollDirection` に沿った軸でノイズを引き伸ばすよう修正し、指向性のある筋模様になることを確認しました。

Glitchのブロックずれ・RGBチャンネル分離は、模様のある(テクスチャ付きの)表面でないと目に見えて確認できません
（単色ベタ塗りのマテリアルには、ずらす模様自体が存在しないため）。チェッカー柄テクスチャで実際に赤/青の
色ズレが起きることを確認済みです。

`ToonShadingModule` は3D専用です。`ShaderFXUberSprite.shader`（2D）は現状アンリット(光源計算をしない)
描画のため、量子化する対象の陰影の勾配がそもそも存在せず、2Dスプライトに割り当てても見た目は変化しません
（エラーにはなりません）。実機のスフィアで3段階のくっきりしたアニメ調陰影が出ることを確認済みです。

### 画面系

| モジュール | 効果 |
|---|---|
| `ShockwaveModule`（衝撃波） | 画面上の指定点から輪状に広がるディストーション。`progress` を0→1へアニメーションさせて「輪が広がる」演出にします |
| `ScreenFlashModule`（画面フラッシュ） | 画面全体を指定色でフラッシュ。被弾時の赤フラッシュなど |

実機検証: Shockwaveは実際にDemoCubeを画面に映した状態で輪状の歪みが視覚的にはっきり確認でき、
ScreenFlashは画面全体が指定色（テストでは赤）に染まることをスクリーンショットで確認済みです。

いずれもURPのVolume標準機能（Vignette/Chromatic Aberration/Bloom等）とは意図的に住み分けています。
Volume標準機能は「常時かかっているカメラ全体の絵作り」向けであるのに対し、このパッケージの画面系は
`EffectProfile`/`EffectTarget` 経由でゲームプレイのイベントに紐付けて発生させる用途（衝撃波・フラッシュ
など、特定の瞬間だけ効かせたい演出）を主眼にしています。カメラの絵作り自体を変えたいだけなら、
標準のURP Volumeオーバーライドを使うほうがシンプルです。

## 既知の制約

- 画面系エフェクトは同時に1設定のみ（複数 Profile が競合した場合は後勝ち。ただしオーバーレイによる
  モジュール種類単位のマージはオブジェクト系・画面系どちらにも効きます）
- シェーダーは Shader Graph ではなく HLSL 手書きで実装（Editor 自動操作環境での再現性を優先した判断）
- Timeline統合は `com.unity.timeline` が入っているプロジェクトでのみコンパイルされます（専用アセンブリに
  隔離済みのため、未インストールでもパッケージ本体は問題なく動作します）
