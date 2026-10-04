# マルチプレイ化 進捗と宿題（4人対戦・LAN）

> 別のチャット（Claude Code のセッション）で作業を再開するときは、まずこのファイルを読んでください。
> 最終更新: 2026-10-04。コードの詳細は `Docs/Tools/FortressDesigner_AI_Reference.md` の「Net」「Hud」「Swarm」節にあります。
> 最初の計画書（承認済み）: `C:\Users\soret\.claude\plans\snoopy-forging-castle.md`。

## 1. 目標と方針（ユーザー承認済み）

- 4人がそれぞれ別のPC（握力センサー1台ずつ）を持ち寄り、LANで同じ試合を遊ぶ。最大3万体の群衆も同期する。
- プレイヤー1（番号0）のPCがHostを兼ねる。**Host権威**（結果はHostが決め、Clientは入力を送って結果を表示する）。
- 通信は NGO（Netcode for GameObjects）+ D-Drive のネットワーク層。接続は `FortressNetworkBootstrap`（D-Drive の `StartHost/StartClient` を呼ぶ）。
- 同期は、シーンに1つの「[Fortress] NetSync」（NetworkObject）に付けた中継役（`*NetworkHub`）が行う。敵や破壊可能物に NetworkObject は付けない（デザイナーの手間を増やさないため）。
- 本番会場の回線は**有線LAN（スイッチングハブ）**の予定（2026-10-04 回答）。

## 2. フェーズの状況

| Phase | 内容 | 状態 |
|---|---|---|
| 0 | 接続基盤（接続UI・承認・起動引数・bat） | ✅ 完了・確認済み |
| 1 | 砲台（握力→Host計算→全員に表示）、自分のゲージ | ✅ 完了・確認済み |
| 2 | 破壊可能物・スマッシュボール（誰が割ったか含む） | ✅ 完了・確認済み |
| 3 | Actorの敵（ボスなど） | ✅ 完了・確認済み（今のゲームにActorの敵の種類はまだ無い） |
| 4 | 群衆（最大3万体）: 各PCで計算＋Hostの位置で補正 | ✅ 動作確認済み。**性能改善中（下の3章）** |
| 5 | コアHP・勝敗・ラウンド進行（`NetworkGameManager` 新設） | ⬜ 未着手（今はコアHPがHostでしか減らない） |
| 6 | ホスト引き継ぎ（Host離脱時に番号最小の人が引き継ぐ） | ⬜ 未着手 |

### ユーザーが決めたこと（作業中に確認した内容）
- 各PCのセンサーは、そのPCでは常にP1（番号0、キーボードならQ）として読む。
- Phase 1〜3の間の当たり判定はHostだけ。自分のレーザーはHost確定のみ（予測なし）。ゲージは自分の分だけ。
- ドロップ品は「未定（今は見た目扱い、各PCで別々に出す）」。
- 群衆は「各PCで計算＋Hostで補正」方式（通信量がHostの位置をそのまま配る方式の約1/10）。
- 握力入力: 実機とキーボードを合成（`CompositeGripTransport`）。デバイスが無いPCでもキーボードで動く。

## 3. 群衆の性能改善（2026-10-04〜）

症状（ユーザー報告）: 3万体を一斉投入すると、Clientに揃うまでの遅れが目立つ。投入後はHostもClientもカクつく。

| 段階 | 内容 | 状態 |
|---|---|---|
| 0 | 自動計測（`Tools/NetTest/bench_2p.bat`・`bench_offline.bat`、`[Bench]` ログ、HUDの通信表示） | ✅ 実装・計測済み |
| 1 | すぐ効く調整: 1フレームに湧かせる上限3000／Clientの押し合い反復1回／`MaxPacketQueueSize` 128→1024 | ✅ 実装・計測済み（下表） |
| 1.5 | **不具合修正**: 補正（Unreliable）が1回3.6KBで送られ、NGOの上限1296バイトを超えて**1件も届いていなかった**。`NetMessageLimits` で分割 | ✅ 修正・計測済み（補正が届くようになった） |
| 2 | 一斉投入は「命令」（種類・中心・半径・数・乱数の種）だけ送り、Clientが同じ乱数で再現 | ⬜ 優先度低（届くまで既に1秒未満） |
| 3 | 送受信をゴミ（GC）ゼロ化（NGOの低レベルAPI＋事前確保バッファ）、詰め込みをBurst Job化、差分＋可変長整数で圧縮 | ⬜ |
| 4 | 補正を「描画だけのずらし」に変更（計算上の位置はHostに即合わせ） | ✅ 実装・計測済み（下表） |
| 4.5 | 補正の間隔の自動調整（Clientがズレを報告→Hostが間隔を調整）。既定を「普段0.25秒・最短0.1秒」に決定 | ✅ 実装・計測済み。**シーンの値を0.25にする必要あり（下記）** |
| 4b | Hostの時刻基準で正確に先読み | ⬜ |
| 5 | 画面内・レーザー付近・コア付近の敵を優先して補正、回線の空きに応じて送信量を自動調整 | ⬜ |

### 計測結果（2026-10-04、1台のPCでHost+Client、3万体一斉投入。Hostは裏のウィンドウ）

| | 改善前 | 段階1後 | オフライン（参考） |
|---|---|---|---|
| Host 投入直後の最大フレーム時間 | 62ms | 35ms | 24ms |
| Host 投入5秒後〜のFPS / 最悪フレーム | 74 / 32ms | 117 / 22ms | 268 / 16ms |
| Client 投入5秒後〜のFPS / 最悪フレーム | 217 / 19ms | 260 / 13ms | — |
| Clientに約2.8万体が届くまで | 0.8秒以内 | 0.8秒以内 | — |
| 補正の送受信 | 0件（不具合） | 0件（不具合） | — |

補正の修正後（2026-10-04、同条件）:
- 補正は毎秒約4.5万件届くようになった（Hostの送信 約250KB/s ≒ 2Mbps / Client1人）。例外0件。
- Hostとのズレ（Client、ワールド単位。敵の半径は0.22）: 投入後0〜3秒は平均10・最大30〜39・瞬間移動（4以上ずれて即合わせ）毎秒1.5万〜2.9万体 → 4秒目以降は平均0.7〜0.8・最大4〜5・瞬間移動 毎秒数〜20体で安定。
- 投入直後は3万体が半径6に詰め込まれて押し合いが爆発的になり、HostとClientの計算が全く別の結果になる（カオス）。補正が0.5秒に1回では追いつかず、約3秒かけて収まる。
- FPS: Host 116 / Client 284（5秒後〜）。Clientの全体の受信は約0.9秒で完了。

次の優先度（計測を踏まえて見直し）: 段階4（計算上の位置はHostに即合わせ、ズレは描画だけで消す）＋補正頻度の調整（起動引数で切り替え可能にして計測）を最優先。段階3（GCゼロ化）が次。段階2（一斉投入の命令化）は、届くまで既に1秒未満なので優先度を下げる。

段階4・補正頻度の比較（2026-10-04、同条件。ズレ＝Clientの画面に描かれた位置とHostの位置の差、ワールド単位、敵の半径0.22）:

| 方式 | 投入後5秒の平均ズレ | 5秒後〜の平均ズレ / 最大(平均) | 5秒後〜の瞬間移動 | 通信(Client受信) |
|---|---|---|---|---|
| 従来方式・0.5秒固定 | 4.48 | 0.75 / 4.5 | 毎秒9回 | 267KB/s |
| 新方式・0.5秒固定 | 4.35 | 0.56 / 3.1 | 0 | 263KB/s |
| 新方式・自動(0.5〜0.15) | 2.13 | 0.50 / 3.0 | 0 | 331KB/s |
| 新方式・0.25秒固定 | 2.64 | 0.41 / 2.7 | 0 | 523KB/s |
| 新方式・0.15秒固定 | 1.72 | 0.36 / 2.4 | 0 | 859KB/s |
| **新方式・自動(0.25〜0.1)←既定に採用** | **1.58** | **0.38 / 2.5** | 0 | 562KB/s |

- FPSはどれもほぼ同じ（Host 約117 / Client 約280）。Clientの60〜90msのカクつきは、計測の最後にHostが先に終了して切断処理をする瞬間だけ（ゲーム中ではない）。
- ⚠ シーンの「[Fortress] NetSync」の Swarm Network Hub に `Correction Cycle Seconds = 0.5` が保存されているため、Inspectorで0.25に変更して保存する必要がある（コードの既定値は0.25に変更済み）。
- 残る課題: 投入直後の約2秒は押し合いの混乱でまだ大きくずれる（瞬間移動が数万回）。補正が多いほどGCも増える（Client 毎秒5〜9回）。→ 次は段階3（GCゼロ化）と、補正に速度を載せてClientの先読みを正確にする案。

分かったこと: オフラインなら3万体でも軽い（FPS 260以上）。1台2起動ではHost（裏のウィンドウ）がCPUを取られて重い。毎秒3〜4回のGC（ゴミ集め）が常に起きている（段階3で対処予定）。

計測のやり方（Claudeが自分で実行してよいとユーザー許可済み。ビルドだけはユーザーがUnityで行う）:
- ビルド先: `<プロジェクト>\Builds\TestBuild`（Git管理外）。Development Build。
- `Tools\NetTest\bench_2p.bat` … Host+Clientを起動し、Clientが全体の状態を受け取ったら3秒後にHostが3万体投入、1秒ごとに `[Bench]` 行をログへ、45秒で自動終了。ログは `Builds\TestBuild\logs\p0_host_bench.log` / `p1_client_bench.log`。
- 改善前の設定で比べる: `bench_2p.bat -fortress-swarm-spawns-per-frame 0 -fortress-replica-separation -1 -fortress-net-packet-queue 128`（ログ名を変えたいときは先に `set LOGSUFFIX=_before`）。
- ネットワーク無しの基準: `bench_offline.bat`（`p0_ui_bench_offline.log`）。
- 段階4の比較: 従来方式 `-fortress-swarm-smoothing blend -fortress-swarm-adaptive 0` / 新方式固定 `-fortress-swarm-adaptive 0` / 新方式＋自動調整（引数なし）/ 固定間隔 `-fortress-swarm-adaptive 0 -fortress-swarm-correction-cycle 0.25`（0.15 も）。
- ⚠ 1台のPCで2つ起動すると、CPU/GPUを取り合うので本番より重く出る。

## 4. 宿題: 2台のPCでのテスト（ユーザーが別の機会に実施）

1台のPCで複数起動すると本番より重く出るため、実際の重さ・遅れは2台で測る必要がある。

準備
1. 両方のPCに同じビルドを置く（`Builds\TestBuild` をフォルダごとコピー。**必ず同じビルド**。違うと破壊可能物・敵の並びの照合でエラーになり同期しない）。
2. Host側PCのIPアドレスを調べる（コマンドプロンプトで `ipconfig` → IPv4 アドレス）。
3. 初回起動時にWindowsのファイアウォールの確認が出たら「プライベート ネットワーク」を許可する（UDP 7777 を使う）。
4. 両方のPCでプロジェクトの `Tools\NetTest\` を使う（またはbatを `MS2026.exe` のフォルダと一緒にコピーし、環境変数 `MS2026_BUILD_DIR` でビルドの場所を指定）。

動作確認
- Host側PC: `run_instance.bat host 0`
- Client側PC: `run_instance.bat client 1 <HostのIP>`

負荷計測（2台）
- Host側PC: `run_instance.bat host 0 - -fortress-bench -fortress-bench-burst 30000 -fortress-bench-clients 1 -fortress-bench-quit 60 -fortress-bench-no-wave`
- Client側PC: `run_instance.bat client 1 <HostのIP> -fortress-bench -fortress-bench-quit 60 -fortress-bench-no-wave`
- 終わったら両方の `logs` フォルダのログ（`p0_host.log` / `p1_client.log`）をClaudeに渡す。PCごとに時計がずれるので、時刻ではなく `BURST` 行からの経過で比べる。
- 4台そろうなら `-fortress-bench-clients 3` にして、Client側を `client 1` `client 2` `client 3` で起動する。

## 5. 既知の制限・注意

- コアHPはHostでしか減らない（Phase 5で同期）。勝敗・ラウンド進行は未実装。
- Host離脱時の引き継ぎは未実装（Phase 6）。今はHostが落ちると全員切断される。
- Client側の群衆は補正の合間に少しずれる／すっと滑ることがある（段階4で改善予定）。
- 破壊可能物・敵の種類は「ウェーブ設定に入っている物」「シーン内の名前の並び」で番号を振る。HostとClientは必ず同じビルドを使う。
- 要塞デザイナーのテストボタン（壊す・湧かせる・ストレステスト）はHost側で押す（Clientでは無効）。

## 6. 主なファイル

| 何 | 場所 |
|---|---|
| 接続・承認・起動引数 | `Assets/_Game/Fortress/Runtime/Net/FortressNetworkBootstrap.cs`, `FortressLaunchArgs.cs`, `FortressConnectUI.cs` |
| 同期の中継役 | `Runtime/Net/TurretNetworkHub.cs`, `Destructibles/DestructibleNetworkHub.cs`, `Enemies/EnemyNetworkHub.cs`, `Swarm/SwarmNetworkHub.cs` |
| 群衆のネット対応 | `Runtime/Swarm/SwarmSystem.Net.cs`, `SwarmCorrectionJob.cs`, `SwarmIdPool.cs` |
| 計測 | `Runtime/Net/Bench/NetBenchmarkRunner.cs`, `NetBenchmarkArgs.cs`, `NetTrafficStats.cs` |
| シーンへの配置 | メニュー `Tools/要塞/ネットワーク/同期オブジェクトをシーンに配置`（`Editor/Net/FortressNetSceneSetup.cs`） |
| 起動用bat | `Tools/NetTest/`（`launch_2p/4p`, `launch_ui_2p`, `launch_duplicate_test`, `bench_2p`, `bench_offline`, `run_instance`） |
| 画面のUI配置 | `Runtime/Hud/DebugOverlayLayout.cs`（左上の縦積み）、`LocalTurretGaugeHud.cs` |
