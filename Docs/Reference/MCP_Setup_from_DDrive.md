# MCP（AI ⇄ Unity Editor 連携）セットアップ ※D-Driveプロジェクトからの参考資料

> **この文書について**
> これは友達が別で進めている「D-Drive」というプロジェクトのドキュメント（`20_mcp_setup.md`）を、このプロジェクト(`Xx_MS2026_xX` / 通称MS2026)向けの参考資料として抜粋・保存したものです。
> D-Driveそのものの設計（アセットID管理・専用エディタ群など）はこのプロジェクトには関係ありませんが、**Claude Code から Unity Editor を直接操作するためのMCP連携の仕組みだけは、このプロジェクトにも既に同じパッケージ (`com.coplaydev.unity-mcp`, `Packages/manifest.json`参照) が入っており、そのまま役立つ**ため、この部分だけ取り込みました。
>
> **このプロジェクトとの違い（読み替え注意）**
> - 文中の「D-Drive」は元ドキュメントの用語で、このプロジェクトでは特にツール名を持たない（読み替え不要、単に他プロジェクトの文脈として読む）
> - `Tools > D-Drive` メニュー、`CLAUDE.md`、`12_review.md`、`10_workflow.md` 等への言及は**D-Drive側にのみ存在**し、このプロジェクトにはまだ存在しない
> - このプロジェクトには **`.mcp.json` がまだ無い**（要作成。下記手順の`.mcp.json`はD-Drive側の設定例として読むこと）
> - 下記「§4 組み込み型サーバー `jp.shiranui-isuzu.unity-mcp`」は、このプロジェクトの`manifest.json`にはまだ入っていない（D-Driveでの評価中の別サーバーの話）。導入するかどうかは別途判断すること
>
> 以下は元ドキュメントの本文（内容はそのまま）です。

---

AI エージェント（Claude Code 等）が **起動中の Unity Editor を直接操作・観測**できるようにする仕組み。
D-Drive では主に「コンパイル結果・テスト結果・Console の確認」「専用エディタウィンドウの起動と操作確認」「シーン/プレハブの組み立て」に使う。

```
[Claude Code] ──HTTP (127.0.0.1:8081/mcp)── [MCP サーバ mcpforunityserver] ──── [Unity 内ブリッジ com.coplaydev.unity-mcp]
   .mcp.json(リポジトリ同梱)                 各自の PC(uvx が自動取得)                Packages/manifest.json(リポジトリ同梱)
```

| 部品 | 実体 | バージョン | 管理場所 |
|---|---|---|---|
| Unity ブリッジ | `com.coplaydev.unity-mcp` | **10.2.0**（タグ固定） | `Packages/manifest.json` |
| MCP サーバ | `mcpforunityserver` (PyPI) | **10.2.0** | 各自の PC。Unity の MCP ウィンドウが `uvx` 経由で起動 |
| クライアント設定 | `.mcp.json` | — | リポジトリ直下（プロジェクトスコープ） |

> ⚠ **ブリッジとサーバのバージョンは必ず一致させる。** 片方だけ上げると「繋がるが一部ツールだけ失敗する」分かりにくい壊れ方をする。`#main` 指定は人によって別バージョンが入るため禁止。

> **このプロジェクトでの確認事項**：`Packages/manifest.json` には現在 `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"` と **`#main`指定のまま**入っている。上記の注意（`#main`禁止、バージョンタグ固定推奨）に従い、動作確認後はタグ固定への変更を検討すること。

---

## 1. 初回セットアップ（各自 1 回）

1. **前提ツール**: Git（PATH 必須。manifest の git URL 解決に使う）と `uv`/`uvx`（<https://docs.astral.sh/uv/>。`uvx --version` が通れば OK）
2. `git pull` 後に Unity を開く（既に開いていればウィンドウをクリックしてフォーカス）→ Package Manager が `MCP for Unity` を取得してコンパイルする
3. Unity メニュー **`Window > MCP for Unity > Toggle MCP Window`**（`Ctrl+Shift+M`）を開く
4. Transport を **HTTP (Local)** にし、**Start Server** を押す → `mcpforunityserver` が `uvx` で取得・起動され、既定では `http://127.0.0.1:8080/mcp` で待ち受ける（**ポートが他アプリと衝突する場合はBase URLを変更し、`.mcp.json`も同じポートに揃える**）
5. 同ウィンドウの **Connect** で Unity ブリッジをサーバに接続する（Auto-Start を有効にしておくと以後は Editor 起動時に自動）
6. プロジェクト直下で Claude Code を起動する。`.mcp.json` が検出され、初回のみ「このプロジェクトの MCP サーバを許可するか」を聞かれるので許可する

### 接続確認

Claude に「Unity の Console を読んで」と頼み、エラーにならず結果が返れば接続できている。

| 症状 | 原因 | 対処 |
|---|---|---|
| `No Unity Editor instances found` | Unity 未起動 / ブリッジ未接続 | Unity を起動し、MCP ウィンドウで Connect |
| 接続拒否 (connection refused) | HTTP サーバが起動していない | MCP ウィンドウで Start Server |
| Claude Code 側が `ENDPOINT_NOT_FOUND` / ポートへの `curl` が 403・404 を返す | **ポートを別プロセスが占有**しており `mcpforunityserver` が起動できていない（`Get-NetTCPConnection -LocalPort <port> -State Listen` で持ち主を確認） | 占有プロセスを止めるか、MCP ウィンドウの Base URL を別ポートに変えて Start Server →`.mcp.json` の `url` も同じポートに揃え、Claude Code を再起動 |
| 一部のツールだけ失敗する | **ブリッジとサーバのバージョン不一致** | 両方を同じバージョンに揃える |
| Package Manager で git エラー | Git 未導入 / PATH 未設定 | Git を入れて PC 再起動 |
| 別プロジェクトを同時に開いている | 1 つのサーバに複数 Editor が接続 | ツール呼び出し時に AI へ対象プロジェクトを明示（サーバは複数インスタンスをルーティングできる。混線時は片方を閉じる） |

### テレメトリ

サーバは既定で利用統計を送信する。切る場合は Unity の MCP ウィンドウ **Advanced > Telemetry** を無効にする（サーバ起動時に環境変数 `UNITY_MCP_TELEMETRY_ENABLED=false` が渡される）。

---

## 2. 使い方（AI に任せる作業の線引き）

| AI に任せてよい | 人間がやる（AI に任せない） |
|---|---|
| スクリプトの生成・編集（通常のファイル編集） | `ProjectSettings/` の変更（Layer/Tag/Physics 等） |
| **`read_console` でコンパイルエラー・警告の確認** | パッケージの追加・削除（`manifest.json`） |
| **`run_tests` で EditMode/PlayMode テストの実行** | 他人が作業中のシーン・Prefab への変更 |
| Toolsメニューの実行（`execute_menu_item`）と結果確認 | ビルド設定の変更 |
| 専用エディタの起動・操作確認・スクリーンショット | 本番アセットの最終的な見た目・音の判断 |
| GameObject / Prefab の構成確認、単純な Prefab の組み立て（`manage_gameobject` / `manage_prefabs`） | 大規模なシーン作り替え |

### 運用ルール

- **`.unity` / `.prefab` / `.asset` の YAML を AI にテキスト編集させない。** 必ず MCP ツール（Unity Editor 経由）で行う。`.meta` の手作成・GUID 書き換えも禁止
- **状態の取得は resource（`mcpforunity://editor/state` 等）、変更は tool** で行う
- **Play Mode 中は書き込み系ツールを実行しない**（変更が破棄される）
- コード変更後は AI 自身が `read_console` でエラー 0 を確認し、関連テストを `run_tests` で回してから完了報告する。**接続できていないのに「Unity で確認した」と報告しない**（未検証なら未検証と書く）
- AI の変更も通常どおりレビュー対象とする（「AI がやった」はレビュー省略の理由にならない）

---

## 3. バージョンを上げるとき

1. `Packages/manifest.json` のバージョン指定を変更
2. 各自 Unity の MCP ウィンドウでサーバを **Stop → Start**（新しいバージョンの`mcpforunityserver`が取得される）
3. 関連ドキュメントのバージョン表記を更新
4. 同一 PC で複数プロジェクトを運用している場合は、**全プロジェクトを同じバージョンに揃える**（サーバは PC で 1 つ）

---

## 4.（参考・未導入）組み込み型サーバー `jp.shiranui-isuzu.unity-mcp` について

D-Drive側では、CoplayDev版で困っていた問題（別プロセスのPythonサーバー、固定ポートの衝突、テスト実行の不安定さ等）を解消するため、[isuzu-shiranui/UnityMCP](https://github.com/isuzu-shiranui/UnityMCP)（Editor内組み込み型、MIT）をCoplayDev版と併用評価している、という記録が元ドキュメントにありました。**このプロジェクトの`manifest.json`にはまだ導入されていません。** 現状のCoplayDev版で困りごとが出た場合の選択肢として、必要になったら改めて調査すること。
