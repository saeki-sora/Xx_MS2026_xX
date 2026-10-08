using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ゲーム（要塞）が画面に渡す値の名前と説明。<see cref="FortressUiValues"/> が毎フレーム書き込む。
    /// プレイヤー番号は人に合わせて 1〜4（P1〜P4）。UIスタジオの「値」ページの「ゲームの値を一覧に登録」で一覧に入る。
    /// </summary>
    public static class FortressUiKeys
    {
        public const string LocalPlayer = "local.player";
        public const string LocalPlayerLabel = "local.playerLabel";
        public const string LocalColor = "local.color";
        public const string LocalHeat = "local.heat";
        public const string LocalOverheated = "local.overheated";
        public const string LocalFiring = "local.firing";
        public const string LocalCharge = "local.charge";
        public const string LocalThickness = "local.thickness";
        public const string LocalGrip = "local.grip";

        public const string CoreHp = "core.hp";
        public const string CoreHp01 = "core.hp01";
        public const string CoreDestroyed = "core.destroyed";

        public const string SwarmAlive = "swarm.alive";
        public const string WaveTime = "wave.time";
        public const string WavePlaying = "wave.playing";

        public const string NetMode = "net.mode";
        public const string NetPlayers = "net.players";
        public const string NetLastError = "net.lastError";

        public const string LobbyStatus = "lobby.status";
        public const string LobbySelectedPlayer = "lobby.selectedPlayer";
        public const string LobbySelectedLabel = "lobby.selectedLabel";
        public const string LobbyName = "lobby.name";
        public const string LobbyPhase = "lobby.phase";
        public const string LobbyIsHost = "lobby.isHost";
        public const string LobbyInRoom = "lobby.inRoom";
        public const string LobbyConnecting = "lobby.connecting";
        public const string LobbyPlayers = "lobby.players";
        public const string LobbyReadyCount = "lobby.readyCount";
        public const string LobbyAllReady = "lobby.allReady";
        public const string LobbyCanStart = "lobby.canStart";
        public const string LobbyCountdown = "lobby.countdown";
        public const string LobbyCountingDown = "lobby.countingDown";
        public const string LobbyLocalReady = "lobby.localReady";
        public const string LobbyGripHold = "lobby.gripHold";
        public const string LobbyAddress = "lobby.address";
        public const string LobbyHostAddress = "lobby.hostAddress";
        public const string LobbyFoundHosts = "lobby.foundHosts";
        public const string LobbyInGame = "lobby.inGame";

        public const string MessageTitle = "message.title";
        public const string MessageBody = "message.body";
        public const string ToastText = "toast.text";

        public static string PlayerHeat(int player) => $"player.{player + 1}.heat";
        public static string PlayerOverheated(int player) => $"player.{player + 1}.overheated";
        public static string PlayerFiring(int player) => $"player.{player + 1}.firing";
        public static string PlayerGrip(int player) => $"player.{player + 1}.grip";
        public static string PlayerConnected(int player) => $"player.{player + 1}.connected";
        public static string PlayerColor(int player) => $"player.{player + 1}.color";
        public static string PlayerName(int player) => $"player.{player + 1}.name";
        public static string PlayerReady(int player) => $"player.{player + 1}.ready";
        public static string PlayerIsHost(int player) => $"player.{player + 1}.isHost";
        public static string PlayerIsLocal(int player) => $"player.{player + 1}.isLocal";
        public static string PlayerPing(int player) => $"player.{player + 1}.ping";
        public static string PlayerTaken(int player) => $"player.{player + 1}.taken";

        /// <summary>UIスタジオの値の一覧に登録する内容（キー、表示名、説明、種類、分類、範囲、サンプル値）。</summary>
        public static (string key, string label, string description, UiValueKind kind, string category, Vector2 range, float sample)[] Describe()
        {
            var list = new System.Collections.Generic.List<(string, string, string, UiValueKind, string, Vector2, float)>
            {
                (LocalPlayer, "自分のプレイヤー番号", "このPCのプレイヤー（0〜3）。決まっていなければ -1。", UiValueKind.Number, "自分", new Vector2(-1, 3), 0),
                (LocalPlayerLabel, "自分の名前（P1〜P4）", "例: P1。", UiValueKind.Text, "自分", Vector2.up, 0),
                (LocalColor, "自分の色", "自分のプレイヤー色。", UiValueKind.Color, "自分", Vector2.up, 0),
                (LocalHeat, "自分の熱（割合）", "0=冷えている、1=オーバーヒート寸前。", UiValueKind.Number, "自分", Vector2.up, 0.65f),
                (LocalOverheated, "自分がオーバーヒート中", "撃てない間だけON。", UiValueKind.Bool, "自分", Vector2.up, 0),
                (LocalFiring, "自分が撃っている", "レーザーを出している間ON。", UiValueKind.Bool, "自分", Vector2.up, 1),
                (LocalCharge, "自分のチャージ（割合）", "溜め撃ちがONのとき、溜まり具合。", UiValueKind.Number, "自分", Vector2.up, 0.3f),
                (LocalThickness, "自分のレーザーの太さ（割合）", "握る強さで変わる太さ。", UiValueKind.Number, "自分", Vector2.up, 0.5f),
                (LocalGrip, "自分の握力（割合）", "センサーの値（その人の0〜100%）。", UiValueKind.Number, "自分", Vector2.up, 0.5f),
                (CoreHp, "コアのHP", "残りHP（数）。", UiValueKind.Number, "コア", new Vector2(0, 100), 70),
                (CoreHp01, "コアのHP（割合）", "1=満タン、0=壊れた。", UiValueKind.Number, "コア", Vector2.up, 0.7f),
                (CoreDestroyed, "コアが壊れた", "壊れたらON（負け）。", UiValueKind.Bool, "コア", Vector2.up, 0),
                (SwarmAlive, "敵の数", "今いる群衆の敵の数。", UiValueKind.Number, "敵", new Vector2(0, 5000), 1200),
                (WaveTime, "ウェーブの経過秒", "ウェーブが始まってからの秒数。", UiValueKind.Number, "敵", new Vector2(0, 300), 42),
                (WavePlaying, "ウェーブ中", "敵が湧いている間ON。", UiValueKind.Bool, "敵", Vector2.up, 1),
                (NetMode, "通信の状態", "オフライン / ホスト / 参加中。", UiValueKind.Text, "通信", Vector2.up, 0),
                (NetPlayers, "つながっている人数", "ホストから見た参加人数（自分を含む）。", UiValueKind.Number, "通信", new Vector2(0, 4), 2),
                (NetLastError, "最後の切断理由", "つながらなかった・切れたときの理由。", UiValueKind.Text, "通信", Vector2.up, 0),
                (LobbyStatus, "ロビーのお知らせ", "ロビー画面に出す状態の文（接続中…など）。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbySelectedPlayer, "ロビーで選んだ番号", "ロビーで選んでいるプレイヤー（0〜3）。", UiValueKind.Number, "ロビー", new Vector2(0, 3), 0),
                (LobbySelectedLabel, "ロビーで選んだ番号（文字）", "例: P2。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbyName, "自分の名前", "ロビーで入力した名前（このPCに保存される）。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbyPhase, "部屋の状態", "待っています / カウントダウン / 読み込み中 / 試合中。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbyIsHost, "自分がホスト", "部屋を作った人のときON（開始ボタンなどを出す）。", UiValueKind.Bool, "ロビー", Vector2.up, 1),
                (LobbyInRoom, "部屋に入っている", "ホストになった・参加できたあとON。", UiValueKind.Bool, "ロビー", Vector2.up, 1),
                (LobbyConnecting, "接続中", "参加ボタンを押してからつながるまでON。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (LobbyPlayers, "部屋の人数", "部屋にいる人数（自分を含む）。", UiValueKind.Number, "ロビー", new Vector2(0, 4), 3),
                (LobbyReadyCount, "準備OKの人数", "準備OKにした人数。", UiValueKind.Number, "ロビー", new Vector2(0, 4), 2),
                (LobbyAllReady, "全員が準備OK", "部屋の全員が準備OKのときON。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (LobbyCanStart, "開始できる", "自分がホストで、全員が準備OKのときON（「ゲーム開始」を押せる）。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (LobbyCountdown, "カウントダウンの数", "3, 2, 1。数えていないときは0。", UiValueKind.Number, "ロビー", new Vector2(0, 5), 3),
                (LobbyCountingDown, "カウントダウン中", "開始のカウントダウン中ON。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (LobbyLocalReady, "自分が準備OK", "自分が準備OKにしているときON。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (LobbyGripHold, "握って準備の溜まり", "握り続けると0→1。1になると準備OKが切り替わる。", UiValueKind.Number, "ロビー", Vector2.up, 0.4f),
                (LobbyAddress, "このPCのアドレス", "このPCのLANのIPアドレス（ほかの人が参加するときに入力する）。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbyHostAddress, "部屋のアドレス", "ホストのアドレス（参加側から見た接続先・ホストでは自分のアドレス）。", UiValueKind.Text, "ロビー", Vector2.up, 0),
                (LobbyFoundHosts, "見つかった部屋の数", "同じLANで見つかったホストの数。", UiValueKind.Number, "ロビー", new Vector2(0, 8), 2),
                (LobbyInGame, "試合中", "ゲームのシーンに入っている間ON。", UiValueKind.Bool, "ロビー", Vector2.up, 0),
                (MessageTitle, "お知らせの見出し", "お知らせの小窓（Message）の見出し。", UiValueKind.Text, "お知らせ", Vector2.up, 0),
                (MessageBody, "お知らせの本文", "お知らせの小窓（Message）の本文。", UiValueKind.Text, "お知らせ", Vector2.up, 0),
                (ToastText, "通知の文", "画面の端に少しだけ出る通知（Toast）の文。", UiValueKind.Text, "お知らせ", Vector2.up, 0)
            };

            for (var p = 0; p < 4; p++)
            {
                var name = $"P{p + 1}";
                list.Add((PlayerHeat(p), $"{name}の熱（割合）", "0=冷えている、1=オーバーヒート寸前。", UiValueKind.Number, "プレイヤー", Vector2.up, 0.2f + 0.2f * p));
                list.Add((PlayerOverheated(p), $"{name}がオーバーヒート中", "撃てない間だけON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p == 3 ? 1 : 0));
                list.Add((PlayerFiring(p), $"{name}が撃っている", "レーザーを出している間ON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p % 2));
                list.Add((PlayerGrip(p), $"{name}の握力（割合）", "ホストでは全員分、参加側では自分の分だけ正確。", UiValueKind.Number, "プレイヤー", Vector2.up, 0.5f));
                list.Add((PlayerConnected(p), $"{name}がつながっている", "ホストでは全員分がわかる。参加側では自分だけON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p < 2 ? 1 : 0));
                list.Add((PlayerColor(p), $"{name}の色", "プレイヤー色。", UiValueKind.Color, "プレイヤー", Vector2.up, 0));
                list.Add((PlayerName(p), $"{name}の名前", "ロビーで付けた名前。空なら未参加。", UiValueKind.Text, "プレイヤー", Vector2.up, 0));
                list.Add((PlayerReady(p), $"{name}が準備OK", "ロビーで準備OKにしているときON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p % 2));
                list.Add((PlayerIsHost(p), $"{name}がホスト", "部屋を作った人のときON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p == 0 ? 1 : 0));
                list.Add((PlayerIsLocal(p), $"{name}が自分", "このPCのプレイヤーのときON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p == 0 ? 1 : 0));
                list.Add((PlayerPing(p), $"{name}の通信の遅れ", "ホストとの往復の時間（ミリ秒）。", UiValueKind.Number, "プレイヤー", new Vector2(0, 300), 12));
                list.Add((PlayerTaken(p), $"{name}がもう使われている", "参加する部屋で、その番号がほかの人に使われているときON。", UiValueKind.Bool, "プレイヤー", Vector2.up, p == 1 ? 1 : 0));
            }

            return list.ToArray();
        }
    }
}
