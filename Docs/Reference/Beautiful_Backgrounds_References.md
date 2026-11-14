# 背景が綺麗と言われる2Dゲーム リファレンス（2026-11-13）

「背景が綺麗」と評判の2Dゲームを、レビュー・開発者インタビュー・一覧記事からネットで集めたもの。`Beautiful_2D_Analysis.md`（2026-11-09、20本）が「綺麗の理由」をまとめた土台で、こちらは**背景に絞って**その土台に載っていない作品を足した追加分。ネットからの参考集めは、このときユーザーが頼んだので行った（それ以外のときは `Beautiful_2D_Analysis.md` の `[RULES]` のとおり勝手に開かない）。

## 使い方・守ること [RULES]

- **画面は人間が見る**。下のリンクは人間が自分で開いて見るためのもの。AI（Claude・画像生成）には画面写真・アートを渡さない。作品名・作家名で画風を寄せる指示もしない（CLAUDE.md、`UI_Design_References.md` の `[SAFETY]`）。
- 根拠の印: **本人** = 開発者自身の言葉／**記事** = 記者のレビュー・まとめ／**説** = ファン・学生の解説（裏付けが弱い）。
- 括弧の P1〜P8 は `Beautiful_2D_Analysis.md` の8つの理由（P1 読める／P2 色を絞る／P3 色の流れ／P4 光と空気／P5 手の跡／P6 動きのリズム／P7 止めても絵／P8 引き算）。
- 既存資料にある20本（Hollow Knight・Ori・Hades・GRIS・Hyper Light Drifter・Celeste・Cuphead・Octopath・Sea of Stars・Eastward・Dead Cells・Vanillaware・Alto・Limbo/Inside・Spiritfarer・Rayman・GG Strive・Lumino City・Chuchel・Knights and Bikes）はここでは繰り返さない。`Beautiful_2D_Analysis.md` の `[GAMES]` 節を見る。

---

## 1. このゲームに近い「見下ろし」で背景が褒められた作品 [TOPDOWN]

このゲーム（見下ろし・4人・敵数千体）に一番関係が深い。既存資料の Hades・Hyper Light Drifter・Eastward・Sea of Stars と合わせて見る。

| ゲーム | 見え方 | 背景が褒められた点 | 効く理由 | 出典 |
|---|---|---|---|---|
| **Children of Morta**（2019, Dead Mage） | 高解像度ドット絵＋現代の光 | 本人: 背景は「細かいドット絵と、場所ごとに違う色の決まり」。それに現代の光・ポストプロセス・なめらかなアニメを重ねて他のドット絵と差をつけた。ドットのアニメを約4倍に伸ばして HD にし、光を普通の2Dと同じ解像度で当てた。描き手は2人で高解像度の絵は大仕事だった | P2 P4 P5 | [Game Developer: Postmortem](https://gamedeveloper.com/design/postmortem-children-of-morta)（本人）, [TechRaptor: How Dead Mage created the art](https://techraptor.net/content/how-dead-mage-created-the-art-for-children-of-morta)（記事）, [Indiegraze interview](https://indiegraze.com/?p=260)（本人） |
| **UNSIGHTED**（2021, Studio Pixel Punk・2人） | ドット絵・サイバーパンク・見下ろし | 記事: 「環境と光の細かさがとにかく美しい」「色の決まりが魅力的で、荒廃した世界を生かしている」。**注意**: 見下ろし＋太いドットで高さ・距離が読みにくいという批判も（このゲームでも敵の高さ・奥行きの読みは同じ問題になりうる） | P2 P4 ／ 注意 P1 | [Way Too Many Games](https://waytoomany.games/2021/10/11/review-unsighted/), [RPGFan](https://www.rpgfan.com/review/unsighted/), [Game Informer](https://gameinformer.com/review/unsighted/unsighted-review-the-ultimate-beat-the-clock-challenge)（いずれも記事） |
| **CrossCode**（2018, Radical Fish） | 16bit 風ドット絵・見下ろし | 記事: 16bit の見た目のまま現代の手触り。**注意**: 本人が「奥行きの読みづらさが多くの人に指摘された」と認め、次作では「ドット絵＋3D で地形の見え方を良くする」方向に変えた。見下ろしの背景は綺麗でも、高さが読めないと遊びに響く見本 | 注意 P1 | [Siliconera: six years](https://www.siliconera.com/crosscode-developer-reflects-rpgs-six-years-evolution/)（本人）, [The Avocado interview](https://the-avocado.org/2021/05/07/indie-developer-interview-radical-fish)（本人）, [itch.io interview](https://itch.io/blog/53052/how-crosscode-modernizes-classic-design-an-interview-with-felix-of-radical-fish)（本人） |
| **Moonlighter**（2018, Digital Sun） | ドット絵・見下ろし | 記事: 「最初から最後まで綺麗。ドット絵は可愛いだけでなく発想が豊かで、技術的にもよくできている」。背景の詳しい分析記事は見つからなかった | P2 P7 | [Nintendo Life: best pixel art Switch games](https://www.nintendolife.com/guides/best-pixel-art-switch-games)（記事） |
| **Cult of the Lamb**（2022, Massive Monster） | 平らな手描きイラスト・紙のような質感・見下ろし | 記事: 「紙のような仕上げが美しい」「平らなイラスト調で、顔は誇張」。本人（アートディレクター）: 可愛さと悪魔的なものを意図して混ぜた。可愛い見た目だからこそ残酷さが際立つ、という対比の見本 | P2 P5 P8 | [Windows Central review](https://windowscentral.com/gaming/pc-gaming/cult-of-the-lamb-review)（記事）, [Inverse: concept art interview](https://inverse.com/gaming/cult-of-the-lamb-concept-art-interview-massive-monster/amp)（本人）, [Push Square](https://www.pushsquare.com/reviews/ps5/cult-of-the-lamb)（記事） |
| **Terra Nil**（2023, Free Lives） | 2D スプライトの斜め見下ろし（回転不可） | 記事: 「手描き風の絵が画面を生き返らせる」「絵画調ではないが色を強調する作りで、緑を戻すほど環境が映える」。荒地→自然へ**色が変わっていく**のが遊びの中心。ステージの色の流れ（P3）の見本 | P2 P3 | [Shacknews review](https://shacknews.com/article/134777/terra-nil-review-nature-appreciation?amphtml=1), [LadiesGamers review](https://ladiesgamers.com/terra-nil-review/), [Inverse](https://www.inverse.com/gaming/terra-nil-nintendo-switch-game-recommendation)（いずれも記事） |
| **ドラゴンクエストIII HD-2D**（2024, Square Enix） | ドット絵＋3D 背景（HD-2D）・斜め見下ろし | 記事: ドット絵を残しつつ3D 背景とエフェクトで見せる。夜の町では家の灯りがオレンジ、宿だけピンク、と**光の色で場所の役割を分ける**。完成度は賛否両論 | P2 P4 | [Inside Games](https://www.inside-games.jp/article/2025/01/03/162966.html), [Game*Spark 先行体験](https://www.gamespark.jp/article/2024/06/19/142524.html), [Game*Spark 特集（賛否）](https://www.gamespark.jp/article/2024/12/01/147406.html)（いずれも記事） |

---

## 2. 手描き・ペイント系の背景 [PAINTED]

| ゲーム | 見え方 | 背景が褒められた点 | 効く理由 | 出典 |
|---|---|---|---|---|
| **Hoa**（2021, Skrollcat Studio） | 手描きの水彩風・横スクロール | 記事: 「層ごとに手で描かれ、丁寧」「暖色で、奥まで細かい背景」。ジブリ風と繰り返し言われる。**異論**: 背景はやや地味、遊びは薄いという評も。Unity で作られた | P4 P5 | [80.lv: Hoa, Ghibli style in Unity](https://80.lv/articles/hoa-using-ghibli-style-for-an-adventure-in-unity-004adk)（本人）, [ScreenHub review](https://www.screenhub.com.au/news/reviews/game-review-hoa-is-a-gorgeous-slice-of-escapism-1472330/), [Shacknews review](https://shacknews.com/article/126275/hoa-review-spirited-away)（記事） |
| **Planet of Lana / II**（2023・2025, Wishfully） | 手描きの絵画・横スクロール | 記事: 「手前の物は細かく、奥の背景は大きくゆるい筆で」（**近くは密・遠くは粗**の見本）。「手描きに見えるのにリアルに動く」「無駄な細部で埋めていない」。II は「絵が動いているよう」 | P4 P5 P8 | [GamesRadar review](https://gamesradar.com/planet-of-lana-review), [The Sixth Axis preview](https://www.thesixthaxis.com/2022/09/01/planet-of-lana-preview-a-gorgeous-side-scrolling-adventure/), [GamingBolt review](https://gamingbolt.com/planet-of-lana-review-spirited-away), [Creative Bloq: Planet of Lana 2](https://www.creativebloq.com/entertainment/gaming/planet-of-lana-2-review-an-emotional-hand-painted-sequel-worth-the-wait)（いずれも記事） |
| **Neva**（2024, Nomada Studio） | 平らな色面の手描き・横スクロール（GRIS の次作） | 記事: 「色と影の使い方が背景とキャラに命を与える」「視差（パララックス）背景の使い方が抜群で、単純な場面に奥行きが出る」「季節（秋）の色の対比が目を引く」「場所ごとに光と色で雰囲気を分ける」。GRIS の水彩から平らな色面に変えた | P2 P3 P4 | [Windows Central review](https://windowscentral.com/gaming/neva-review), [Push Square review](https://pushsquare.com/reviews/ps5/neva), [GamingTrend review](https://gamingtrend.com/feature/reviews/neva-review-an-ethereal-and-haunting-journey-through-life-and-death/)（いずれも記事） |
| **Child of Light**（2014, Ubisoft Montreal） | 手描き＋水彩仕上げ（UbiArt） | 記事: 絵を手で描いて水彩の仕上げ。アートディレクター Thomas Rollus が2Dと3Dを融合（詳細は記事途中で切れている）。Rayman と同じエンジン | P5 | [VGC: making of](https://videogameschronicle.com/features/child-of-light-making-of), [Xbox Wire: The Fantastic Art](https://news.xbox.com/en-us/?p=4396), [PlayStation Blog: behind the scenes](https://blog.eu.playstation.com/2014/04/30/behind-scenes-stunning-rpg-child-light-today/)（記事） |
| **Forgotton Anne**（2018, ThroughLine Games） | アニメ映画風の手描き（Unity） | 記事: 「環境に写実性と細部を持ち込み、2Dなのに奥行きを感じさせる」。Unity の2D事例 | P4 P5 | [Unity case study](https://www.unity.com/case-study/forgotton-anne), [Game Developer: Road to the IGF](https://www.gamedeveloper.com/game-platforms/road-to-the-igf-throughline-games-i-forgotton-anne-i-)（本人）, [ThroughLine: technical wizardry](https://www.throughlinegames.com/amp/2017/11/10/immersion-through-technical-wizardry)（本人） |
| **Nine Sols**（2024, Red Candle Games） | 手描き・東洋×SF（タオパンク） | 記事: 「どの場所も繊細に手描きで、見て歩くのが楽しい」「デスクトップの壁紙にしたい場面がいくつもある」。**異論**: 一部の場所は個性が薄いという声 | P5 P7 | [Prima Games review](https://primagames.com/?p=266102), [Nintendo World Report](https://nintendoworldreport.com/review/69286), [God is a Geek](https://godisageek.com/reviews/nine-sols-review/)（記事） |
| **Hollow Knight: Silksong**（2025, Team Cherry） | 手描き（前作の続き） | 記事: 「背景と建築が美しいアニメで描かれ、純粋な絵として息をのむ」「世界全体が巨大な動く一枚のイラスト」「UE5 作品の均一な見た目と違う個性」。Greymoor の背景・Citadel の建築が名指しで褒められる | P4 P5 P7 | [GamingBolt review](https://gamingbolt.com/hollow-knight-silksong-review-i-venture-forth-to-hunt), [Niche Gamer](https://nichegamer.com/reviews/hollow-knight-silksong-review), [VGC（3/5 でも背景は称賛）](https://www.videogameschronicle.com/review/hollow-knight-silksong/)（記事） |

---

## 3. ドット絵の背景 [PIXEL]

| ゲーム | 見え方 | 背景が褒められた点 | 効く理由 | 出典 |
|---|---|---|---|---|
| **Rain World**（2017, Videocult） | 細密ドット絵・荒廃した産業世界 | 記事: 「背景が驚くほど細かい」「物語は背景（崩れた文明の巨大構造物）で語られる」。**注意**: 背景を横切る影のせいで細部が読みづらい、との指摘（背景が主役を食う例）。遊びの難しさは批判された | P5 P7 ／ 注意 P1 | [GameSpot review](https://www.gamespot.com/reviews/rain-world-review/1900-6416648/)（記事）, [Heterotopias: Symbols & Strata](https://www.heterotopiaszine.com/2017/04/04/symbols-strata-rain-world/)（説・考察）, [Wikipedia](https://en.wikipedia.org/wiki/Rain_World) |
| **Blasphemous / 2**（2019・2023, The Game Kitchen） | 純粋なドット絵（シェーダー・3D を使わない方針） | 本人: 「最初から純粋主義で、シェーダーや3D要素を避けた」。記事: 背景はセビリアの建築（崩れた大聖堂・彫像・迷路のような構造）が元で「背景が美しく、高い技量」「2 はさらに細かく色も増えた」。開発日誌 11 話あり | P2 P5 P7 | [Hollywood Reporter: purist approach](https://www.hollywoodreporter.com/heat-vision/blasphemous-designer-purist-approach-games-pixel-art-style-1238370)（本人）, [bit-tech review](https://bit-tech.net/reviews/gaming/pc/blasphemous-review/1/), [Film Stories: Blasphemous 2](https://filmstories.co.uk/reviews/blasphemous-2-review-blood-sweat-and-pixels/)（記事）, [GoNintendo: Blasphemous II dev diary](https://www.gonintendo.com/contents/26553-blasphemous-ii-dev-diary-4-the-invisible-hand)（本人・映像） |
| **Momodora: Moonlit Farewell**（2024, Bombservice） | ドット絵＋光の演出 | 記事: 「場所ごとに光の演出と粒がふんだんに盛られる」「月光の神秘的な光が雰囲気を作る」「前作と差をつけたのは色の決まり」。**注意**: 敵が背景に溶けることがある、という批判（このゲームの P1 と同じ問題） | P2 P4 ／ 注意 P1 | [Nintendo Life review](https://www.nintendolife.com/reviews/switch-eshop/momodora-moonlit-farewell), [GamingTrend](https://gamingtrend.com/reviews/momodora-moonlit-farewell-review-perfectly-pleasant-goodbye), [Destructoid](https://www.destructoid.com/reviews/review-momodora-moonlit-farewell/)（記事） |
| **Owlboy**（2016, D-Pad Studio） | 高密度ドット絵 | 記事: 「背景ほど小さく多いドットで、どこを見ても一点物の絵に見える（実際は一部タイル）」「雲と木の細かさ」。見た目を好まない評者もいる | P5 | [Push Square review](https://pushsquare.com/reviews/ps4/owlboy)（記事）, [Galaxus: 13 most beautiful pixel games](https://www.galaxus.ch/en/page/the-13-most-beautiful-pixel-games-of-the-modern-age-40712)（記事・機械翻訳） |
| **The Last Night**（未発売, Odd Tales） | ドット絵のテクスチャを貼った低ポリ3D（2.5D） | 記事: 低ポリ3Dにドット絵テクスチャを貼って描き、カメラの焦点距離で「平らでまとまった一枚」に見せる。光は映画の参考板で決めた（説）。自社の 2.5D ドット絵ツールと 2024 年の技術講演あり | P4 | [80.lv: pixel art cyberpunk](https://80.lv/articles/the-last-night-pixel-art-cyberpunk-open-world)（記事）, [Odd Tales blog](https://oddtales.net/blog)（本人）, [Odd Tales GitHub](https://github.com/oddtales)（本人） |
| **Dead Cells**（既存資料に有り） | 3D→ドット絵、背景は手描きを3Dの光に通す | 補足: 80.lv のインタビューで「手描きの背景を3Dの光のシステムに入れた」と語っている | P4 P5 | [80.lv interview](https://80.lv/articles/interview-with-the-developers-of-dead-cells)（本人） |

---

## 4. 格闘ゲームの背景（動く背景・層の重ね方の見本） [FIGHTING]

格闘ゲームは「背景だけが動き、主役は手前」という構造が、このゲームの「背景の上に大群と攻撃」に似ている。背景が主役を邪魔しない作り方の見本。

| ゲーム | 褒められた点 | 出典 |
|---|---|---|
| **Street Fighter III: 3rd Strike**（1999, Capcom） | 「2Dゲームで最も美しい部類」「細かい背景と流れるようなアニメ」。Kousyu Street など 90 年代の名ステージとして定番 | [Inverse: 25 years later](https://www.inverse.com/gaming/street-fighter-3-25-years-later-iconic-fighting-game-finally-gets-some-respect), [Den of Geek: best 90s stages](https://www.denofgeek.com/games/the-best-fighting-game-stages-of-the-90s/)（記事） |
| **Garou: Mark of the Wolves**（1999, SNK） | 「SNK の背景は塊が大きく、明るく、細かい」。3rd Strike に迫る絵の質 | [Den of Geek](https://www.denofgeek.com/games/the-best-fighting-game-stages-of-the-90s/)（記事）, [insertcredit forum](https://forums.insertcredit.com/t/favorite-fighting-game-stages/2994)（説） |
| 一覧 | 格闘ゲームの背景 27 選（戦っていない背景だけを集めた記事） | [GamesRadar: 27 most amazing fighting game backgrounds](https://www.gamesradar.com/27-most-amazing-fighting-game-backgrounds/)（記事） |

---

## 5. 作品選びに使った一覧記事 [LISTS]

- [Plitch: 12 games with amazing art styles](https://blogs.plitch.com/en/blog/12-games-with-amazing-art-styles)（Hollow Knight の手描き環境、Ori の光る森など）
- [Lifehacker AU（Vanillaware・十三機兵の森）](https://www.lifehacker.com.au/?p=1158967)
- [EarlyGame: 20 most beautiful hand-drawn animated games](https://earlygame.com/guides/gaming/the-20-most-beautiful-hand-drawn-animated-video-games)
- [EarlyGame: 20 stunning pixel art games](https://earlygame.com/guides/gaming/20-stunning-pixel-art-video-games-you-need-to-play)
- [PC Gamer: ten gorgeous adventure game scenes from the pixel art era](https://www.pcgamer.com/uk/ten-of-the-best-classic-adventure-game-scenes)（90年代アドベンチャーの背景）
- [Nintendo Life: best pixel art Switch games](https://www.nintendolife.com/guides/best-pixel-art-switch-games)
- [Galaxus: 13 most beautiful pixel games](https://www.galaxus.ch/en/page/the-13-most-beautiful-pixel-games-of-the-modern-age-40712)（機械翻訳・読者の異論あり）
- [Gamepressure: 15 indie games with best graphics](https://www.gamepressure.com/editorials/15-indie-video-games-with-best-graphics/z65a0-8)
- [TV Tropes: Scenery Porn / Video Games](https://tvtropes.org/pmwiki/pmwiki.php/SceneryPorn/VideoGames)（網羅的だが説。作品名を拾う用）
- [Giant Bomb user list: beautiful 2D](https://giantbomb.com/user-lists/uid/3378/beautiful-2d)（Trine・Aquaria・Outland など。説）

日本語でまとまった「背景美術のランキング」記事は今回見つからなかった（検索結果は素材サイトが多かった）。ENDER LILIES の開発者インタビュー（[GameBusiness.jp](https://www.gamebusiness.jp/article/2021/09/06/19029.html)）は世界観とレベルデザイン中心で、背景の作り方には触れていない。

---

## 6. 背景について、繰り返し出てきたこと [FINDINGS]

今回集めた分（上の表）で、複数の作品に共通して出てきた点。`Beautiful_2D_Analysis.md` の P1〜P8 に当てはめた。**提案で、何も決まっていない**。

1. **近くは密、遠くは粗（P4・P8）**: Planet of Lana は手前の物を細かく、奥の背景を大きくゆるい筆で描く。Owlboy は背景ほど小さなドットで密度を変える。奥行きは「描き込みの密度の差」で出る。→ このゲームでは、床（主役が動く層）を一番おとなしくし、画面の縁や外周の装飾ほど描き込みを変える、という使い方が考えられる。
2. **色の決まりを場所ごとに決め、光の色で役割を分ける（P2・P3）**: Children of Morta は背景ごとの色の決まり、DQ3 HD-2D は宿だけ灯りの色を変える、Terra Nil は進行で色が変わる、Neva は季節で色の対比。→ ステージごとの色の決まり＋ウェーブで変える流れ（既存資料 `[OURS]` 2）と同じ方向。
3. **背景が主役を食う失敗は見下ろし・ドット絵で起きやすい（P1）**: Momodora（敵が背景に溶ける）、Rain World（背景を横切る影で読みにくい）、UNSIGHTED・CrossCode（見下ろしで高さ・距離が読めない）。背景が褒められた作品でも、この批判がついている。→ このゲームは敵数千体なので、背景の明るさ・彩度・動きを抑える決まり（既存資料 `[OURS]` 1）が最優先。
4. **手描きの光を現代の光と混ぜる（P4・P5）**: Children of Morta・Dead Cells は手描きの絵を光のシステムに通し、The Last Night はドット絵テクスチャを3Dに貼る。Blasphemous は逆に純粋ドット絵に徹して評価された。→ どちらでも綺麗になる。大事なのは「光が当たる方向と止まる場所」が一貫していること。
5. **背景で物語を語る（P7）**: Rain World（崩れた文明）、Blasphemous（セビリアの建築）、Silksong（場所ごとに建築が違う）。背景の細部に「ここは何の場所か」が入っている。→ 口の中のステージなら、歯・歯茎・食べかす・磨き残しなど「ここは口の中」と分かる細部を背景に置く（要素を増やさない決定 2026-11-04 の範囲で）。
6. **動く背景は手前を邪魔しない範囲で（P6）**: 格闘ゲームの背景は人や物が動くが、主役より暗く・遅く・小さく動く。→ 環境の粒が敵に見えないように、という決定（2026-11-04）と同じ考え。
