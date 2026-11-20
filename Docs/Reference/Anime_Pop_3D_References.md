# アニメ調・ポップな3Dモデルのゲーム リファレンス（2026-11-20）

このゲームのように、3Dモデルを写実ではなく**アニメ調（トゥーン・セル調）やポップな雰囲気**で見せているゲームを、開発者の講演・インタビュー・記事からネットで集めたもの。ネットからの参考集めは、このときユーザーが頼んだので行った（それ以外のときは勝手に開かない）。

このゲームの今の見た目（メモリ・コミット履歴より）: 歯のコアはトゥーンが土台で、汚れ＋縁の光（2026-11-07 決定）。敵には縁・ぷるぷる・当たったときの潰れの質感がある。4人のプレイヤー色がある。

## 使い方・守ること [RULES]

- **画面は人間が見る**。リンクは人間が自分で開くためのもの。AI（Claude・画像生成）には画面写真・アートを渡さない。作品名・作家名で画風を寄せる指示もしない（CLAUDE.md、`UI_Design_References.md` の `[SAFETY]`）。考え方・手法を言葉で使うのはよい。
- 根拠の印: **本人** = 開発者自身の言葉・講演／**記事** = 記者のまとめ・講演レポート／**説** = ファン・解析（裏付けが弱い）。
- 関連資料: `Beautiful_2D_Analysis.md`（綺麗の8つの理由 P1〜P8）、`Beautiful_Backgrounds_References.md`（背景）、`2D_with_3D_Backgrounds_References.md`（2Dの板＋3Dの背景）。

---

## 1. 見下ろし・パーティ系のポップな3D（このゲームに近い） [PARTY]

| ゲーム | 見え方 | 分かっていること | 出典 |
|---|---|---|---|
| **スプラトゥーン**（2015〜, 任天堂） | ポップな3D、2チームのインクの色 | 本人: 2チームそれぞれのインクの色からゲームを組み立て、その色が目立つように押し出した。アートディレクター井上精太さんが、スタッフの好み（ストリートファッションなど）を1つの見た目にまとめた | [Engadget: producer interview 2015](https://www.engadget.com/2015/05/29/splatoon-producer-on-making-a-nintendo-original/)（本人）, [社長が訊く Splatoon](https://iwataasks.nintendo.com/interviews/wiiu/splatoon/0/3/)（本人）, [開発者に訊きました Splatoon 3](https://www.nintendo.com/us/whatsnew/ask-the-developer-vol-7-splatoon-3-part-1/)（本人） |
| **Overcooked!**（2016, Ghost Town Games, Unity） | 斜め見下ろしの、ころっとしたポップな3D | 本人（アート・デザイン担当 Phil Duncan）: 世界とキャラを**明るいべた塗りの形**で揃え、遠くからでも読めるようにした。**注意**: 見下ろしのせいで画面の下が隠れる、という批判 | [PC Gamer: How Overcooked's tiny team...](https://pcgamer.com/how-overcookeds-tiny-team-took-over-our-living-rooms)（本人）, [Slant Magazine review](https://www.slantmagazine.com/games/overcooked/)（記事・批判） |
| **ブロスタ（Brawl Stars）**（2018, Supercell） | 見下ろしの、塊の大きい3Dキャラ | 本人: キャラは古典アニメを土台に、くっきりした目印になる形で読みやすく覚えやすくする。Clash 系の担当者: 微妙なグラデーションと明るさの差で、目を顔へ導く | [Pocket Gamer.biz: 5 creative pillars for Brawlers](https://www.pocketgamer.biz/5-creative-pillars-for-designing-brawlers-in-supercells-brawl-stars)（本人）, [Adobe: Supercell Helsinki stylized content](https://www.adobe.com/products/substance3d/magazine/supercell-helsinki-creating-stylized-content-for-clash-of-clans-and-clash-royale)（本人） |
| **Fall Guys**（2020, Mediatonic） | 丸い豆のようなキャラの3Dパーティ | 本人: 最初から「物理的に感じる世界」と「鮮やかなキャラ」を大事にした。「一番笑える形は？」から、短い足・大きな腕・丸い体になった。転ぶのが面白いので、運動神経の良いキャラにはしなかった | [GamesRadar: Fall Guys interview](https://www.gamesradar.com/fall-guys-interview), [GGRecon: what makes their jelly beans lovable](https://www.ggrecon.com/articles/fall-guys-developers-reveal-what-makes-their-jelly-beans-so-lovable)（本人） |
| **Death's Door**（2021, Acid Nerve, Unity） | 斜め見下ろし、自然な光の環境＋控えめなトゥーンのキャラ | 本人: ドット絵から全部3Dの斜め見下ろしに移った。いくつか試して斜め見下ろしに決め、特別な場所だけ正面寄りの角度にした。記事: アートディレクターは環境を自然な光で作り、キャラは控えめなトゥーンにして対比させた。被写界深度を少し使う | [Byteside: how it made its crow game](https://byteside.com/2021/08/deaths-door-developer-acid-nerve-on-how-it-made-its-sword-wielding-crow-game)（本人）, [Unity: Crafting intricate environments](https://blog.unity.com/games/crafting-intricate-environments-deaths-door)（本人）, [Game Informer: Afterwords](https://www.gameinformer.com/afterwords/2021/09/22/afterwords-deaths-door)（本人） |
| **TUNIC**（2022, Andrew Shouldice, Unity） | 斜め見下ろしのかわいいローポリ | 本人: 簡単で、見た目がまとまっていて、作り足しやすい絵柄を目指した。「細かくて面白いのに、幾何学的な形から外れない」所を探すのに何度も試した。記事: 草や茂みもできるだけ少ない頂点で作る。色の決まりで世界が光って見える | [Xbox Hub: Tunic interview](https://www.thexboxhub.com/the-tunic-interview-an-exclusive-chat-with-polymath-designer-andrew-shouldice/)（本人）, [Wireframe: Tunic and the art of keeping a secret](https://wireframe.raspberrypi.com/articles/wireframe-cover-star-tunic-and-the-art-of-keeping-a-secret)（記事）, [Game Developer: How TUNIC weaves...](https://www.gamedeveloper.com/road-to-igf-2023/how-tunic-weaves-wondrous-unknowable-worlds-inspired-by-inscrutable-nes-manuals)（本人） |
| **ピクミン4**（2023, 任天堂, Unreal） | 小さな者から見たミニチュアの世界、前作より明るくカートゥーン寄り | 記事: 前作より明るくカートゥーン調に寄せ、敵もすっきり。Digital Foundry: ミニチュアの大きさの世界に見せている。**異論**: 粘土の模型のような味が消えた、という古いファンの声 | [Digital Trends review](https://digitaltrends.com/gaming/pikmin-4-review-nintendo-switch)（記事）, [GoNintendo: Digital Foundry deep dive](https://www.gonintendo.com/contents/24084-digital-foundry-takes-a-tech-deep-dive-into-pikmin-4)（記事） |
| **星のカービィ ディスカバリー**（2022, HAL研究所） | 3Dのカービィ、廃墟の世界をポップに | 本人: 「忘れられた文明」の舞台をカービィらしい見た目となじませるため、真っ青な空と、草や花など色とりどりの植物を足した | [開発者に訊きました 星のカービィ ディスカバリー](https://www.nintendo.com.au/news-and-articles/ask-the-developer-vol-4-kirby-and-the-forgotten-land)（本人） |

---

## 2. アニメ調トゥーンの技術（講演資料が厚い） [ANIME]

| ゲーム | 分かっていること | 出典 |
|---|---|---|
| **GUILTY GEAR Xrd**（2014, アークシステムワークス） | 本人（GDC 2015）: ベクター画・超高解像度のドット絵なども検討し、専用のセルシェーダー＋そのシェーダー専用に作る3Dモデルに決めた。セル調では面が「明るいか暗いか」だけなので、面の小さな乱れが全部目立ち、整えるのが難しい。記事（講演レポート）: 3Dらしさを消すのが狙い。法線マップを使わず、影は画家が直接決める。顔には専用の光を当て、自然な影より明るく見やすくした。アニメは補間せず、キーだけのコマ打ち | [GDC Vault: The X Factor Between 2D and 3D](https://gdcvault.com/play/1022031/GuiltyGearXrd-s-Art-Style-The)（本人・動画）, [講演の配布資料 PDF](https://www.ggxrd.com/Motomura_Junya_GuiltyGearXrd.pdf)（本人）, [GAME Watch 講演レポート](https://game.watch.impress.co.jp/docs/news/20150307_691725.html)（記事・日本語） |
| **Hi-Fi RUSH**（2023, Tango Gameworks, UE4改造） | 本人（GDC）: キャラも環境も、世界全体をトゥーンで描いた。60fps のリズムアクション。記事: チームに経験者はいなかった。ジェットセットラジオ・大神・ビューティフル ジョーに影響を受けたセル調 | [GDC Vault: 3D Toon Rendering in Hi-Fi RUSH](https://gdcvault.com/play/1034251/3D-Toon-Rendering-in-Hi)（本人）, [80.lv: The making of Hi-Fi Rush's 3D toon rendering](https://80.lv/articles/the-making-of-hi-fi-rush-s-3d-toon-rendering-style)（記事）, [Unreal Engine インタビュー（日本語）](https://www.unrealengine.com/ja/developer-interviews/hi-fi-rush-was-inspired-by-shaun-of-the-dead-and-futurama)（本人） |
| **原神**（2020, miHoYo, Unity） | 本人（Unite / Unity道場）: PC・家庭用とスマホで別の描き方を持ち、全体は物理ベースを土台にした「様式化」。影は近くでは細かく、遠くまで届くように。説: 影は手描き、光と影の境目はあまり固くしない | [Unity道場 2021（日本語スライド）](https://www.docswell.com/s/UnityJapan/KWRPQ5-210617-unity-dojo20211mihoyozhenzhongyi)（本人）, [GDC Vault: Crafting an Anime Style Open World](https://gdcvault.com/play/1027538/-Genshin-Impact-Crafting-an)（本人）, [ファミ通 GDC 2021 レポート](https://www.famitsu.com/news/202107/23228135.html)（記事） |
| **崩壊3rd**（miHoYo, Unity） | 本人（Unite Tokyo 2018）: アニメ風レンダリングの極意。筆の感じを持たせた複数の段の影（マルチランプ）、質の高い輪郭線 | [Unite Tokyo 2018 スライド（日本語）](https://image.docswell.com/s/UnityJapan/KWLQP5-unite-tokyo-20183rd)（本人） |
| **BLUE PROTOCOL**（バンダイナムコオンライン） | 記事（CEDEC 2021）: 法線を整えてアニメのような影を入れる。リムライト（縁の光）に輪郭の抽出を使う。街の描画を軽くする工夫 | [GameBusiness.jp: CEDEC2021 レポート](https://www.gamebusiness.jp/article/2021/09/13/19051.html)（記事・日本語） |
| **モンスターハンターストーリーズ3**（2026, カプコン, RE ENGINE） | 記事（CEDEC 2026）: 写実向きの RE ENGINE で初めて本格的にアニメ調に挑み、描画の流れそのものまで手を入れた。光と陰影の工夫。キャラは前作より線を細かくしつつセル調を保った | [ファミ通: CEDEC2026 レポート](https://www.famitsu.com/article/202608/83946)（記事）, [CGWORLD: CEDEC2026 MHS3](https://cgworld.jp/article/202609-cedec-mhs3.html)（記事） |
| **グランブルーファンタジー リリンク**（2024, Cygames） | 本人: 写実でもセル調でもなく「グラブルの2Dの絵を3Dで再現」。グラブルらしく見えるなら、写実的でない光もあえて入れた。雲の描き方が大事。記事: 水彩のような見た目は3Dでは難しい。キャラによって絵柄の移し方にむらがある、という評も | [Cygames Magazine: Relink graphics part 1](https://magazine.cygames.co.jp/en/?p=3373)（本人）, [Part 2](https://magazine.cygames.co.jp/en/?p=3379)（本人）, [Creative Bloq: Yasuyuki Kaji interview](https://www.creativebloq.com/features/granblue-fantasy-relink-yasuyuki-kaji-interview)（本人） |
| **学園アイドルマスター**（2024, QualiArts） | 記事（CEDEC 2024）: スマホの3Dキャラと背景を細部までこだわる（肌の揺れ・毛先・衣装・鼻の描き方など） | [GAME Watch: CEDEC2024 学マス](https://game.watch.impress.co.jp/docs/kikaku/1617554.html)（記事・日本語） |
| **ブルーアーカイブ**（NEXON Games） | 記事（講演）: 3D背景チームの作り方（Substance Designer で背景を効率よく作る）。キャラのトゥーンの話ではない | [4Gamer: ブルアカ 3D背景モデリング](https://www.4gamer.net/games/519/G051983/20250625011/)（記事・日本語） |

---

## 3. セル調の元祖・定番 [CLASSIC]

| ゲーム | 分かっていること | 出典 |
|---|---|---|
| **ジェットセットラジオ**（2000, スミルビット） | 記事: 最初のセル調の3Dゲームとされる。SF・ファンタジーばかりのセガ作品への不満から生まれた。上田竜太さんの絵柄は、遊びより先に決まった。渋谷・新宿を撮影して参考にした | [Wikipedia](https://en.wikipedia.org/wiki/Jet_Set_Radio)（記事）, [USgamer: 20 years later](https://www.usgamer.net/articles/jet-set-radio-20-year-anniversary-sega-feature-interview)（本人） |
| **ゼルダの伝説 風のタクト**（2002, 任天堂） | 本人: 最初は写実で考えていたが、アーティストがトゥーンのリンクを提案し、チームが乗った。技術を使い切って世界の自然な特徴を引き出すことが大事と考えた。**記事**: 宮本さんは最初は驚いた（「顔をしかめた」とも伝わる） | [社長が訊く 風のタクト HD](https://iwataasks.nintendo.com/interviews/wiiu/wind-waker/0/0)（本人）, [GamesRadar: The making of Wind Waker](https://gamesradar.com/the-making-of-the-legend-of-zelda-the-wind-waker)（記事） |
| **Bomb Rush Cyberfunk**（2023, Team Reptile） | 記事: ジェットセットラジオへの敬意をこめた、角ばったセル調とくっきりした輪郭線、ネオン。**異論**: 真似にとどまり、元の作品のような必然がない、という評も | [Slant Magazine review](https://slantmagazine.com/games/bomb-rush-cyberfunk-review)（記事・批判）, [GamingBolt review](https://gamingbolt.com/bomb-rush-cyberfunk-review-funk-on-the-roll)（記事） |

日本語の講演で、ゼンレスゾーンゼロ・ブルーアーカイブのキャラのトゥーン表現を扱ったものは、今回の検索では見つからなかった。Minishoot' Adventures は候補にしたが、2Dの作品だったので外した。

---

## 4. 繰り返し出てきたこと [FINDINGS]

複数の作品・講演で共通していた点。**提案で、何も決まっていない**。採用するかはユーザーに聞く。

1. **色は遊びの役割から決める（P2）**: スプラトゥーンは2チームのインクの色から組み立てた。→ このゲームなら4人のプレイヤー色が主役で、床・歯・障害物の色はその4色を邪魔しない範囲に抑える（既存資料 `[OURS]` 1「明るさの役割」と同じ方向）。
2. **遠くから読める「塊」で作る（P1・P8）**: Overcooked は明るいべた塗りの形、ブロスタはくっきりした目印の形、TUNIC は少ない頂点。→ 見下ろしで小さく映るこのゲームでは、細かい模様より形の塊と明るさの差が効く。
3. **セル調は面の乱れが目立つので、法線を整える（P5）**: Guilty Gear Xrd は「明るいか暗いかだけなので、小さな乱れが全部見える」と言い、法線マップを使わず影を画家が決めた。BLUE PROTOCOL も法線を整えてアニメの影を入れる。→ 歯のコアのトゥーンの影がまだらに見えるときは、テクスチャより法線（Maya 側か取り込み後）を疑う。
4. **光は「見せたい所に」嘘をついてよい（P4）**: Guilty Gear は顔に専用の光、リリンクは写実でない光をあえて入れる、原神は手描きの影。→ 歯のコアの縁の光（決定済み）のように、主役にだけ専用の光を当てる考え方は、この流れと同じ。
5. **世界を同じ描き方で揃えるか、わざと分けるか**: Hi-Fi RUSH は世界全体をトゥーンで揃えた。Death's Door は環境を自然な光、キャラを控えめなトゥーンにして分けた。→ このゲームは歯・障害物がトゥーンなので、敵の板（2D）も同じ影の段数・縁の描き方に寄せると揃う（`2D_with_3D_Backgrounds_References.md` の `[FINDINGS]` 4 と同じ）。
6. **輪郭線と縁の光（P1）**: 崩壊3rd の質の高い輪郭線、BLUE PROTOCOL のリムライト、Bomb Rush のくっきりした線。→ 敵の群れの縁（実装済みの「縁」の質感）と、プレイヤーの攻撃の白い芯は、この輪郭の考え方で強弱を決められる。
7. **形が笑いと「ポップさ」を作る（P6）**: Fall Guys は「一番笑える形」から短い足・丸い体にし、転ぶ動きを残した。→ 菌のぷるぷる・潰れ（実装済み）のように、形と動きの誇張でポップさを出すのは、色を足さずにできる方法（要素を増やさない決定 2026-11-04 とも合う）。
8. **アニメはコマを抜くと「決め」が出る（P6）**: Guilty Gear Xrd はキーだけのコマ打ち。→ 既存資料 `[OURS]` 5（演出だけ1秒12〜24コマ）と同じ方向。
