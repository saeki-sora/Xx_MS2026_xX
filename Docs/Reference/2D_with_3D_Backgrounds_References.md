# 2Dのキャラ・敵＋3Dの背景・オブジェクトのゲーム リファレンス（2026-11-20）

このゲームと同じく「キャラや敵は2Dの絵（板・スプライト）、床・障害物・背景は3Dモデル」という作りのゲームを、開発者インタビュー・技術記事・レビューからネットで集めたもの。ネットからの参考集めは、このときユーザーが頼んだので行った（それ以外のときは勝手に開かない）。

このゲームの今の作り（ソースで確認、2026-11-20）: 床・歯のコア・障害物はステージ背景スタジオで取り込んだ3Dモデル（Maya の FBX）。敵の群れは `SwarmRenderer` が板（`SwarmSprite.shader`）として描く。カメラは見下ろしで、プリセットで遠近・傾きを変えられる（`StageGameCamera.HasParallax`）。

## 使い方・守ること [RULES]

- **画面は人間が見る**。リンクは人間が自分で開くためのもの。AI（Claude・画像生成）には画面写真・アートを渡さない。作品名・作家名で画風を寄せる指示もしない（CLAUDE.md、`UI_Design_References.md` の `[SAFETY]`）。
- 根拠の印: **本人** = 開発者自身の言葉／**記事** = 記者のまとめ／**説** = ファン・フォーラム・解析（裏付けが弱い）。
- 括弧の P1〜P8 は `Beautiful_2D_Analysis.md` の8つの理由。背景の見た目そのものは `Beautiful_Backgrounds_References.md` も見る。
- 関係するユーザー決定（優先）: 霧は使わない・光の筋は常に出す・要素を増やさない（2026-11-04）／歯のコアはトゥーンが土台（2026-11-07）。

---

## 1. 見下ろし・斜め見下ろしで「3Dの世界＋2Dの板」 [TOPDOWN]

このゲームに一番近い。

| ゲーム | 作り | 分かっていること | 出典 |
|---|---|---|---|
| **Cult of the Lamb**（2022, Massive Monster, Unity） | 2Dの絵のキャラ・建物を3D空間に立て、斜め見下ろしのカメラで撮る | 本人: 初めての3Dエンジンなので、もう1つの軸を使いたくて 2.5D にした。板を動かして「にせの3Dの建物」を組み、カメラとキャラの角度はエディタで決めた。2.5D の仕組みは自作が必要だった。Reddit で開発者が「2Dの板と、うまい美術の決めだけ」と回答 | [Unity ブログ（日本語あり）](https://unity.com/ja/blog/games/recipe-behind-smash-hit-cult-of-the-lamb)（本人）, [r/Unity3D: how did Cult of the Lamb pull off this depth](https://redlib.hbubli.cc/r/Unity3D/comments/1joamr1/how_did_cult_of_the_lamb_pull_off_this_depth)（本人の返答＋説） |
| **Enter the Gungeon**（2016, Dodge Roll, Unity） | 見た目は2Dの見下ろしだが、中は3D | 説（改造者向け資料）: 複雑な重なり順を自然に扱うため、実は3D空間で描いている。壁は本物の3Dの物、普通の板は45°傾けて置く。立っている物が奥へ倒れて見えるのはカメラと角度の工夫 | [ETG modding guide: Perpendicularity & Height](https://mtgmodders.gitbook.io/etg-modding-guide/sprites/perpendicularity-and-height-off-ground)（説）, [Mod the Gungeon: sprite structure](https://modthegungeon.gitbook.io/semi/spriting)（説） |
| **Don't Starve**（2013, Klei） | 2Dアニメの板を3Dの世界に立て、斜めのカメラ（回転できる） | 説: 板をカメラに向けて回す（ビルボード）。アニメは Flash で作った（開発者の発言の転載）。板を大きくしたり小さくしたりするときの乱れが一番の難所、という別の開発者の声 | [Klei forums: name for this style](https://kleiforums.com/forums/topic/56775-is-there-a-name-for-the-games-2d-as-3d-animation-style/)（説）, [The art of Don't Starve（解説）](https://ryancosans.github.io/games/2020/03/18/the-art-of-dont-starve.html)（説）, [Cocos: 3D ゲームを2Dに見せる](https://discuss.cocos2d-x.org/t/tutorial-cocos-creator-giving-a-2d-look-for-your-3d-game/54999)（説・手順） |
| **Cassette Beasts**（2023, Bytten Studio・2人, Godot） | ドット絵のキャラ＋3Dの地形 | 本人: 3D（2.5D）のゲームだが、カメラの角度が固定で地形が升目なので、マップは2Dの見下ろしゲームのつもりで作る。地形はボクセル（Qubicle）で作る。記事: 3Dの地形にドット風のテクスチャを貼ってキャラに合わせた | [Godot Showcase: Cassette Beasts](https://godotengine.org/article/godot-showcase-cassette-beasts/)（本人）, [公式ブログ](https://www.cassettebeasts.com/?p=1116)（本人）, [Boiling Steam review](https://boilingsteam.com/cassette-beasts/)（記事） |
| **Songs of Conquest**（2024, Lavapotion, Unity） | 地面・崖は3D、木・建物・キャラは2Dのドット絵 | 本人: 最初は全部2Dで、試作を重ねて今の形になった。光は専用の「光の板」を別のカメラで描いて重ねる。ドット絵は Aseprite、タイルは PyxelEdit | [80.lv: Mixing 2D billboards and 3D environments](https://80.lv/articles/mixing-2d-billboards-and-3d-environments-in-a-game/)（本人）, [80.lv: The shader approach to billboarding](https://80.lv/articles/the-shader-approach-to-billboarding/)（本人）, [CGM: Niklas Borglund interview](https://cogconnected.com/feature/lavapotion-songs-of-conquest-interview/3)（本人） |
| **Lunarium**（開発中, Unity） | 手描きの2Dの絵＋簡単な3Dの形、斜め見下ろしのアクションRPG | 本人: 普通の3Dの光は手描きと合わなかったので、3Dの見え方の環境に合う「2Dの光の仕組み」を自作した。手描きの絵の上にカメラ向きの簡単な3Dの形を重ね、カメラから絵を投影して貼る | [80.lv: How Lunarium blends hand-painted 2D art with 3D](https://80.lv/articles/how-lunarium-blends-hand-painted-2d-art-with-3d-environments)（本人） |
| **Blightbound**（2021, Ronimo, Unity URP） | 2Dの見た目のまま、3Dの動きと3Dのカメラ | 本人: エフェクトの多くは、平らな板をいろいろな角度で重ねて3Dに見せる。地面の輪は床に寝かせ、投げる武器は少しカメラへ傾ける。コマ送りのアニメも混ぜる | [Game Developer: VFX breakdown](https://www.gamedeveloper.com/art/vfx-breakdown-combining-2d-and-3d-in-blightbound)（本人）, [Real Time VFX: Creation of the VFX](https://realtimevfx.com/t/creation-of-the-vfx-for-blightbound/15141)（本人） |
| **Songs of Glimmerwick**（開発中, Eastshade Studios） | 2Dの絵を3Dの形に投影 | 本人: 小物は「3Dエンジンの中の2D」の普通のやり方に、リアルタイムの影を足した。建物は絵を投影するので、キャラが周りを歩いても前後関係と当たり判定が正しくなる | [Game Developer: Deep dive, projected 2D workflow](https://gamedeveloper.com/production/deep-dive-the-innovative-projected-2d-workflow-of-songs-of-glimmerwick)（本人） |
| **ポケモン ブラック・ホワイト**（2010, ゲームフリーク） | 3Dの町＋2Dのキャラの板 | 説: キャラも実はカメラを向く3Dの板。ヒウンシティではカメラが動いて大都会らしさを出した。**批判**: カメラが大きく動くと2Dの板がひどく見える、という声も | [Recreating the 2.5D effect from Pokémon B&W](https://mjakeman.substack.com/p/recreating-the-25d-effect-from-pokemon)（説・再現手順）, [GameSpot first look](https://www.gamespot.com/articles/pokemon-black-version-first-look/1100-6296943)（記事）, [Zelda Dungeon: a tale of two cities（批判）](https://www.zeldadungeon.net/pokemon_black_white_daily_log_a_tale_of_two_cities/)（説） |

---

## 2. HD-2D（ドット絵のキャラ＋3Dのジオラマ） [HD2D]

| ゲーム | 分かっていること | 出典 |
|---|---|---|
| **Octopath Traveler / II**（2018・2023, Square Enix＋Acquire, UE4） | 本人: 2Dのドットと3Dの環境を混ぜるのが HD-2D の出発点。II では高解像度にして昼夜で動く光を足した。**悩み**: キャラは2Dなので紙のように平らに見え、3Dのカメラをどこまで回せるかが問題だった。背景はほぼ全部3D | [Unreal Engine: Octopath II interview（日本語あり）](https://www.unrealengine.com/ja/developer-interviews/octopath-traveler-ii-builds-a-bigger-bolder-world-in-its-stunning-hd-2d-style)（本人）, [Unreal Engine spotlight 2019](https://www.unrealengine.com/spotlights/octopath-traveler-s-hd-2d-art-style-and-story-make-for-a-jrpg-dream-come-true)（本人）, [Siliconera: HD-2D の苦労](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/)（本人） |
| **Triangle Strategy**（2022, Square Enix＋Artdink） | 本人: いくつかの会社に HD-2D の試作映像を作ってもらい、Acquire と見比べて Artdink に決めた。ドットが細かく高解像度になるほど「イラストに近く」なる。見た目は思うよりお金がかかる。記事: 写実的な光がデフォルメのキャラに当たり、ジオラマのよう | [Nintendo Life: producers talk HD-2D](https://www.nintendolife.com/news/2022/05/triangle-strategy-producers-talk-hd-2d-and-why-other-devs-havent-used-it)（本人）, [Nintendo Everything: "accurate" HD-2D](https://nintendoeverything.com/triangle-strategy-devs-on-how-the-game-uses-accurate-hd-2d)（本人）, [GamingBolt: costs more than you'd think](https://gamingbolt.com/triangle-strategys-art-style-costs-more-than-youd-think-producer)（本人） |
| **ドラゴンクエストIII HD-2D**（2024） | `Beautiful_Backgrounds_References.md` の `[TOPDOWN]` を見る（宿だけ灯りの色を変える例） | 同ファイル |
| HD-2D の一般的な説明 | 説: 被写界深度・ティルトシフト（ミニチュア写真のぼかし）・動く光でジオラマに見せる。素朴に板を置くと法線がいつもカメラを向くので紙のように薄く見え、法線マップで量感を足す方法がある | [HD-2D explained（Wikipedia 転載）](https://everything.explained.today/HD-2D/)（説）, [DEV: Creating an HD-2D rendering pipeline on DX12](https://dev.to/gaurav_de/creating-an-hd-2d-rendering-pipeline-on-dx12-205k)（説・個人の実装） |

---

## 3. 横から見る・その他の混ぜ方 [SIDE]

| ゲーム | 作り | 分かっていること | 出典 |
|---|---|---|---|
| **DAVE THE DIVER**（2023, MINTROCKET, Unity） | ドット絵のデイヴ＋3Dの海の生き物・背景の物 | 本人: だんだん背景の物だけでなく海の生き物も3Dにした（一番難しかった）。「2Dのアーケードらしい動きと、3Dの空間の感じ、両方が魅力だった」。Shader Graph でアーティストが自分でエフェクトを作れた | [Unity case study: Dave the Diver](https://unity.com/case-study/dave-diver)（本人）, [Game World Observer: 2D/3D optimization](https://gameworldobserver.com/2023/12/04/dave-the-diver-unity-2d-3d-graphics-optimization-mintrocket)（記事）, [Game Developer: Dave the Diver](https://gamedeveloper.com/design/dave-the-diver)（本人） |
| **Hollow Knight / Silksong**（Team Cherry, Unity） | 2Dの絵を3D空間に奥行きをずらして並べる | 本人: 2Dの素材を3Dの環境に重ねてレベルを作った（Unity 事例）。説: Silksong を解析すると実は3Dで、使っていない奥行きの軸で多くの層を並べて視差を出している | [Unity: Hollow Knight](https://unity.com/case-study/hollow-knight)（本人）, [Creative Bloq: optical illusions in games](https://www.creativebloq.com/3d/video-game-design/these-ingenious-optical-illusions-make-game-development-easier)（説・解析） |
| **ペーパーマリオ**（2000〜, インテリジェントシステムズ） | 3Dのポリゴンの世界＋紙のように薄いキャラ | 本人: プレイステーションなどの3DCGの見た目に皆が飽きてきたのでは、と考えた。3Dポリゴンで作るが、絵本をそのままゲームにしたような空気にした | [shmuplations: Paper Mario 2000 developer interviews](https://shmuplations.com/?p=8702)（本人・英訳）, [社長が訊く ペーパーマリオ スーパーシール](https://www.nintendo.co.uk/Iwata-Asks/Iwata-Asks-Paper-Mario-Sticker-Star/Paper-Mario-Sticker-Star/1-Talking-At-Iwata/1-Talking-At-Iwata-690475.html)（本人） |
| **At Fate's End**（2026予定, Thunder Lotus, Unity） | 2Dの絵を3Dのカメラで撮る | 記事: 2Dのゲームだが3Dのカメラに頼り、遠くの物が遠くに見えるようにした。飛び出す絵本のよう | [Creative Bloq: 3D camera tricks](https://www.creativebloq.com/3d/video-game-design/how-at-fates-end-uses-3d-camera-tricks-to-make-its-2d-art-feel-alive)（記事） |

---

## 4. 古典と「群れの敵を板で描く」系 [CLASSIC]

| ゲーム | 分かっていること | 出典 |
|---|---|---|
| **ファイナルファンタジータクティクス**（1997） | 記事: 回転できる3Dの斜め見下ろしの戦場に、描いた2Dのキャラの板。2.5D の代表例 | [Wikipedia: 2.5D](https://en.wikipedia.org/wiki/2.5D)（記事）, [TheGamer: best games to combine polygons and sprites](https://www.thegamer.com/best-games-to-combine-polygons-sprites-hd-2d/)（記事） |
| **ゼノギアス**（1998） | 記事: 3Dの世界に2Dのキャラの板、ロボットは3Dモデル | [Xeno Series Wiki](https://xenoserieswiki.org/wiki/Xenogears_(game))（説）, [MobyGames forum](https://www.mobygames.com/forum/game/4154/thread/113740/2d-sprites/)（説） |
| **日本一ソフトウェアのSRPG**（ラ・ピュセル以降・ディスガイア） | 記事: 3Dのマップに2Dのキャラの板。グランディア・ヴァンダルハーツなども同じ系統 | [TheGamer（同上）](https://www.thegamer.com/best-games-to-combine-polygons-sprites-hd-2d/)（記事） |
| **Warhammer 40,000: Boltgun**（2023）ほか Doom 系 | 本人: 敵と手元の武器は2Dの板、環境と物は3D。敵はほぼ正面から見られるので、正面の絵を一番良く見せる。**大量の敵を板で描く**点がこのゲームの群れに近い | [Xbox Wire: Breathing retro life into the enemies](https://news.xbox.com/en-us/?p=202509)（本人）, [80.lv: Nightmare Reaper](https://80.lv/articles/nightmare-reaper-how-an-indie-studio-created-a-retro-inspired-fps-game)（本人） |

---

## 5. 技術の記事・一覧 [TECH]

- [80.lv: The shader approach to billboarding](https://80.lv/articles/the-shader-approach-to-billboarding/)（本人）: 何千もの動く板はすぐ重くなる。回る軸を1本に絞るとまとめて描けて軽い。
- [Recreating the 2.5D effect from Pokémon B&W](https://mjakeman.substack.com/p/recreating-the-25d-effect-from-pokemon)（説）: カメラを向く板の作り方を順に説明。
- [Unity Discussions: Octopath の影の角度は Unity でできるか](https://discussions.unity.com/t/how-do-games-like-octopath-traveler-handle-the-angle-of-shadows-and-is-this-even-possible-in-unity-urp/246459)（説）: 板の影の向きの問題。
- [Unreal forums: 見下ろしで2Dの板の影を3Dと合わせる](https://forums.unrealengine.com/t/paper-2d-sprites-top-down-camera-how-to-get-shadows-consistent-with-3d-space-4-26/505228)（説）: 見える板は影を落とさず、床に垂直に立てた見えない板に影を落とさせる案。
- [Godot: Sprite3D に法線マップ](https://eliornadir.github.io/gamedev/GodotSprite3DNormal)（説）: 板に法線マップを付けて光に反応させる手順。
- [Game Developer: GDC 2005, Interactive 3D lighting in sprites rendering](https://gamedeveloper.com/programming/gdc-2005-proceeding-interactive-3d-lighting-in-sprites-rendering)（本人・講演）
- [Unity: Warhammer 40,000 Warpforge](https://unity.com/blog/games/stunning-scenarios-in-warhammer-40000-warpforge)（本人）: 2Dの絵を3Dの形に投影して貼る。
- [Wikipedia: 2.5D](https://en.wikipedia.org/wiki/2.5D) ／ [TV Tropes: Sprite/Polygon Mix](https://tvtropes.org/pmwiki/pmwiki.php/Main/SpritePolygonMix) ／ [Giant Bomb: 2D Characters in a 3D Scenario](https://giantbomb.com/wiki/Concepts/2D_Characters_in_a_3D_Scenario)（作品名を拾う用、説）

日本語の講演資料（CEDEC など）でこの作り方を扱ったものは、今回の検索では見つからなかった。アークナイツ（3Dのマップ＋2Dのキャラの塔防衛）も候補にしたが、作り方を説明した資料は見つからなかったので表に入れていない。

---

## 6. 繰り返し出てきた問題と工夫 [FINDINGS]

複数の作品で共通していた点。**提案で、何も決まっていない**。採用するかはユーザーに聞く。

1. **板は「紙のように薄く」見えやすい（P1・P7）**: Octopath II の開発者自身が悩みとして挙げ、ポケモンBWはカメラを大きく動かして批判された。Cassette Beasts・Cult of the Lamb はカメラ角度を固定して決めた。→ このゲームも見下ろしのカメラを固定に近く保ち、試合の終わりのズームなどで大きく回さない方が安全。
2. **板を少し傾けて置く（P4）**: Enter the Gungeon は板を45°、Blightbound は投げる武器を少しカメラへ、地面の輪は床に寝かせる。→ 敵の群れは「立てる板」、床の印や波紋は「寝かせる板」と分けて決めると、3Dの床となじみやすい。
3. **板にも影と光を当てる（P4）**: Glimmerwick は小物にリアルタイムの影、Songs of Conquest は光だけ別のカメラで描いて重ねる、Lunarium は2D用の光を自作。フォーラムでは「見えない板に影を落とさせる」「法線マップを付ける」案がある。→ 3Dの障害物の影が敵の板に落ちる・落ちないを揃えるだけで「同じ空間にいる」感じが出る（赤スキルの光は障害物で止まる、の決まりと同じ考え）。
4. **3Dの側を2Dの絵柄に寄せる（P2・P5）**: Cassette Beasts は地形にドット風のテクスチャ、Lunarium・Glimmerwick・Warpforge は手描きの絵を3Dに投影する。→ このゲームは3D側がトゥーン（歯のコアの決定 2026-11-07）なので、敵の板の輪郭・影の段の数をトゥーンの段と揃える、という方向が考えられる。
5. **3D空間に置くと重なり順が自然に決まる**: Gungeon は重なり順のために3Dで描き、Glimmerwick は投影で前後関係と当たり判定が正しくなる。→ このゲームの「障害物の裏の敵」は今の作り（ステージの透け・隠れた敵の影）で扱える範囲。
6. **大量の板は描き方で重さが変わる**: Songs of Conquest は回る軸を1本に絞ってまとめて描いた。→ 敵数千体の群れの描き方（今は `SwarmRenderer`）を見直すときの参考。
7. **ジオラマのぼかし（被写界深度・ティルトシフト）は HD-2D の特徴**: ただしこのゲームは敵数千体を読ませるのが最優先（P1）。ぼかしは霧と同じく主役を読みにくくするので、使うなら画面の縁だけ、など弱く試す（霧は使わない決定 2026-11-04 は守る）。
