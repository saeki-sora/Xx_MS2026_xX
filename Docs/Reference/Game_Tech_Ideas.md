# このゲームに使えそうな技術（知らないと思いつけない物の一覧）

- **何の資料か**: 「ラディアンスカスケードのように、知らないと使う発想にすらならない技術」を集め、このゲーム（握れ、灼ける前に）に合うかを評価した Claude 向けの一覧（2026-11-27。ユーザーに頼まれてウェブで調べた。ウェブはこのときだけ）。
- **読み方**: `[TOP]` おすすめ5つ ／ `[LIGHT]` 光と見た目 ／ `[MOTION]` 動き・物理 ／ `[INPUT]` 握る入力と音 ／ `[AI]` CPU プレイヤー ／ `[NET]` 記録・通信 ／ `[PERF]` 性能 ／ `[HAVE]` もう入っていて外した物 ／ `[SRC]` 出典。
- **決まりではない**: どれも未決定の候補。実装する前にユーザーに聞く（いくつか方法がある物は見本ページで見比べる）。ユーザー決定（メモリ・CLAUDE.md）が優先。見た目の物は `Beautiful_2D_Analysis.md` の P1〜P8 で確かめる。
- **更新**: ユーザーに頼まれたときだけ。
- 評価の記号: 合う度 ◎よく合う ○合う △条件付き ／ 手間 小=半日〜1日・中=2〜4日・大=1週間〜 ／ 重さ（PC の負担）軽い・ふつう・重め。

---

## おすすめ5つ（手間が小さく、効き目が大きい） [TOP]

| # | 技術 | 一言 | このゲームでの使い道 | 合う | 手間 | 重さ |
|---|---|---|---|---|---|---|
| 1 | 1€フィルター（One Euro Filter） | 動きが遅いときは強くならし、速いときは遅れなくする揺れ取り | 握力センサーの値。今は移動平均・メディアン・EMA（ならす強さが一定）なので「揺れを取ると遅れる」の板ばさみ。1€ならゆっくり握るとき震えず、ぐっと握った瞬間は遅れない | ◎ | 小 | 軽い |
| 2 | 試合の流れで色を変えるカラーグレーディング（LUT の切り替え） | 画面全体の色味を表（LUT）で決め、時間で混ぜて変える | ウェーブ・残り時間・ボス・終盤のピンチで色味を変える（P3）。URP に最初からある | ◎ | 小 | 軽い |
| 3 | 4色の色覚チェック（OKLCH で色をそろえる＋色覚シミュレーション＋形の手がかり） | 明るさ・あざやかさを数字でそろえた色の作り方と、色が見分けにくい人の見え方での確認 | プレイヤーは赤・青・黄・緑。赤と緑は色覚の多様性（日本の男性の約20人に1人）で見分けにくい組み合わせ。展示は大勢が遊ぶ。色だけに頼らず、マークや模様も足す | ◎ | 小 | なし |
| 4 | 密度マップ／影響マップ（Influence Map） | 「敵がどこにどれだけ集まっているか」を格子の絵にしたもの | CPU プレイヤー（未実装）が撃つ場所・スキルを使う時を選ぶ材料。群れの音の厚み、HUD の危険表示にも使い回せる。群れの位置の格子（SwarmSpatialGrid）がもうあるので安い | ◎ | 中 | 軽い |
| 5 | 握りを音で返す（ソニフィケーション＋BGM の縦の重ね） | 握る強さに合わせて音の高さ・厚みを変える／危なさに合わせて BGM の楽器を重ねる | 展示はキーボードがなく握るだけ。握った手ごたえを耳でも返す。ピンチ・ボスで BGM が厚くなる | ○ | 中 | 軽い |

---

## 光と見た目 [LIGHT]

- **ラディアンスカスケード（Radiance Cascades）** ◎/大/ふつう — 床に落ちる光をまとめて計算し、障害物で自然に止まる。光る物がいくつあっても重さがほぼ同じ。URP 用の作例が GitHub にいくつかある（2D 版・Render Graph 版）。注意: 全部が光る（P8）・トゥーンの段。前の回答の詳細どおり。
- **ジャンプフラッド法（JFA）による距離場** ○/中/軽い — 「一番近い縁までの距離」を GPU で一気に作る。太さを自由に変えられる縁取り、光のにじみ、障害物にそって走る衝撃の輪、ラディアンスカスケードの土台に使える。群れ用の壁の距離場（SwarmWallField、CPU・格子）とは別物（こちらは画面の細かさで毎フレーム作れる）。
- **床のペイント（汚れを描きためるテクスチャ）** ○/中/軽い — 床全体を覆う1枚の絵に、菌が通った跡の汚れを描きため、レーザーや撃破で焼き消す。「どこまで守れたか」が床に残り、試合の流れで床の見た目が変わる（P3）。注意: 2026-11-04 の「見た目の強化では要素を足さない」とぶつかるかもしれないので先に聞く。敵より目立たせない（P1）。
- **2D の流体シミュレーション（Stable Fluids）** ○/中〜大/ふつう — 煙・泡・光の粒が、レーザーに押されて渦を巻き、障害物を回り込む。Unity での作例で GPU 0.3ms 前後。青の追尾バブル、光る泡の撃破演出、ボスの登場に。注意: 環境の粒は敵に見えないように（2026-11-04）。
- **2コマ打ち＋線のゆらぎ（Animate on Twos / Line Boil）** ○/小/軽い — 演出のアニメをわざと1秒12コマに落とし、縁の線を少しずつ揺らす。手描きっぽさ（P5）と止めのメリハリ（P6）。操作の反応は60コマのまま分ける。
- **ブルーノイズのディザ** ○/小/軽い — 細かくばらついた点の模様で、グラデーションの段々（バンディング）を消す。半透明の重なり順の問題をよける「点で抜く半透明」にも使える。障害物のシェーダー（StagePropEffects.hlsl）に一部ディザがあるので、まずそれを確かめる。
- **メタボール（画面上の密度＋しきい値）** △/中/ふつう — 敵が密な所で輪郭がくっつき、ぬめっとした1つの塊に見える。「群れは流体のように流れる」（2026-11-04）とは合う。注意: 1体ずつが読めなくなる（P1）。見本で確かめないと決められない。
- **Surface Cache GI（Unity 6.7 のプレビュー機能）** △/—/ふつう — Unity 公式の3D のリアルタイム間接光。今は Unity 6.3 なので版上げが要る。展示の前に版を上げるのは危ないので、今は見送りの候補。

## 動き・物理 [MOTION]

- **ソフトボディ（XPBD）・ヴァーレのひも（Verlet Rope）** ○/中/軽い — 点をばねでつないで、ぷるぷる・びよーんと動かす。ボスの舌の引っぱり合い（2026-11-26 決定の攻撃）の舌、巨大菌の体の揺れに。ステージ用のバネ（DampedSpring2D）はもうあるので、それを広げる形でもよい。

## 握る入力と音 [INPUT]

- **1€フィルター** → `[TOP]` 1。今のフィルター（GripFilterFactory）に1種類足すだけで、握力入力ブリッジの画面で今の物と切り替えて比べられる。
- **握りのソニフィケーション・BGM の縦の重ね** → `[TOP]` 5。BGM は作曲する人に「楽器ごとに分けた音」を用意してもらう必要がある。

## CPU プレイヤー [AI]

- **影響マップ** → `[TOP]` 4。
- **ユーティリティ AI（点数で選ぶ AI）** ○/中/軽い — 「どこを撃つ」「いつスキルを使う」を候補ごとに点数を付けて一番高い物を選ぶ。3段階の強さは点の付け方の重みを変えるだけで作れ、試合の中で学ぶ（2026-11-15 決定）のも重みを少しずつ動かせばよい。人間と同じ視界という決まりにも合わせやすい。

## 記録・通信 [NET]

- **写真からのリプレイ（スナップショットのためておき）** ○/中/軽い — Host が1秒30回配っている群衆の写真を最後の数秒分ためておけば、あとから同じ場面を好きな速さ・角度で再生できる。結果画面の名場面、撃破カメラの見せ直しに。決まった試合の終わりのカメラ（2026-11-19 決定、その場でズーム＋スロー）はそのままで、足すなら結果画面の側。

## 性能 [PERF]

- **動的解像度・品質の自動下げ（FrameTimingManager で GPU の時間を測る）** ○/小〜中/なし — 重くなったら描く細かさを少し下げ、軽くなったら戻す。展示会場の PC の性能差への保険。今は入っていない。

---

## もう入っていて外した物 [HAVE]

調べた中で、ソースにもうあったので候補から外した物（2026-11-27 時点）。

- 流れ場の経路探索（FlowFieldResult）・群れ用の壁の距離場（SwarmWallField）・GPU インスタンシングで群れを描く（SwarmRenderer）
- 握力のキャリブレーション（GripCalibrationProfile）・握った／離したの判定のヒステリシス（GripEdgeDetector）・移動平均／メディアン／EMA のフィルター
- 同時に鳴る音の数の上限（AudioManager）・ヒットストップ（ボスのとどめ）・ノイズでなめらかに揺らすカメラの揺れ（D-Drive）
- 敵の体のつぶれ・揺れ（SwarmSpriteCommon.hlsl）・ステージの物のバネの揺れ（DampedSpring2D）

---

## 出典 [SRC]

- 1€フィルター: [論文の作者のページ](https://cristal.univ-lille.fr/~casiez/1euro) ／ [Unity 版](https://github.com/DarioMazzanti/OneEuroFilterUnity)
- ラディアンスカスケード: [Unity URP 版](https://relatedrepos.com/gh/alexmalyutindev/unity-urp-radiance-cascades) ／ [2D URP 版](https://relatedrepos.com/gh/Youssef-Afella/UnityURP-RadianceCascades2DGI) ／ [Unity の標準 GI との比較論文（DiGRA）](https://dl.digra.org/index.php/dl/article/view/2775)
- JFA: [Wikipedia](https://en.wikipedia.org/wiki/Jump_flooding_algorithm) ／ [Unity の縁取り（bgolus）](https://gist.github.com/bgolus/a18c1a3fc9af2d73cc19169a809eb195) ／ [JFA で 2D の光（Light2D）](https://github.com/robert-k/light2d)
- 2D 流体: [Ignitement の解説（Unity ブログ）](https://unity.com/blog/real-time-fluid-simulation-fire-vfx-ignitement-breakdown) ／ [GPU Fluid Sim（itch.io）](https://denizin.itch.io/gpu-fluid-sim) ／ [GPU Gems 3 30章](https://developer.nvidia.com/gpugems/gpugems3/part-v-physics-simulation/chapter-30-real-time-simulation-and-rendering-3d-fluids)
- メタボール: [GPU Gems 3 7章](https://developer.nvidia.com/gpugems/gpugems3/part-i-geometry/chapter-7-point-based-visualization-metaballs-gpu)
- XPBD: [Towards Generalized Position-Based Dynamics](https://arxiv.org/html/2511.23131v1)
- 色: [OKLCH の解説（Evil Martians）](https://evilmartians.com/chronicles/oklch-in-css-why-quit-rgb-hsl) ／ [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/?p=91) ／ [ゲームの色を誰にでも見えるように](https://indieklem.substack.com/p/how-to-make-colors-games-accessible)
- Surface Cache GI: [Unity の説明](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/surface-cache-gi-volume-override-reference) ／ [プレビューのお知らせ（Unity Discussions）](https://discussions.unity.com/t/surface-cache-gi-preview/1720494)
- 3D の上の 2D スプライトの描き方（GDC 2026、読む資料として）: [How We Draw a 3D Sprite World（Never's End）](https://www.ryanjuckett.com/how-we-draw-a-3d-sprite-world-gdc2026/)
