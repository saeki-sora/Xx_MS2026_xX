# CLAUDE.md

## ツールのドキュメント（作業前に読む・更新はしない）

このゲーム（「握れ、灼ける前に」）に入っているツールには、それぞれ **人間向けガイド（HTML）** と **AI 向けリファレンス（.md）** が `Docs/Tools/` にある。

| ツール | AI 向け（作業前に読む） | 人間向け | コード |
|---|---|---|---|
| 要塞デザイナー | `Docs/Tools/FortressDesigner_AI_Reference.md` | `FortressDesigner_ガイド.html` | `Assets/_Game/Fortress/` |
| 握力入力ブリッジ | `Docs/Tools/GripInputBridge_AI_Reference.md` | `GripInputBridge_ガイド.html` | `Assets/_Tools/GripInputBridge/`, `Assets/ArduinoSerialReader.cs`, `Tools/Exhibition/`（展示用：LAN で配る起動アイコン） |
| ShaderFX | `Docs/Tools/ShaderFX_AI_Reference.md` | `ShaderFX_ガイド.html` | `Packages/com.ms2026.shaderfx/` |
| SpriteAnim | `Docs/Tools/SpriteAnim_AI_Reference.md` | `SpriteAnim_ガイド.html` | `Packages/com.ms2026.spriteanim/` |
| ステージ背景スタジオ | `Docs/Tools/StageStudio_AI_Reference.md` | `StageStudio_ガイド.html` | `Assets/_Game/Stage/`, `Assets/_Tools/StudioKit/`（共通の画面部品） |
| UIスタジオ | `Docs/Tools/UIStudio_AI_Reference.md` | `UIStudio_ガイド.html` | `Assets/_Game/UI/`, `Assets/_Tools/StudioKit/` |
| D-Drive（外部パッケージ） | `Docs/Tools/DDrive_AI_Reference.md` | `D-Drive_ガイド.html` | `Library/PackageCache/com.ddrive.core@*`, `Assets/_Game/DDrive/` |
| タイトル画面（作業中） | `Docs/Tools/TitleScreen_AI_Reference.md` | `TitleScreen_ガイド.html` | `Assets/Title/`, `Assets/Scenes/Title.unity` |
| キャラクター選択（作業中） | `Docs/Tools/CharacterSelect_AI_Reference.md` | `CharacterSelect_ガイド.html` | `Assets/CharacterSelect/`, `Assets/Scenes/Character_Select.unity`, `Assets/_Game/UI/Game/Session/GameSession.CharacterSelect.cs` |
| 録画しながらプレイ（自作パッケージ） | `Docs/Tools/PlayRecorder_AI_Reference.md` | `PlayRecorder_ガイド.html` | `Packages/com.ms2026.playrecorder/`（パッケージ内 `README.md` も同時に更新） |
| 赤スキル（キャラのスキル欄・作業中） | `Docs/Tools/RedSkill_AI_Reference.md` | `RedSkill_ガイド.html` | `Assets/RedSkill/`（スキル欄・配る係・対応表は4色共通、`Red/` が赤の極太レーザー、`Yellow/` が黄色のリコシェアロー）, `Assets/Scenes/redskill_testscene.unity` |
| ステージ切り替え・ステージ抽選ルーレット（作業中） | `Docs/Tools/Stages_AI_Reference.md` | `Stages_ガイド.html` | `Assets/Stages/`, `Assets/Scenes/Stages/`（分けた後）, `Assets/_Game/UI/Game/Session/GameSession.Stage.cs`, `FortressNetworkBootstrap.AutoStartGate` |
| 緑スキル（ミント・サテライト・作業中） | `Docs/Tools/GreenSkill_AI_Reference.md` | `GreenSkill_ガイド.html` | `Assets/RedSkill/Scripts/Runtime/Green/`, `Assets/RedSkill/Shaders/GreenSkill*.shader`, `Assets/RedSkill/Scripts/Editor/GreenSkillMenu.cs`, `Assets/_Game/UI/Game/HudCoreHealFx.cs`（HUD のコアのバーの回復演出） |

1. **読む**: 上のツールに関わる作業では、最初に該当する `*_AI_Reference.md` を読んで全体をつかむ。ただし下の 2 のとおり資料はもう更新していないので、2026-10-24 より後の変更は載っていない。作業で使う所は必ずソースで確かめる（資料とソースが違えば、ソースが正しい）。
2. **資料は更新しない**（ユーザー決定 2026-10-24。それまでの「同じ作業の中で必ず更新する」決まりはやめた）: ツールのコード・挙動・UI の文言などを変えても、`*_ガイド.html`・`*_AI_Reference.md`・`Docs/Tools/index.html`・`Docs/Tools/DOCS_GUIDE.md` は更新しなくてよい。新しいツールを作っても資料は作らない。ユーザーに頼まれたときだけ更新・作成し、そのときは `Docs/Tools/DOCS_GUIDE.md` の書き方に従う。
3. `Packages/manifest.json` の D-Drive の版が `DDrive_AI_Reference.md` に書かれた版（v1.2.1）と違うときは、内容が古い可能性がある。使う部分をソースで確かめること。

## 演出（エフェクト・効果音）の共通ルール

ゲーム内の演出欄は、すべて `FortressEffect`（`Assets/_Game/Fortress/Runtime/Effects/`。D-Drive の VFX・SE の番号札＋プレイヤー色）にそろえる（ユーザー決定 2026-10-04）。新しく演出欄を作るときも、Prefab や AudioClip を直接持たせずこの型を使い、`FortressEffectPlayer` で鳴らす（エディタの見た目・試し再生ボタンは自動で付く）。マルチプレイでは通信せず各PCが自分で鳴らすので、VFX の Flags › Net は Local のまま。詳細は `Docs/Tools/FortressDesigner_AI_Reference.md` の「Effects」節。

## マルチプレイ化（作業中）

4人対戦（LAN・Host権威）の作業状況・ユーザーが決めた方針・残りの宿題（2台のPCでのテスト、性能改善の続き、Phase 5/6）は `Docs/Multiplayer/MultiplayerProgress.md` にまとめてある。マルチプレイ・通信・群衆の同期に関わる作業では、最初にこれを読む（2026-10-24 から更新はしていないので、ソースでも確かめる。更新はユーザーに頼まれたときだけ）。群衆の同期は「写真方式」（Hostが全員の位置を1秒30回、差分圧縮した写真で配り、Clientは先読みして表示）。人間向けの解説とネットワークの基礎は `Docs/Multiplayer/群衆同期_写真方式_解説.html`、実装の詳細は `Docs/Tools/FortressDesigner_AI_Reference.md` の Net 節（この解説 HTML も、頼まれたとき以外は更新しない）。

## 見た目の作業の共通ルール（美しい2Dの分析を土台にする）

見た目に少しでも関わる作業（シェーダー・エフェクト・色・光・カメラ・UI の見た目・アニメ・ステージの見え方・キャラの絵の見やすさ・スキル演出・見本ページづくりなど）を頼まれたら、最初に `Docs/Reference/Beautiful_2D_Analysis.md` を読む（ユーザー決定 2026-11-09）。綺麗と言われる2Dゲーム20本から「綺麗に感じる8つの理由」（P1 読める〜P8 引き算）をまとめた Claude 専用の資料。まず `[CHECK]` のチェック表を読み、必要な節だけ `[TAG]` で探して読む。提案・比較ページ・実装は、どの理由に効くかを考えて作り、作った後にチェック表で崩していないか確かめる。ユーザーが決めたこと（メモリ・この CLAUDE.md・各資料）がこの資料の提案より優先。資料の更新はユーザーに頼まれたときだけ。

## ツール・UI の作業の共通ルール（使いやすいツールの分析を土台にする）

エディタのツール（要塞デザイナー・ステージ背景スタジオ・UIスタジオ・スキル調整スタジオ・インスペクターなど）やゲーム内の UI（メニュー・HUD・設定画面）を作る・直すときは、最初に `Docs/Reference/Tool_UI_Analysis.md` を読む（ユーザー決定 2026-11-09）。使いやすいと評判のツールと使いやすさの研究から「見やすい・使いやすい10の理由」（T1 やりたいことで並べる〜T10 使う人を見て直す）をまとめた Claude 専用の資料。まず `[CHECK]` を読み、必要な節だけ `[TAG]` で探して読む。特に「開いた瞬間に見える項目を少なく、残りは1段奥へ」（T2）を守る（スキル調整スタジオは項目が多すぎて見づらいと言われた）。ユーザーが決めたことがこの資料の提案より優先。資料の更新はユーザーに頼まれたときだけ。

## UI デザインの参考サイト

画面の見た目・レイアウト・操作の分かりやすさ、画像やUIの「AIっぽさ」対策を考えるときだけ、`Docs/Reference/UI_Design_References.md` の必要な節（`[TAG]` で Grep）を読む。サイトは原則開かず、ログイン・課金・インストール等は一切しない（同ファイル `[SAFETY]`）。**他人の絵・イラスト・他社ゲームの画面は、AI（画像生成・Claude 自身とも）に見本や学習用として渡さない。作家名・作品名で画風を寄せる指示もしない**（ユーザー決定 2026-10-13）。**例外**（ユーザー決定 2026-11-02）: ユーザーが自分で渡した参考（動画など）を「参考に」と頼まれたときは、Claude が開いて見てよい（動き・配置・タイミングをつかむため）。作るものは一から自分たちで作り、絵を写したり、画像生成 AI や学習に渡したりはしない。
