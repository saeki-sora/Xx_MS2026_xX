using System;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;
using BgmId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.BgmMarker>;

namespace MS2026.UI.Game
{
    /// <summary>ロビーの中で今どこにいるか（このPCから見た状態）。</summary>
    public enum SessionState
    {
        /// <summary>最初の画面（名前・番号を決める）。</summary>
        Top,

        /// <summary>参加する部屋を選んでいる。</summary>
        Browsing,

        /// <summary>参加ボタンを押して、つながるのを待っている。</summary>
        Connecting,

        /// <summary>部屋にいる（ホスト、または参加できた）。</summary>
        Room,

        /// <summary>ゲームのシーンで遊んでいる。</summary>
        InGame,

        /// <summary>部屋を出る・タイトルへ戻る途中。</summary>
        Leaving
    }

    /// <summary>
    /// タイトル → ロビー → ゲーム の流れを受け持つ（ロビーのシーンに1つ。ロビーのシーンは遊んでいる間も読み込まれたまま）。
    /// ・ホストになる／参加する（同じLANの部屋は自動で見つかる）、部屋の全員の名前・準備OK・通信の遅れを全員に見せる
    /// ・全員が準備OKになったらホストが「ゲーム開始」→ 3・2・1 → 全員一緒にゲームのシーンを重ねて読み込む（NGO のシーン管理）
    /// ・試合中は Esc で一時メニュー（全員でロビーに戻る／試合から抜ける／タイトルへ）。ホストが抜けたら全員ロビーへ
    /// 画面はUIスタジオの画面（名前で開く）、表示する値は UiValues（FortressUiKeys の lobby.* / player.N.*）。
    /// 通信の土台（NetworkManager・D-Drive・FortressNetworkBootstrap）はロビーのシーンに置き、ゲームのシーンの同じ物は
    /// <see cref="GameSceneSessionGuard"/> が読み込み時に外す（ゲームのシーンだけで遊ぶときは今まで通りそちらを使う）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    [AddComponentMenu("UI Studio/ロビーとゲームの流れ (GameSession)")]
    public sealed partial class GameSession : MonoBehaviour
    {
        [Header("シーン")]
        [Tooltip("試合をするシーンの名前（ビルドの設定に入っていること）。ロビーの上に重ねて読み込む。")]
        public string gameSceneName = "Game";

        [Tooltip("「タイトルへ」で戻るシーンの名前。")]
        public string titleSceneName = "Title";

        [Header("画面の名前（UIスタジオの画面の一覧の名前）")]
        [Tooltip("ロビーの背景（背景の段）。試合中は閉じる。")]
        public string backgroundScreen = "LobbyBackground";

        [Tooltip("最初の画面（名前・番号・ホストになる・参加する）。")]
        public string topScreen = "LobbyTop";

        [Tooltip("参加する部屋を選ぶ画面。")]
        public string joinScreen = "LobbyJoin";

        [Tooltip("部屋の画面（4人の席・準備OK・ゲーム開始）。")]
        public string roomScreen = "LobbyRoom";

        [Tooltip("3・2・1 の画面。")]
        public string countdownScreen = "Countdown";

        [Tooltip("試合中の画面。")]
        public string hudScreen = "Hud";

        [Tooltip("試合中に Esc で出す一時メニュー。")]
        public string pauseScreen = "Pause";

        [Tooltip("お知らせの小窓（切断されたときなど）。")]
        public string messageScreen = "Message";

        [Tooltip("画面の端に少し出る通知（○○が参加しました など）。")]
        public string toastScreen = "Toast";

        [Header("画面切り替えの幕（空ならフェード）")]
        [Tooltip("ロビーの画面どうし（最初の画面 ↔ 部屋）の切り替え。")]
        public UiScreenTransition menuTransition;

        [Tooltip("ロビー ↔ ゲームの切り替え。")]
        public UiScreenTransition gameTransition;

        [Tooltip("タイトルへ戻るとき。")]
        public UiScreenTransition titleTransition;

        [Header("開始")]
        [Tooltip("「ゲーム開始」を押してから始まるまでの秒数（3・2・1）。")]
        [Min(0f)]
        public float countdownSeconds = 3f;

        [Tooltip("開始に必要な人数（ホストを含む）。")]
        [Range(1, 4)]
        public int minPlayers = 1;

        [Tooltip("ONなら、ホストは全員の準備OKを待たずに「待たずに開始」できる。")]
        public bool allowForceStart = true;

        [Header("握って準備OK")]
        [Tooltip("ONなら、センサーを握り続けると準備OKが切り替わる（ボタンを押さなくてよい）。")]
        public bool gripToReady = true;

        [Tooltip("この強さ以上で握っている間、溜まっていく（0〜1）。")]
        [Range(0.1f, 1f)]
        public float gripReadyThreshold = 0.6f;

        [Tooltip("この秒数握り続けると切り替わる。")]
        [Min(0.2f)]
        public float gripReadyHoldSeconds = 1f;

        [Header("通信")]
        [Tooltip("同じLANの部屋を見つけるための番号（UDP）。ゲームの通信（7777）とは別。全員同じにする。")]
        public int discoveryPort = 47777;

        [Tooltip("参加ボタンを押してから、この秒数でつながらなければあきらめる。")]
        [Min(1f)]
        public float connectTimeoutSeconds = 10f;

        [Tooltip("アドレスの欄の最初の値（前回の値があればそちら）。")]
        public string defaultAddress = "127.0.0.1";

        [Header("ロビーの見た目（試合中は隠す）")]
        [Tooltip("ロビー用のカメラ。試合中は止める（ゲームのカメラと取り合わないように）。")]
        public Camera lobbyCamera;

        [Tooltip("試合中は隠すロビーの物（背景など）。")]
        public GameObject[] hideDuringGame = Array.Empty<GameObject>();

        [Header("音・演出（D-Drive の番号札。空なら鳴らさない）")]
        [Tooltip("ロビーで流すBGM。試合に入るとステージのBGMに変わり、ロビーに戻るとまたこれになる。")]
        public BgmId lobbyBgm;

        [Tooltip("誰かが部屋に入ったとき。")]
        public FortressEffect onPlayerJoined = new FortressEffect();

        [Tooltip("誰かが部屋から抜けたとき。")]
        public FortressEffect onPlayerLeft = new FortressEffect();

        [Tooltip("誰かの準備OKが変わったとき。")]
        public FortressEffect onReadyChanged = new FortressEffect();

        [Tooltip("カウントダウンの1つごと。")]
        public FortressEffect onCountdownTick = new FortressEffect();

        [Tooltip("試合が始まる瞬間。")]
        public FortressEffect onMatchStart = new FortressEffect();

        [Tooltip("つながらない・切れたなどのお知らせを出すとき。")]
        public FortressEffect onProblem = new FortressEffect();

        [Header("参照（空なら自動で探す）")]
        [Tooltip("接続を受け持つ部品（ロビーのシーンの [D-Drive] Runtime に付いている物）。")]
        public FortressNetworkBootstrap network;

        private const string PrefName = "MS2026.Lobby.Name";
        private const string PrefSeat = "MS2026.Lobby.Seat";
        private const string PrefAddress = "MS2026.Lobby.Address";

        /// <summary>今のロビー（ロビーのシーンが無ければ null。ゲームのシーンだけで遊んでいるとき）。</summary>
        public static GameSession Active { get; private set; }

        public SessionState State { get; private set; } = SessionState.Top;

        /// <summary>部屋の一覧（ホストが持ち、参加側は届いた物）。</summary>
        public LobbyRoster Roster { get; } = new LobbyRoster();

        public LanDiscovery Discovery { get; private set; }

        public string LocalName { get; private set; } = string.Empty;

        /// <summary>このPCが座る席（P1〜P4 = 0〜3）。</summary>
        public int SelectedSeat { get; private set; }

        /// <summary>前回参加したアドレス（アドレスの欄の初期値）。</summary>
        public string LastAddress { get; private set; }

        /// <summary>今の部屋のアドレス（ホストなら自分のアドレス）。</summary>
        public string RoomAddress { get; private set; } = string.Empty;

        /// <summary>状態が変わったとき（画面の部品が見直す合図）。</summary>
        public event Action Changed;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogWarning("[Lobby] GameSession が2つあります。後から来た方を止めます。");
                enabled = false;
                return;
            }

            Active = this;
            network = network != null ? network : FindFirstObjectByType<FortressNetworkBootstrap>();
            Discovery = new LanDiscovery(discoveryPort);
            Discovery.Changed += RaiseChanged;
            LoadPrefs();
        }

        private System.Collections.IEnumerator Start()
        {
            SubscribeScenes();

            // UIの置き場所（UiRoot）の準備が終わってから画面を出す。
            yield return null;
            ShowLobbyLook(true);
            OpenFirstScreen();
            PlayLobbyBgm();
        }

        private void OnDestroy()
        {
            if (Active != this)
            {
                return;
            }

            UnsubscribeNetwork();
            UnsubscribeScenes();
            Discovery?.Dispose();
            Active = null;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            Discovery?.Tick(now);
            TickNetwork(now);
            TickCountdown(now);
            TickGripReady(Time.unscaledDeltaTime);
            TickInput();
            PublishValues(now);
        }

        // ───────── 名前と席 ─────────

        public void SetLocalName(string name)
        {
            var clean = (name ?? string.Empty).Trim();
            if (clean.Length > LobbyRoster.MaxNameLength)
            {
                clean = clean.Substring(0, LobbyRoster.MaxNameLength);
            }

            if (clean == LocalName)
            {
                return;
            }

            LocalName = clean;
            PlayerPrefs.SetString(PrefName, clean);
            RaiseChanged();
        }

        public void SelectSeat(int seat)
        {
            if (!LobbyRoster.IsValid(seat) || State == SessionState.Room || State == SessionState.InGame)
            {
                return;
            }

            SelectedSeat = seat;
            PlayerPrefs.SetInt(PrefSeat, seat);
            RaiseChanged();
        }

        /// <summary>このPCの名前（空なら P1 など）。</summary>
        public string DisplayName => LobbyRoster.CleanName(LocalName, SelectedSeat);

        private void LoadPrefs()
        {
            LocalName = PlayerPrefs.GetString(PrefName, string.Empty);
            SelectedSeat = Mathf.Clamp(PlayerPrefs.GetInt(PrefSeat, 0), 0, LobbyRoster.SeatCount - 1);
            LastAddress = PlayerPrefs.GetString(PrefAddress, defaultAddress);
            if (network != null && network.LaunchArgs.PlayerIndex.HasValue)
            {
                SelectedSeat = network.LaunchArgs.PlayerIndex.Value;
            }
        }

        private void RaiseChanged() => Changed?.Invoke();

        private void SetState(SessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            RaiseChanged();
        }
    }
}
