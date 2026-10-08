# CLAUDE.md

## ツールのドキュメント（必読・必ず更新）

このゲーム（「握れ、灼ける前に」）に入っているツールには、それぞれ **人間向けガイド（HTML）** と **AI 向けリファレンス（.md）** が `Docs/Tools/` にある。

| ツール | AI 向け（作業前に読む） | 人間向け | コード |
|---|---|---|---|
| 要塞デザイナー | `Docs/Tools/FortressDesigner_AI_Reference.md` | `FortressDesigner_ガイド.html` | `Assets/_Game/Fortress/` |
| 握力入力ブリッジ | `Docs/Tools/GripInputBridge_AI_Reference.md` | `GripInputBridge_ガイド.html` | `Assets/_Tools/GripInputBridge/`, `Assets/ArduinoSerialReader.cs` |
| ShaderFX | `Docs/Tools/ShaderFX_AI_Reference.md` | `ShaderFX_ガイド.html` | `Packages/com.ms2026.shaderfx/` |
| SpriteAnim | `Docs/Tools/SpriteAnim_AI_Reference.md` | `SpriteAnim_ガイド.html` | `Packages/com.ms2026.spriteanim/` |
| D-Drive（外部パッケージ） | `Docs/Tools/DDrive_AI_Reference.md` | `D-Drive_ガイド.html` | `Library/PackageCache/com.ddrive.core@*`, `Assets/_Game/DDrive/` |
| タイトル画面（作業中） | `Docs/Tools/TitleScreen_AI_Reference.md` | `TitleScreen_ガイド.html` | `Assets/Title/`, `Assets/Scenes/Title.unity` |

1. **読む**: 上のツールに関わる作業では、ソースを読み始める前に該当する `*_AI_Reference.md` を読むこと。ソースを開くのは、そこに書いていないことを確かめるときだけにする。
2. **更新する**: ツールのコード・挙動・UI の文言・メニュー・既定値・パス・公開 API を変更／追加したら、**同じ作業の中で**そのツールの `*_ガイド.html` と `*_AI_Reference.md` を両方更新する（後回しにしない）。D-Drive は版の更新、ID の追加・変更、呼び出し箇所や設定の変更が対象。
3. **新しいツールを作ったら**: 両方のドキュメントを新規作成し、`Docs/Tools/index.html` と上の表と `Docs/Tools/DOCS_GUIDE.md` の表に追加する。
4. 書き方・構成・確認手順は `Docs/Tools/DOCS_GUIDE.md` に従う（HTML はプランナーが初見で使えるレベル、.md は前提知識ゼロの AI が読む前提）。
5. `Packages/manifest.json` の D-Drive の版が `DDrive_AI_Reference.md` に書かれた版（v1.2.1）と違うときは、内容が古い可能性がある。使う部分をソースで確かめ、リファレンスの更新をユーザーに提案すること。

## 演出（エフェクト・効果音）の共通ルール

ゲーム内の演出欄は、すべて `FortressEffect`（`Assets/_Game/Fortress/Runtime/Effects/`。D-Drive の VFX・SE の番号札＋プレイヤー色）にそろえる（ユーザー決定 2026-10-04）。新しく演出欄を作るときも、Prefab や AudioClip を直接持たせずこの型を使い、`FortressEffectPlayer` で鳴らす（エディタの見た目・試し再生ボタンは自動で付く）。マルチプレイでは通信せず各PCが自分で鳴らすので、VFX の Flags › Net は Local のまま。詳細は `Docs/Tools/FortressDesigner_AI_Reference.md` の「Effects」節。

## マルチプレイ化（作業中）

4人対戦（LAN・Host権威）の作業状況・ユーザーが決めた方針・残りの宿題（2台のPCでのテスト、性能改善の続き、Phase 5/6）は `Docs/Multiplayer/MultiplayerProgress.md` にまとめてある。マルチプレイ・通信・群衆の同期に関わる作業では、最初にこれを読み、進んだら同じ作業の中で更新すること。群衆の同期は「写真方式」（Hostが全員の位置を1秒30回、差分圧縮した写真で配り、Clientは先読みして表示）。人間向けの解説とネットワークの基礎は `Docs/Multiplayer/群衆同期_写真方式_解説.html`、実装の詳細は `Docs/Tools/FortressDesigner_AI_Reference.md` の Net 節。方式・既定値・ファイル構成を変えたら、この解説HTMLも同じ作業の中で更新する。
