# SourceAssets/Cutscene

このフォルダには **Maya から書き出したカットシーン用 FBX** を置きます。
他の種別フォルダと違い、1 ファイル = 1 Data ではなく「1 ショット = FBX セット」で 1 つの
CutsceneData になります(詳細は `docs/26_timeline.md` §5、Maya 作業者向けの手順は
`docs/DesignerManual/cutscene-maya-export.html`)。

## 対応拡張子

.fbx

## 置き方(1 ショットにつき)

- `Cutscene/<カテゴリ.../>ショット名.fbx` — カメラ + 小物(1 本)
- `Cutscene/<カテゴリ.../>ショット名__Model識別子.fbx` — キャラごとの骨アニメ(`__` は 2 つ。キャラの数だけ)
- 同じキャラを 2 体使う場合は `ショット名__Model識別子_2.fbx`

## 生成される Data の例

`Cutscene/Opening/Opening01.fbx` + `Cutscene/Opening/Opening01__Hero.fbx` を置くと、
`Assets/GameData/Cutscene/Opening/CUT_Opening_Opening01.asset` が自動的に作られます。

## 注意

キャラ FBX の Model 識別子(ファイル名の `__` の後ろ)と一致する ModelData が見つかると、
自動で Humanoid + Avatar 引き継ぎで取り込まれます。見つからない場合は Console に警告が出ます。
再取り込みは自動生成トラック/カーブだけを差し替え、Timeline に手で足したトラックや
StepFps/Blend/Focus の設定はそのまま残ります。
