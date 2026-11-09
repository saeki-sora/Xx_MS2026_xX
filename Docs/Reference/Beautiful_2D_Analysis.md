# 美しい2Dの解剖（見た目の判断の土台・AI 向け）

「見た目が綺麗」と言われる2Dゲーム20本について、開発者のインタビュー・講演・技術記事から「何を考えて、どう作ったか」を集め、綺麗に感じる理由を8つにまとめた資料。2026-11-09 に作成した（ネットからの参考集めは、このときだけユーザーが許可）。人間向けの見やすいページは https://claude.ai/artifact/SpwJJhaNzUrRob9KV749rw にある（自作の付け外し実験の図付き）。

---

## 0. 読む前に（AI 向けのルール） [READ-ME]

- **いつ読むか**: 見た目に少しでも関わる作業をユーザーに頼まれたら、作業の最初に読む。シェーダー・エフェクト・色・光・カメラ・UI の見た目・アニメ・ステージの見え方・キャラの絵の見やすさ・スキル演出・見本ページ（比較ページ）づくりなどが当てはまる。CLAUDE.md の「見た目の作業の共通ルール」から案内している。
- **読み方**: 丸ごと読まなくてよい。まず `[CHECK]`（作業前後のチェック表）を読み、必要な理由の節だけを `[TAG]` で `Grep` して `Read` する。
  - `[CHECK]` チェック表 ／ `[P1]`〜`[P8]` 8つの理由 ／ `[ANTI]` 汚く見える原因 ／ `[OURS]` このゲームでの使い方 ／ `[GAMES]` 集めたゲームと出典 ／ `[AWARDS]` 賞の傾向 ／ `[RULES]` 守ること
- **どう使うか**: 提案・比較ページ・実装のときに、どの理由（P1〜P8）に効くのかを考え、必要ならユーザーへの説明でも「P4（光が当たる）に効く」のように使う。ただし、ユーザーが決めたこと（メモリ・CLAUDE.md・各資料の「ユーザー決定」）がいつも優先。この資料は判断の材料で、決まりではない。
- **更新**: ユーザーに頼まれたときだけ直す（CLAUDE.md の資料の扱いと同じ）。

---

## 1. 作業の前と後に見るチェック表 [CHECK]

作る前に「どれを狙うか」を決め、作った後に「壊していないか」を見る。土台（上の行）から順に大事。下の行は上の行が崩れていると効かない。

| # | 問い | 見るところ |
|---|---|---|
| P1 | 主役（自分の攻撃・自分のキャラ・敵）が背景に埋もれずに読めるか。明るさの差とシルエットで区別できるか | 4人同時・敵数千体の一番うるさい場面で確かめる |
| P2 | 使う色が絞れているか。そのステージ・その演出の「色の決まり」の中に収まっているか | 新しい色を足すなら、何かの色を減らせないか |
| P3 | 時間・場面の流れで色や明るさが変わるか（ずっと同じで目が慣れていないか） | ウェーブ・残り時間・スキル発動の前後 |
| P4 | 光が物に「当たって」止まるか。照らされる側と影の側があるか。奥行き（遠くが霞む・手前と奥）があるか | 障害物の裏に光や演出が回り込んでいないか（赤スキルの決まりと同じ） |
| P5 | 手で描いたような揺らぎ・質感が残っているか。つるっと均一な「機械っぽさ」「AIっぽさ」になっていないか | 自動生成の法線・ノイズだけで済ませていないか |
| P6 | 動きに止めとメリハリがあるか。全部が同じ速さでぬるぬる動いていないか | 演出のアニメのコマ数（操作の反応とは分けて考えてよい） |
| P7 | どの瞬間で止めても絵として成り立つか | 録画しながらプレイの映像を数か所で止めて並べる |
| P8 | 足しすぎていないか。上限（色数・光る物の数・粒の数）を決めたか | 「全部が光る」「全部が派手」になっていないか |

---

## 2. 8つの理由

### P1. まず「読める」 [P1]
- **中身**: 主役と背景の明るさをはっきり分け、形（シルエット）だけで何か分かるようにする。
- **なぜ綺麗に感じるか**: 目がどこを見ればいいか迷わないと、画面全体が「整っている」と感じる。綺麗さの半分は「迷わなさ」。背景と主役が同じくらい目立つと、絵が上手でもごちゃごちゃして見える。
- **根拠**:
  - Hyper Light Drifter: 作者は何より色とシルエットを考えるのが好きだったと話している。低い解像度でも見やすいことを最優先にし、キャラを見失わないようにした（本人）
  - Ori and the Will of the Wisps: 構図の線と明暗の差で目を導く。プレイヤーが見る所は細かく、隅は粗くてよいと割り切った（記事）
  - Eastward: 固定カメラにして、手前が奥を隠さないよう街に高さを付けた（本人）
  - Hollow Knight: キャラは単純な形と少ない色、背景のほうが細かい（記事）

### P2. 色を絞って、場所ごとに決める [P2]
- **中身**: エリア（ステージ）ごとに使う色を決め、色数を減らす。差し色は1つ。
- **なぜ綺麗に感じるか**: 色が揃うと、ばらばらに描いた物も同じ光の下にあるように見え、一枚の絵としてまとまる。色数の多さは豊かさにならず、安っぽさにつながりやすい。場所を覚えやすくもなる。
- **根拠**: Hyper Light Drifter（環境ごとに目印が目立つ配色、本人）／Ori WotW（後半は東西南北のエリアごとに配色、記事）／Hollow Knight（エリアごとに色を限ったと言われる、説）／Limbo（白黒まで削った、記事）／Alto's Odyssey（場所ごとに色を絞る、説）

### P3. 色の流れを物語にする [P3]
- **中身**: ゲーム全体・試合全体の色の移り変わり（カラースクリプト）を先に設計する。
- **なぜ綺麗に感じるか**: 同じ色を見続けると目が慣れて何も感じなくなる。色が変わる瞬間に「景色が変わった」と感じ、それが気持ちの動きになる。
- **根拠**: Hades（早い段階で各エリアの見本絵を並べて色の流れを確かめた、本人）／GRIS（色のない世界に色が戻る、本人）／Spiritfarer（カラースクリプトを道しるべにした、本人）／Alto's Odyssey（テーマに合わせて前作より明るい色、本人）

### P4. 光と空気で奥行きを出す [P4]
- **中身**: 光が物に当たって明るくなる、影ができる、遠くが霞む、という「空間の証拠」を足す。
- **なぜ綺麗に感じるか**: 光が壁に当たって止まる・物の片側だけ照らされる、という反応があると、脳は「そこに空間と物がある」と受け取る。平らな絵が「世界」に変わる。
- **根拠**:
  - Ori WotW: 自動の法線マップは安っぽく見えるとして、約7,000の素材に約3万枚の光の当たり方の絵を手で描いた（記事）
  - Octopath Traveler: ドット絵に光を描き込むだけでは足りず、3Dの背景と光を足して奥行きが出た（本人）
  - Sea of Stars: 動く光が企画の出発点で、素材も手順も光の都合に合わせた（記事）
  - Eastward: ドット絵を部品ごとに3Dに置き、光に反応する凹凸の絵を手で描いた。霧と光の筋も手作業（本人）
  - Celeste: 崖を登るゲームなので、光は壁に当たって止まらなければならない、として作り直した（本人）
  - Hollow Knight: 反対の例。複雑な3Dの光は使わず、柔らかい半透明の形で光を表した（記事）→ 技術は必須ではない。意図が要る

### P5. 人の手の跡を残す [P5]
- **中身**: 効率を落としてでも「描いた感じ」を残す。均一な自動の質感を避ける。
- **なぜ綺麗に感じるか**: 手で描いた線や色の少しずつ不ぞろいな揺らぎを、目は情報として受け取り、温かさや作り手を感じる。均一すぎる質感はプラスチックのように見える（「AIっぽさ」の正体の1つ）。
- **根拠**: Ori WotW（新しい人が何度も自動化しようとしたが手描きをやめなかった、記事）／Cuphead（12万枚以上を手描き、記事）／Vanillaware（原画を倍の大きさで描いて縮める、本人）／Spiritfarer（紙やセルの線に近いざらつきのブラシ、本人）／Hollow Knight（スケッチをそのまま取り込む、記事）／Rayman Origins（手描きの原画をそのままゲームに入れる仕組み、記事）

### P6. 動きのリズムを選ぶ [P6]
- **中身**: 何コマで動かすかを決め、止めと動きのメリハリを作る。操作の反応（ゲームの速さ）とアニメの速さは分けてよい。
- **なぜ綺麗に感じるか**: 決めのポーズで一瞬止まり、次に大きく動くと、目に形が残る。全部が同じ速さでぬるぬる動くと、どの瞬間も印象に残らない。
- **根拠**: Cuphead（アニメは1秒24コマ、ゲームは1秒60回、本人）／Hades（キャラ・敵のアニメ約94万コマ、演出約3万2千コマ、本人）／Dead Cells（3Dで動かしてドット絵に変換し、動きの多い絵を少人数で作った、記事）／Alto's Odyssey（放物線に沿ったなめらかな動きを芯にした、本人）／Guilty Gear Strive（3Dで作り、コマを落とした動きと平たい塗りで2Dに見せる代表例、記事）

### P7. どこで止めても一枚の絵 [P7]
- **中身**: どの瞬間を切り取っても構図（焦点・余白・明暗の配置）が決まっているように作る。音も場面に合わせる。
- **なぜ綺麗に感じるか**: 人は「綺麗なゲーム」をスクリーンショットや配信の一場面で覚える。どの瞬間にも構図があると、記憶に残る絵が増え、「全体が綺麗」という評判になる。
- **根拠**: GRIS（全体が1枚のイラストのように感じられるように、本人）／Octopath Traveler II（いつ撮っても絵として完璧に、思い出の中のドット絵より綺麗に、本人）／Alto's Odyssey（景色そのものが主人公、本人）／GRIS・Neva・Hyper Light Drifter（音楽を場面の気持ちに合わせて鳴らし分け、本人）

### P8. 引き算と制約 [P8]
- **中身**: 描けるものを全部描かず、上限を決めてその中で最大限にする。
- **なぜ綺麗に感じるか**: 情報が多すぎると目が落ち着けない。削られた画面では残ったもの1つ1つに意味が出る。制約があると判断が揃い、統一感が出る。
- **根拠**: Alto's Odyssey（雑音を取り除き、一番単純な要素まで減らす、本人）／Hyper Light Drifter（制約で余計な物を削り、残りを磨いた、本人）／Octopath（解像度や彩度を上げすぎるとドット絵の良さが消える、本人）／Limbo（最小限の見た目・言葉を使わない、記事）／Hades（見た目のアイデアは遊びと物語が固まるまで捨ててよいもの、本人）

---

## 3. 逆に、汚く見える原因 [ANTI]

| 原因 | なぜそうなるか | 避け方（理由） |
|---|---|---|
| 背景と主役が同じ明るさ | 目の止まる場所がない | 背景を落ち着かせ、主役だけ明るく・はっきり（P1） |
| 色が多い・場所ごとの決まりがない | 同じ世界にない感じがする | エリアごとに色を決める（P2） |
| 全部が光る・全部が派手 | 光の意味がなくなり目が疲れる | 光る物を限り、光は当たる物として扱う（P4・P8） |
| つるっと均一な質感 | プラスチックのよう、作り手を感じない | 光や質感を手で描く（P5） |
| 解像度・密度・描き方が混ざる | どれが本当の絵か分からず安っぽい | 上げすぎない加減を決める（P8） |
| どの動きも同じ速さ | 形が目に残らない | コマ数を決め、止めを作る（P6） |

---

## 4. このゲーム（握れ、灼ける前に）での使い方 [OURS]

見下ろし視点、4人同時プレイ、敵数千体。同じ見下ろしの Hades・Hyper Light Drifter・Eastward・Sea of Stars・Octopath の考え方が一番近い。以下は 2026-11-09 時点の**提案で、何も決まっていない**。採用するかはユーザーに聞く。

1. **明るさの役割を決める（P1・P8）**: 床は中くらいの明るさで色を抑え、敵の群れはそれより暗く縁がはっきり、プレイヤーの攻撃だけが一番明るい。4人同時に撃つので「全部が光る」を避ける決まりが特に大事。スキル調整スタジオ（`Tools/スキル調整スタジオを開く`）の付け外し・強さのメーターで、4人分を出して確かめられる。
2. **ステージごとの色の決まりと色の流れ（P2・P3）**: ステージごとに主な3〜4色と差し色1色を決め、ウェーブ・残り時間で少しずつ変える。ステージ背景スタジオの見え方の設定（`StageLookProfile`）にまとめる形が考えられる。
3. **光は「当たる」ものにする（P4）**: 赤スキルの床を照らす光は障害物で止まり、裏に回り込まない（2026-11-09 実装）。同じ考え方を他のスキルや普段のレーザーにも広げると、空間の感じが揃う。
4. **手の跡を光と影に入れる（P5）**: 歯のコアや障害物の光の当たり方を、自動計算だけでなく手で描いた絵で足す（Ori・Eastward のやり方）。トゥーンの見た目と合い、「AIっぽさ」対策（`UI_Design_References.md` の `[ANTI-AI]`）とも同じ方向。
5. **演出のコマ数を決める（P6）**: ゲームは1秒60回のまま、爆発・撃破などの演出アニメだけ1秒12〜24コマにして「決め」を作る。
6. **どこで止めても絵になるか確かめる（P7）**: 録画しながらプレイ（`Packages/com.ms2026.playrecorder/`）の映像を数か所で止めて並べ、どれも絵として成り立つかを見る。

関係するユーザー決定（メモリにもある。ソースで確かめること）: 見た目の強化では要素を増やさない・光の筋は常に出す・霧は使わない・環境の粒は敵に見えないように（2026-11-04）／赤スキルは障害物を貫かず、裏側に何も出さない（2026-11-09）／敵の撃破演出は光る泡（2026-11-09）。

---

## 5. 集めたゲームと出典 [GAMES]

根拠の印: 本人 = 開発者自身の言葉／記事 = 記者のまとめ／説 = ファンの解説など、裏付けが弱い。

| ゲーム | 見え方 | 中心の理由 | 出典 |
|---|---|---|---|
| Hollow Knight（2017, Team Cherry） | 手描き | P1 P2 P4 P5 | [Unity case study](https://unity.com/case-study/hollow-knight), [Wikipedia: Ari Gibson](https://en.wikipedia.org/wiki/Ari_Gibson) |
| Ori（2015/2020, Moon Studios） | 手描きの背景 | P1 P2 P4 P5 | [TheGamer: 30,000 light maps](https://www.thegamer.com/ori-and-the-will-of-the-wisps-artists-hand-painted-30000-light-maps/), [TheGamer: art interview](https://www.thegamer.com/ori-and-the-will-of-the-wisps-art-interview/) |
| Hades（2020, Supergiant） | ペン画・見下ろし | P3 P6 P8 | [MCV/DEVELOP: Behind the art of Hades](https://mcvuk.com/business-news/behind-the-art-of-hades-we-value-artistic-integrity-and-excellence-in-artistic-craft-at-supergiant-however-were-first-and-foremost-a-game-design-lead-team/) |
| GRIS（2018, Nomada Studio） | 水彩 | P3 P5 P7 | [Whatoplay: Conrad Roset](https://whatoplay.com/post/conrad-roset-interview), [VICE: Nomada Studio](https://www.vice.com/en/article/nomada-studio-lets-me-in-to-learn-how-gris-and-neva-deliver-a-beautiful-and-touching-message-interview) |
| Hyper Light Drifter（2016, Heart Machine） | ドット絵・見下ろし | P1 P2 P8 | [Automaton インタビュー](https://automaton-media.com/articles/interviewsjp/20170525-47328/), [Game Informer 2014](https://www.gameinformer.com/games/hyper_light_drifter/b/macintosh/archive/2014/03/14/developer-covers-key-aspects-hyper-light-drifter.aspx) |
| Celeste（2018） | ドット絵 | P4 | [Noel Berry: Remaking Celeste's Lighting](https://noelberry.ca/posts/celeste_lighting) |
| Cuphead（2017, Studio MDHR） | 1930年代アニメ | P5 P6 | [Game Developer: Animating Cuphead](https://gamedeveloper.com/art/animating-i-cuphead-i-the-verve-of-the-1930s-with-the-tech-of-now), [Unity: Cuphead](https://unity.com/madewith/cuphead) |
| Octopath Traveler I・II（HD-2D） | ドット絵＋3D | P4 P7 P8 | [Nintendo Life: picture perfect](https://www.nintendolife.com/news/2022/10/octopath-traveler-ii-devs-aimed-to-make-its-hd-2d-visuals-picture-perfect), [Siliconera: HD-2D](https://www.siliconera.com/project-octopath-traveler-developers-answer-project-started-troubles-developing-hd-2d/) |
| Sea of Stars（2023, Sabotage） | ドット絵・見下ろし | P4 | [MobileSyrup](https://mobilesyrup.com/2023/09/11/sea-of-stars-level-design-sabotage-studio-thierry-boulanger-interview/), [Nintendo Life preview](https://www.nintendolife.com/news/2020/03/hands_on_first_impressions_of_sea_of_stars_the_next_project_from_the_messenger_dev_sabotage) |
| Eastward（2021, Pixpil） | ドット絵＋3Dの光・見下ろし | P1 P4 P5 | [Game Developer: Eastward](https://www.gamedeveloper.com/art/eastward-s-creators-share-insights-on-making-pixel-art-adventures) |
| Dead Cells（2018, Motion Twin） | 3D→ドット絵 | P6 | [80.lv interview](https://80.lv/articles/interview-with-the-developers-of-dead-cells) |
| Vanillaware 作品（Muramasa・Odin Sphere・十三機兵防衛圏） | 手描き | P5 P6 | [Game Developer: King of 2D](https://www.gamedeveloper.com/business/king-of-2d-vanillaware-s-george-kamitani) |
| Alto's Odyssey（2018, Team Alto） | ミニマル | P2 P3 P7 P8 | [Pocket Gamer](https://www.pocketgamer.com/altos-odyssey/interview-a-closer-look-at-team-altos-stunning-sequel-altos-odyssey), [Game Developer: Road to the IGF](https://gamedeveloper.com/business/road-to-the-igf-team-alto-s-i-alto-s-odyssey-i-) |
| Limbo / Inside（2010/2016, Playdead） | 白黒・シルエット | P2 P8 | [Wikipedia: Limbo](https://en.wikipedia.org/wiki/Limbo_(video_game)) |
| Spiritfarer（2020, Thunder Lotus） | 手描きアニメ | P3 P5 | [Toon Boom: Spiritfarer trailer](https://www.toonboom.com/?p=13714) |
| Rayman Origins / Legends | 手描き | P5 P6 | [Wikipedia: UbiArt Framework](https://en.wikipedia.org/wiki/UbiArt_Framework) |
| Guilty Gear Strive（2021） | 3Dで2Dに見せる | P6 | [EarlyGame: unique art styles](https://int.earlygame.com/guides/general-news/30-best-games-with-the-most-unique-art-styles) |
| Lumino City（2014） | 紙工作を撮影 | P5 | [Wikipedia: BAFTA Artistic Achievement](https://en.wikipedia.org/wiki/British_Academy_Games_Award_for_Artistic_Achievement) |
| Chuchel（2018, Amanita Design） | 手描きアニメ | P5 P6 | [Wikipedia: IGF](https://en.wikipedia.org/wiki/Independent_Games_Festival) |
| Knights and Bikes（2019） | 絵本のような手描き | P5 | [Wikipedia: IGF](https://en.wikipedia.org/wiki/Independent_Games_Festival) |

一覧記事（作品選びの参考）: [Digitec: 13 most beautiful pixel games](https://www.digitec.ch/en/page/the-13-most-beautiful-pixel-games-of-the-modern-age-40712)、[GamesRadar: most beautifully animated 2D games](https://www.gamesradar.com/the-7-most-beautifully-animated-2d-games)、[Ulster Univ. 学生ブログ（Alto の解説、説）](https://blogs.ulster.ac.uk/cormacmccann/?p=794)。

---

## 6. 賞から見た傾向 [AWARDS]

美術の賞を取った2D（または2Dの見た目）の作品。描き方そのものが新しい作品、紙工作・段ボール・ノートの手描きなど素材感のある手作りの見た目が多い（P5）。

- BAFTA 美術賞: Rayman Origins（2012）、Lumino City（2015）、Ori and the Blind Forest（2016）、Hades（2021）、The Artful Escape（2022）
- IGF 美術賞: Hyper Light Drifter（2017）、Chuchel（2018）、Knights and Bikes（2020）、Genesis Noir（2021）、Papetura（2022）、RPG Time: The Legend of Wright（2023）、Phonopolis（2024）

---

## 7. 守ること [RULES]

- 他人の絵・ゲーム画面は、AI（画像生成・Claude 自身とも）に見本として渡さない。作家名・作品名で画風を寄せる指示もしない（CLAUDE.md、`UI_Design_References.md` の `[SAFETY]`）。この資料は、作品の画面ではなく「開発者が何を考えたか」を言葉にしたものなので、考え方として使ってよい。
- この資料に出てくるゲームのサイトや画像を、見た目の作業のたびに開かない。ネットからの参考集めは 2026-11-09 の1回だけの許可。また必要になったらユーザーに聞く。
- ユーザーが決めたこと（メモリ・CLAUDE.md・各資料）が、この資料の提案より優先。
